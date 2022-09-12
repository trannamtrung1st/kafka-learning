using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        private readonly AppConsumerConfig _baseConfig;

        // [DEMO]
        private HashSet<Guid> _existenceSet;

        public Worker(IServiceProvider serviceProvider, IConfiguration configuration,
            IApplicationLog log)
        {
            _existenceSet = new HashSet<Guid>();
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _log = log;
            _baseConfig = new AppConsumerConfig();
            _configuration.Bind("ExternalProductSyncConsumerConfig", _baseConfig);

            if (_configuration.GetValue<bool>("StartFromVS"))
            {
                _baseConfig.FindCertIfNotFound();
            }
        }

        private void StartConsumerThread(int idx)
        {
            Thread thread = new Thread(async () =>
            {
                try
                {
                    using (IConsumer<string, ProductModel> consumer
                        = new ConsumerBuilder<string, ProductModel>(_baseConfig)
                            .SetValueDeserializer(new SimpleJsonSerdes<ProductModel>())
                            .Build())
                    {
                        consumer.Subscribe(EventConstants.Events.ProductUpdated);

                        bool cancelled = false;

                        while (!cancelled)
                        {
                            ConsumeResult<string, ProductModel> message = consumer.Consume(default(CancellationToken));

                            if (!_existenceSet.Add(message.Message.Value.Id))
                            {
                                await _log.LogAsync($"Consumer {idx} begins UPDATING product {JsonConvert.SerializeObject(message.Message.Value)}");
                            }
                            else
                            {
                                await _log.LogAsync($"Consumer {idx} begins CREATING product {JsonConvert.SerializeObject(message.Message.Value)}");
                            }

                            try
                            {
                                consumer.Commit();
                            }
                            catch (Exception ex)
                            {
                                await _log.LogAsync(ex.Message);
                            }
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

            for (int i = 0; i < _baseConfig.ConsumerCount; i++)
            {
                StartConsumerThread(i);
            }
        }
    }
}
