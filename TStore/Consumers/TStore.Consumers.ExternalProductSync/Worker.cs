using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;
using TStore.Shared.Configs;
using TStore.Shared.Constants;
using TStore.Shared.Helpers;
using TStore.Shared.Models;
using TStore.Shared.Serdes;
using TStore.Shared.Services;

namespace TStore.Consumers.ExternalProductSync
{
    public class Worker : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IApplicationLog _log;
        private readonly AppConsumerConfig _baseConsumerConfig;
        private readonly AppProducerConfig _baseProducerConfig;
        private readonly int _transactionCommitInterval;

        public Worker(IServiceProvider serviceProvider, IConfiguration configuration,
            IApplicationLog log)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _log = log;
            _baseConsumerConfig = new AppConsumerConfig();
            _configuration.Bind("ExternalProductSyncConsumerConfig", _baseConsumerConfig);
            _baseProducerConfig = new AppProducerConfig();
            _configuration.Bind("ExternalProductProducerConfig", _baseProducerConfig);

            if (_configuration.GetValue<bool>("StartFromVS"))
            {
                _baseConsumerConfig.FindCertIfNotFound();
                _baseProducerConfig.FindCertIfNotFound();
            }

            _transactionCommitInterval = _configuration.GetValue<int>("TransationCommitInterval");
        }

        private void StartConsumerThread(int idx)
        {
            Thread thread = new Thread(async () =>
            {
                IKafkaProducerManager kafkaProducerManager = _serviceProvider.GetService<IKafkaProducerManager>();
                SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

                // [Important] 1 TransactionalId per producer instance
                TransactionalProducerWrapper<string, string> producerWrapper = null;

                try
                {
                    producerWrapper = await kafkaProducerManager
                        .GetTransactionalProducerFromPoolAsync<string, string>(
                            _baseProducerConfig, _baseProducerConfig.DefaultPoolSize,
                            nameof(ExternalProductSync), idx);

                    using (IConsumer<string, ProductModel> consumer
                        = new ConsumerBuilder<string, ProductModel>(_baseConsumerConfig)
                            .SetValueDeserializer(new SimpleJsonSerdes<ProductModel>())
                            .Build())
                    {
                        bool cancelled = false;
                        bool inTransaction = false;

                        Func<Task> abortTransFunc = async () =>
                        {
                            producerWrapper.AbortTransaction();

                            inTransaction = false;

                            // [DEMO] re-subscribe
                            consumer.Unsubscribe();
                            consumer.Subscribe(new[]
                            {
                                EventConstants.Events.ProductCreated,
                                EventConstants.Events.ProductUpdated
                            });

                            await _log.LogAsync($"Aborted transaction {producerWrapper.TransactionalId}");
                        };

                        consumer.Subscribe(new[]
                        {
                            EventConstants.Events.ProductCreated,
                            EventConstants.Events.ProductUpdated
                        });

                        while (!cancelled)
                        {
                            ConsumeResult<string, ProductModel> message = consumer.Consume(default(CancellationToken));

                            try
                            {
                                await semaphore.WaitAsync();

                                if (!inTransaction)
                                {
                                    producerWrapper.BeginTransaction();

                                    inTransaction = true;

                                    await _log.LogAsync($"Entered transaction {producerWrapper.TransactionalId}");

                                    System.Timers.Timer commitTimer = new System.Timers.Timer()
                                    {
                                        AutoReset = false,
                                        Interval = _transactionCommitInterval
                                    };
                                    commitTimer.Elapsed += async (obj, e) =>
                                    {
                                        try
                                        {
                                            await semaphore.WaitAsync();

                                            producerWrapper.CommitTransaction();

                                            inTransaction = false;

                                            await _log.LogAsync($"Committed transaction {producerWrapper.TransactionalId}");
                                        }
                                        catch (Exception ex)
                                        {
                                            await abortTransFunc();
                                            throw ex;
                                        }
                                        finally
                                        {
                                            commitTimer.Stop();
                                            commitTimer.Dispose();
                                            semaphore.Release();
                                        }
                                    };
                                    commitTimer.Start();
                                }

                                await producerWrapper.TryRunAsync(async () =>
                                {
                                    // [DEMO] heavy task
                                    int? syncDelay = _configuration.GetValue<int?>("SyncDelay");
                                    if (syncDelay > 0)
                                    {
                                        await Task.Delay(syncDelay.Value);
                                    }

                                    try
                                    {
                                        await _log.LogAsync($"Consumer {idx} begins to transform product {JsonConvert.SerializeObject(message.Message.Value)}");

                                        for (int i = 0; i < 10; i++)
                                        {
                                            await producerWrapper.ProduceAsync(EventConstants.Events.SampleEvents,
                                                new Message<string, string>
                                                {
                                                    Key = message.Message.Key,
                                                    Value = $"Sample value {i}"
                                                });
                                        }

                                        producerWrapper.SendOffsetsToTransaction(
                                            new[] { message.TopicPartitionOffset },
                                            consumer.ConsumerGroupMetadata,
                                            TimeSpan.FromSeconds(30));

                                        await _log.LogAsync($"Consumer {idx} finishes processing product {message.Message.Value.Id}");
                                    }
                                    catch (Exception ex)
                                    {
                                        await abortTransFunc();
                                        throw ex;
                                    }
                                });
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }

                        consumer.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
                finally
                {
                    if (producerWrapper != null)
                    {
                        kafkaProducerManager.Release(producerWrapper);
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _log.LogAsync("[EXTERNAL PRODUCT SYNC]");

            for (int i = 0; i < _baseConsumerConfig.ConsumerCount; i++)
            {
                StartConsumerThread(i);
            }
        }
    }
}
