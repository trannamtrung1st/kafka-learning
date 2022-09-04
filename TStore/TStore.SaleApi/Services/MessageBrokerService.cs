using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TStore.Shared.Constants;
using TStore.Shared.Services;

namespace TStore.SaleApi.Services
{
    public interface IMessageBrokerService
    {
        Task InitializeTopicsAsync();
    }

    public class KafkaMessageBrokerService : IMessageBrokerService, IDisposable
    {
        private bool _disposedValue;
        private readonly IAdminClient _adminClient;
        private readonly IApplicationLog _log;

        public KafkaMessageBrokerService(IConfiguration configuration,
            IApplicationLog log)
        {
            _log = log;
            string kafkaServers = configuration.GetSection("KafkaServers").Value;

            AdminClientConfig config = new AdminClientConfig
            {
                BootstrapServers = kafkaServers
            };

            _adminClient = new AdminClientBuilder(config).Build();
        }

        public async Task InitializeTopicsAsync()
        {
            Metadata metadata = _adminClient.GetMetadata(TimeSpan.FromSeconds(10));

            string[] topicNames = metadata.Topics.Select(t => t.Topic).ToArray();

            List<TopicSpecification> topicSpecs = new List<TopicSpecification>();

            if (!topicNames.Contains(EventConstants.Events.NewOrder))
            {
                topicSpecs.Add(new TopicSpecification
                {
                    Name = EventConstants.Events.NewOrder,
                    NumPartitions = 7
                });
            }

            if (!topicNames.Contains(EventConstants.Events.PromotionApplied))
            {
                topicSpecs.Add(new TopicSpecification
                {
                    Name = EventConstants.Events.PromotionApplied,
                    NumPartitions = 7
                });
            }

            if (!topicNames.Contains(EventConstants.Events.NewUnsavedInteraction))
            {
                topicSpecs.Add(new TopicSpecification
                {
                    Name = EventConstants.Events.NewUnsavedInteraction,
                    NumPartitions = 7
                });
            }

            if (!topicNames.Contains(EventConstants.Events.NewRecordedInteraction))
            {
                topicSpecs.Add(new TopicSpecification
                {
                    Name = EventConstants.Events.NewRecordedInteraction,
                    NumPartitions = 7
                });
            }

            if (topicSpecs.Count > 0)
            {
                await _adminClient.CreateTopicsAsync(topicSpecs);

                await _log.LogAsync($"Finish creating topics: {string.Join(", ", topicNames)}");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _adminClient?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~TopicManagementService()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
