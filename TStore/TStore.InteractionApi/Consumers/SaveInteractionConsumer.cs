﻿using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TStore.Shared.Constants;
using TStore.Shared.Models;
using TStore.Shared.Services;

namespace TStore.InteractionApi.Consumers
{
    public interface ISaveInteractionConsumer
    {
        void StartListenThread(int id);
    }

    public class SaveInteractionConsumer : ISaveInteractionConsumer
    {
        private readonly ConsumerConfig _config;
        private readonly IServiceProvider _provider;
        private readonly IApplicationLog _log;

        private int? _id;
        private readonly int _batchSize;

        public SaveInteractionConsumer(IConfiguration configuration,
            IServiceProvider provider,
            IApplicationLog log)
        {
            _batchSize = configuration.GetValue<int>("KafkaBatchSize");
            _provider = provider;
            _log = log;
            _config = new ConsumerConfig
            {
                BootstrapServers = configuration.GetSection("KafkaServers").Value,
                GroupId = configuration.GetSection("KafkaGroupId").Value,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false // [Important]
            };
        }

        public async Task ListenForNewInteractionAsync()
        {
            await _log.LogAsync("[SAVE INTERACTION HANDLER]");

            bool cancelled = false;

            using (IConsumer<string, string> consumer = new ConsumerBuilder<string, string>(_config).Build())
            {
                consumer.Subscribe(EventConstants.Events.NewUnsavedInteraction);

                List<ConsumeResult<string, string>> batch = new List<ConsumeResult<string, string>>();
                bool isTimeout = false;

                while (!cancelled)
                {
                    ConsumeResult<string, string> message = consumer.Consume(100);

                    if (message != null)
                    {
                        await _log.LogAsync($"Consumer {_id} is handling message {message.Message.Key} - {message.Message.Timestamp.UtcDateTime}");

                        batch.Add(message);
                    }
                    else
                    {
                        isTimeout = true;
                    }

                    if (batch.Count > 0 && (batch.Count >= _batchSize || isTimeout))
                    {
                        await _log.LogAsync($"Consumer {_id} begins processing batch of {batch.Count} messages");

                        using (IServiceScope scope = _provider.CreateScope())
                        {
                            List<InteractionModel> interactions = batch
                                .Select(m => JsonConvert.DeserializeObject<InteractionModel>(m.Message.Value))
                                .ToList();

                            await HandleNewInteractionsAsync(scope.ServiceProvider, interactions);
                        }

                        consumer.Commit();
                        batch.Clear();
                        isTimeout = false;
                    }
                }

                consumer.Close();
            }
        }

        public void StartListenThread(int id)
        {
            if (_id != null) throw new Exception("Already started");

            _id = id;

            Thread thread = new Thread(async () => await ListenForNewInteractionAsync())
            {
                IsBackground = true
            };

            thread.Start();
        }

        private async Task HandleNewInteractionsAsync(IServiceProvider serviceProvider, List<InteractionModel> interactions)
        {
            IInteractionService interactionService = serviceProvider.GetService<IInteractionService>();

            await interactionService.SaveInteractionsAsync(interactions);

            await _log.LogAsync($"Finish saving interaction batch of {interactions.Count}");
        }
    }
}