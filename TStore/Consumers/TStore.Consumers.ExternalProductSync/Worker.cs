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
        }

        private void StartConsumerThread(int idx)
        {
            Thread thread = new Thread(async () =>
            {
                try
                {
                    IKafkaProducerManager kafkaProducerManager = _serviceProvider.GetService<IKafkaProducerManager>();

                    using (IConsumer<string, ProductModel> consumer
                        = new ConsumerBuilder<string, ProductModel>(_baseConsumerConfig)
                            .SetValueDeserializer(new SimpleJsonSerdes<ProductModel>())
                            .Build())
                    {
                        consumer.Subscribe(EventConstants.Events.ProductUpdated);

                        bool cancelled = false;

                        while (!cancelled)
                        {
                            ConsumeResult<string, ProductModel> message = consumer.Consume(default(CancellationToken));

                            TransactionalProducerWrapper<string, string> producerWrapper = kafkaProducerManager
                                .GetTransactionalProducerFromPool<string, string>(
                                    _baseProducerConfig, _baseProducerConfig.DefaultPoolSize,
                                    nameof(ExternalProductSync), $"_{message.Message.Key}");

                            await producerWrapper.WrapTransactionAsync(async () =>
                            {
                                producerWrapper.BeginTransaction();

                                await _log.LogAsync($"Enter transaction {producerWrapper.TransactionalId}");

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

                                        await Task.Delay(1000);
                                    }

                                    producerWrapper.SendOffsetsToTransaction(
                                        new[] { message.TopicPartitionOffset },
                                        consumer.ConsumerGroupMetadata,
                                        TimeSpan.FromSeconds(30));

                                    producerWrapper.CommitTransaction();

                                    await _log.LogAsync($"Commit transaction {producerWrapper.TransactionalId}");

                                    await _log.LogAsync($"Consumer {idx} finishes processing product {message.Message.Value.Id}");
                                }
                                catch (Exception ex)
                                {
                                    producerWrapper.AbortTransaction();

                                    await _log.LogAsync($"Abort transaction {producerWrapper.TransactionalId}");

                                    throw ex;
                                }
                            });

                            kafkaProducerManager.Release(producerWrapper);
                        }

                        consumer.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
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
