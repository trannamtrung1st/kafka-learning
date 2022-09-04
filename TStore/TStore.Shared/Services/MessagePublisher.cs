using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TStore.Shared.Serdes;

namespace TStore.Shared.Services
{
    public interface ICommonMessagePublisher
    {
        Task PublishAsync<TKey, TValue>(string eventName, TKey key, TValue value);
    }

    public class KafkaCommonMessagePublisher : ICommonMessagePublisher, IDisposable
    {
        private readonly ConcurrentDictionary<string, IClient> _producerMap;
        private bool _disposedValue;
        private readonly ProducerConfig _config;

        public KafkaCommonMessagePublisher(IConfiguration configuration)
        {
            string kafkaServers = configuration.GetSection("KafkaServers").Value;
            string kafkaClientId = configuration.GetSection("KafkaClientId").Value;
            string caRootLocation = configuration.GetSection("KafkaCaCert").Value;

            _config = new ProducerConfig
            {
                BootstrapServers = kafkaServers,
                ClientId = kafkaClientId,
                SecurityProtocol = SecurityProtocol.Ssl,
                SslCaLocation = caRootLocation, // or just install ca-root.crt
            };

            _producerMap = new ConcurrentDictionary<string, IClient>();
        }

        public async Task PublishAsync<TKey, TValue>(string eventName, TKey key, TValue value)
        {
            IProducer<TKey, TValue> producer = _producerMap.GetOrAdd(eventName, (key) =>
            {
                ProducerBuilder<TKey, TValue> builder = new ProducerBuilder<TKey, TValue>(_config);

                Type valueType = typeof(TValue);

                if (valueType.IsClass || valueType.IsInterface)
                {
                    builder.SetValueSerializer(new SimpleJsonSerdes<TValue>());
                }

                return builder.Build();
            }) as IProducer<TKey, TValue>;

            await producer.ProduceAsync(eventName, new Message<TKey, TValue>()
            {
                Key = key,
                Value = value,
                Timestamp = new Timestamp(DateTimeOffset.UtcNow),
            });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)

                    foreach (IClient producer in _producerMap.Values)
                    {
                        try
                        {
                            producer.Dispose();
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine(e);
                        }
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~KafkaMessagePublisher()
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
