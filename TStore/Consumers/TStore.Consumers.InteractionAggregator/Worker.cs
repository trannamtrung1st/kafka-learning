﻿using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TStore.Shared.Constants;
using TStore.Shared.Models;
using TStore.Shared.Serdes;
using TStore.Shared.Services;

namespace TStore.Consumers.InteractionAggregator
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
                try
                {
                    ConsumerConfig config = new ConsumerConfig
                    {
                        BootstrapServers = _configuration.GetSection("KafkaServers").Value,
                        GroupId = _configuration.GetSection("KafkaGroupId").Value,
                        AutoOffsetReset = AutoOffsetReset.Earliest
                    };

                    bool cancelled = false;

                    using (IConsumer<string, IEnumerable<InteractionModel>> consumer
                        = new ConsumerBuilder<string, IEnumerable<InteractionModel>>(config)
                            .SetValueDeserializer(new SimpleJsonSerdes<IEnumerable<InteractionModel>>())
                            .Build())
                    {
                        consumer.Subscribe(EventConstants.Events.NewRecordedInteraction);

                        while (!cancelled)
                        {
                            ConsumeResult<string, IEnumerable<InteractionModel>> message = consumer.Consume(default(CancellationToken));

                            await _log.LogAsync($"Consumer {idx} begins handle message {message.Message.Timestamp.UtcDateTime}");

                            using (IServiceScope scope = _serviceProvider.CreateScope())
                            {
                                await HandleNewInteractionsAsync(message.Message.Value);
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

        private async Task HandleNewInteractionsAsync(IEnumerable<InteractionModel> interactionModels)
        {
            IInteractionService interactionService = _serviceProvider.GetRequiredService<IInteractionService>();

            await interactionService.AggregateInteractionReportAsync(interactionModels);

            await _log.LogAsync($"Finish aggregating interaction reports for {interactionModels.Count()} interactions");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _log.LogAsync("[INTERACTION AGGREGATOR]");

            int consumerCount = _configuration.GetValue<int>("KafkaConsumerCount");

            for (int i = 0; i < consumerCount; i++)
            {
                StartConsumerThread(i);
            }
        }
    }
}
