using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TStore.Shared.Constants;
using TStore.Shared.Models;
using TStore.Shared.Services;

namespace TStore.Consumers.InteractionBigDataLoader
{
    public class Worker : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IApplicationLog _log;

        public Worker(IServiceProvider serviceProvider, IConfiguration configuration,
            IApplicationLog log)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _log = log;
        }

        private void StartConsumerThread(int idx)
        {
            Thread thread = new Thread(async () =>
            {
                ConsumerConfig config = new ConsumerConfig
                {
                    BootstrapServers = _configuration.GetSection("KafkaServers").Value,
                    GroupId = _configuration.GetSection("KafkaGroupId").Value,
                    AutoOffsetReset = AutoOffsetReset.Latest
                };

                bool cancelled = false;

                using (IConsumer<string, string> consumer = new ConsumerBuilder<string, string>(config).Build())
                {
                    consumer.Subscribe(EventConstants.Events.NewRecordedInteraction);

                    while (!cancelled)
                    {
                        ConsumeResult<string, string> message = consumer.Consume(default(CancellationToken));

                        await _log.LogAsync($"Consumer {idx} begins handle message {message.Message.Timestamp.UtcDateTime}");

                        using (IServiceScope scope = _serviceProvider.CreateScope())
                        {
                            await HandleNewInteractionsAsync(
                                JsonConvert.DeserializeObject<IEnumerable<InteractionModel>>(message.Message.Value));
                        }
                    }

                    consumer.Close();
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private async Task HandleNewInteractionsAsync(IEnumerable<InteractionModel> interactionModels)
        {
            IInteractionService interactionService = _serviceProvider.GetRequiredService<IInteractionService>();

            string dataDir = _configuration.GetValue<string>("DataDir");

            await interactionService.BigDataLoad(dataDir, interactionModels);

            await _log.LogAsync($"Finish loading {interactionModels.Count()} interactions to big data store");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _log.LogAsync("[INTERACTION BIG DATA LOADER]");

            int consumerCount = _configuration.GetValue<int>("KafkaConsumerCount");

            for (int i = 0; i < consumerCount; i++)
            {
                StartConsumerThread(i);
            }
        }
    }
}
