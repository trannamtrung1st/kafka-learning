using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TStore.Shared.Configs;
using TStore.Shared.Helpers;
using TStore.Shared.Serdes;

namespace TStore.Shared.Services
{
    public interface ICommonMessagePublisher
    {
        Task PublishAsync<TKey, TValue>(string eventName, TKey key, TValue value, Action<object> deliveryHandler = null);
        Task<object> PublishAndWaitAsync<TKey, TValue>(string eventName, TKey key, TValue value);
    }

    public class KafkaCommonMessagePublisher : ICommonMessagePublisher, IDisposable
    {
        private readonly ConcurrentDictionary<string, IClient> _producerMap;
        private bool _disposedValue;
        private readonly AppProducerConfig _baseConfig;

        public KafkaCommonMessagePublisher(IConfiguration configuration)
        {
            _baseConfig = new AppProducerConfig();
            configuration.Bind("CommonProducerConfig", _baseConfig);

            if (configuration.GetValue<bool>("StartFromVS"))
            {
                _baseConfig.FindCertIfNotFound();
            }

            _producerMap = new ConcurrentDictionary<string, IClient>();
        }

        public Task PublishAsync<TKey, TValue>(string eventName, TKey key, TValue value, Action<object> deliveryHandler = null)
        {
            IProducer<TKey, TValue> producer = GetProducer<TKey, TValue>(eventName);

            // [DEMO] async call, provide delivery result callback
            Action produceAct = () => producer.Produce(eventName, new Message<TKey, TValue>()
            {
                Key = key,
                Value = value,
                Timestamp = new Timestamp(DateTimeOffset.UtcNow),
            }, deliveryHandler);

            produceAct();

            if (_baseConfig.ProduceDuplication) produceAct();

            return Task.CompletedTask;
        }

        public async Task<object> PublishAndWaitAsync<TKey, TValue>(string eventName, TKey key, TValue value)
        {
            IProducer<TKey, TValue> producer = GetProducer<TKey, TValue>(eventName);

            Func<Task<DeliveryResult<TKey, TValue>>> produceAct = async () => await producer.ProduceAsync(eventName, new Message<TKey, TValue>()
            {
                Key = key,
                Value = value,
                Timestamp = new Timestamp(DateTimeOffset.UtcNow),
            });

            DeliveryResult<TKey, TValue> result = await produceAct();

            if (_baseConfig.ProduceDuplication) await produceAct();

            return result;
        }

        public IProducer<TKey, TValue> GetProducer<TKey, TValue>(string eventName)
        {
            IProducer<TKey, TValue> producer = _producerMap.GetOrAdd(eventName, (key) =>
            {
                ProducerBuilder<TKey, TValue> builder = new ProducerBuilder<TKey, TValue>(_baseConfig);

                Type valueType = typeof(TValue);

                if (valueType.IsClass || valueType.IsInterface)
                {
                    builder.SetValueSerializer(new SimpleJsonSerdes<TValue>());
                }

                return builder.Build();
            }) as IProducer<TKey, TValue>;

            return producer;
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
