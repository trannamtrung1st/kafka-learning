using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TStore.Shared.Constants;
using TStore.Shared.Helpers;
using TStore.Shared.Services;

namespace TStore.SaleApi.Services
{
    public interface IMessageBrokerService
    {
        Task InitializeTopicsAsync();
        Task InitializeAclsAsync();
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

            AdminClientConfig config = new AdminClientConfig();
            configuration.Bind("CommonAdminClientConfig", config);

            if (configuration.GetValue<bool>("StartFromVS"))
            {
                config.FindCertIfNotFound();
            }

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

            if (!topicNames.Contains(EventConstants.Events.ShipApplied))
            {
                topicSpecs.Add(new TopicSpecification
                {
                    Name = EventConstants.Events.ShipApplied,
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
                    NumPartitions = 7,
                    Configs = new Dictionary<string, string>
                    {
                        ["max.message.bytes"] = "1048588" // allow batch up to 1MB
                    }
                });
            }

            if (topicSpecs.Count > 0)
            {
                await _adminClient.CreateTopicsAsync(topicSpecs);

                await _log.LogAsync($"Finish creating topics: " +
                    $"{string.Join(", ", topicSpecs.Select(spec => spec.Name))}");
            }
        }

        public async Task InitializeAclsAsync()
        {
            Metadata metadata = _adminClient.GetMetadata(TimeSpan.FromSeconds(10));

            DescribeAclsResult describeResult = await _adminClient.DescribeAclsAsync(new AclBindingFilter()
            {
                PatternFilter = new ResourcePatternFilter
                {
                    ResourcePatternType = ResourcePatternType.Any,
                    Type = ResourceType.Any,
                },
                EntryFilter = new AccessControlEntryFilter
                {
                    Operation = AclOperation.Any,
                    PermissionType = AclPermissionType.Any,
                }
            });

            AccessControlEntry allowConsumerRead = new AccessControlEntry
            {
                Host = "*",
                Operation = AclOperation.Read,
                PermissionType = AclPermissionType.Allow,
                Principal = "User:consumer"
            };

            List<AclBinding> aclBindings = new List<AclBinding>()
            {
                new AclBinding
                {
                    Entry = new AccessControlEntry
                    {
                        Host = "*",
                        Operation = AclOperation.Write,
                        PermissionType = AclPermissionType.Allow,
                        Principal = "User:producer"
                    },
                    Pattern = new ResourcePattern
                    {
                        Name = "*",
                        Type = ResourceType.Topic,
                        ResourcePatternType = ResourcePatternType.Literal
                    }
                },
                new AclBinding
                {
                    Entry = allowConsumerRead,
                    Pattern = new ResourcePattern
                    {
                        Name = "*",
                        Type = ResourceType.Topic,
                        ResourcePatternType = ResourcePatternType.Literal
                    }
                },
                new AclBinding
                {
                    Entry = allowConsumerRead,
                    Pattern = new ResourcePattern
                    {
                        Name = "*",
                        Type = ResourceType.Group,
                        ResourcePatternType = ResourcePatternType.Literal
                    }
                }
            };

            aclBindings = aclBindings
                .Where(srcBinding => !describeResult.AclBindings.Any(destBinding => AclEquals(srcBinding, destBinding)))
                .ToList();

            if (aclBindings.Count > 0)
            {
                Console.WriteLine("Creating ACLs:");
                foreach (AclBinding aclBinding in aclBindings)
                {
                    Console.WriteLine(aclBinding);
                }

                await _adminClient.CreateAclsAsync(aclBindings);
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

        private bool AclEquals(AclBinding src, AclBinding dest)
        {
            return src.Entry?.Principal == dest.Entry?.Principal
                && src.Entry?.PermissionType == dest.Entry?.PermissionType
                && src.Entry?.Operation == dest.Entry?.Operation
                && src.Entry?.Host == dest.Entry?.Host
                && src.Pattern?.ResourcePatternType == dest.Pattern?.ResourcePatternType
                && src.Pattern?.Type == dest.Pattern?.Type
                && src.Pattern?.Name == dest.Pattern?.Name;
        }
    }
}
