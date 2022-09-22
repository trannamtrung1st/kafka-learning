﻿using Confluent.Kafka;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TStore.Shared.Configs;
using TStore.Shared.Serdes;

namespace TStore.Shared.Services
{
    public interface IKafkaProducerManager : IDisposable
    {
        IProducer<TKey, TValue> GetCommonProducer<TKey, TValue>(
            string eventName,
            AppProducerConfig config);
        TransactionalProducerWrapper<TKey, TValue> GetTransactionalProducerFromPool<TKey, TValue>(
            AppProducerConfig config,
            int poolSize,
            string transactionName,
            string transactionSuffix);
        void Release<TKey, TValue>(TransactionalProducerWrapper<TKey, TValue> producer);
    }

    public class KafkaProducerManager : IKafkaProducerManager
    {
        private bool _disposedValue;
        private readonly ConcurrentDictionary<string, ProducerPool> _producerPoolMap;
        private readonly ConcurrentDictionary<string, IClient> _commonProducerMap;

        public KafkaProducerManager()
        {
            _producerPoolMap = new ConcurrentDictionary<string, ProducerPool>();
            _commonProducerMap = new ConcurrentDictionary<string, IClient>();
        }

        public IProducer<TKey, TValue> GetCommonProducer<TKey, TValue>(
            string eventName,
            AppProducerConfig config)
        {
            IProducer<TKey, TValue> producer;

            if (!_commonProducerMap.TryGetValue(eventName, out IClient producerObj))
            {
                ProducerConfig clonedConfig = config.Clone();

                ProducerBuilder<TKey, TValue> builder = new ProducerBuilder<TKey, TValue>(clonedConfig);

                Type valueType = typeof(TValue);

                if (valueType.IsClass || valueType.IsInterface)
                {
                    builder.SetValueSerializer(new SimpleJsonSerdes<TValue>());
                }

                producer = builder.Build();
            }
            else
            {
                producer = producerObj as IProducer<TKey, TValue>;
            }

            return producer;
        }

        public TransactionalProducerWrapper<TKey, TValue> GetTransactionalProducerFromPool<TKey, TValue>(
            AppProducerConfig config,
            int poolSize,
            string transactionName,
            string transactionSuffix)
        {
            if (poolSize == 0)
            {
                throw new ArgumentException("Invalid pool size");
            }

            TransactionalProducerWrapper<TKey, TValue> producerWrapper = null;
            bool existedBefore = true;
            ProducerPool producerPool;

            lock (_producerPoolMap)
            {
                producerPool = _producerPoolMap.GetOrAdd(transactionName, key =>
                {
                    existedBefore = false;
                    return new ProducerPool(poolSize);
                });
            }

            if (!existedBefore)
            {
                lock (producerPool)
                {
                    producerWrapper = producerPool.InitProducers<TKey, TValue>(config, transactionName, transactionSuffix);
                }
            }

            if (producerWrapper == null)
            {
                producerWrapper = producerPool.GetProducer<TKey, TValue>(transactionSuffix);
            }

            if (producerWrapper.LastLock != null && DateTime.UtcNow - producerWrapper.LastLock > TimeSpan.FromSeconds(30))
            {
                producerWrapper.Unlock();
            }

            producerWrapper.Lock();

            if (!producerWrapper.Initialized)
            {
                producerWrapper.Initialize();
            }

            return producerWrapper;
        }

        public void Release<TKey, TValue>(TransactionalProducerWrapper<TKey, TValue> producerWrapper)
        {
            if (producerWrapper.LastLock != null)
            {
                producerWrapper.Unlock();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                foreach (ProducerPool pool in _producerPoolMap.Values)
                {
                    pool.Dispose();
                }

                foreach (IDisposable producer in _commonProducerMap.Values)
                {
                    try
                    {
                        producer.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    internal class ProducerPool : ConcurrentDictionary<int, IDisposable>, IDisposable
    {
        private bool _disposedValue;

        public int PoolSize { get; }

        public ProducerPool(int poolSize)
        {
            PoolSize = poolSize;
        }

        public TransactionalProducerWrapper<TKey, TValue> GetProducer<TKey, TValue>(string transactionSuffix)
        {
            int poolId = GetPoolId(PoolSize, transactionSuffix);

            TransactionalProducerWrapper<TKey, TValue> producerWrapper = this[poolId] as TransactionalProducerWrapper<TKey, TValue>;

            return producerWrapper;
        }

        public TransactionalProducerWrapper<TKey, TValue> InitProducers<TKey, TValue>(
            AppProducerConfig config,
            string transactionName,
            string transactionSuffix)
        {
            int currentPoolId = GetPoolId(PoolSize, transactionSuffix);

            TransactionalProducerWrapper<TKey, TValue> producerWrapper =
                CreateProducerAndAddToPool<TKey, TValue>(config, currentPoolId, transactionName);

            Task.Run(() =>
            {
                for (int id = 0; id < PoolSize; id++)
                {
                    if (id != currentPoolId)
                    {
                        CreateProducerAndAddToPool<TKey, TValue>(config, id, transactionName);
                    }
                }
            });

            return producerWrapper;
        }

        private TransactionalProducerWrapper<TKey, TValue> CreateProducerAndAddToPool<TKey, TValue>(
            AppProducerConfig config,
            int poolId,
            string transactionName)
        {
            string finalTransId = GetFinalTransactionId(transactionName, poolId);
            TransactionalProducerWrapper<TKey, TValue> producerWrapper = new TransactionalProducerWrapper<TKey, TValue>(config, finalTransId);
            this[poolId] = producerWrapper;
            return producerWrapper;
        }

        private string GetFinalTransactionId(string transactionName, int poolId)
            => $"{transactionName}_{poolId}";

        private int GetPoolId(int poolSize, string suffix) => Math.Abs(suffix.GetHashCode()) % poolSize;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    foreach (IDisposable producer in Values)
                    {
                        try
                        {
                            producer.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class TransactionalProducerWrapper<TKey, TValue> : IDisposable
    {
        private bool _disposedValue;
        private readonly SemaphoreSlim _semaphoreSlim;
        private readonly ProducerConfig _config;
        private readonly IAsyncPolicy _asyncPolicy;

        private IProducer<TKey, TValue> _producer;

        public DateTime? LastLock { get; set; }
        public string TransactionalId => _config.TransactionalId;
        public bool Initialized => LastInitialization != null;
        public DateTime? LastInitialization { get; private set; }

        public TransactionalProducerWrapper(AppProducerConfig config, string finalTransId)
        {
            _config = config.Clone();
            _config.TransactionalId = finalTransId;
            _semaphoreSlim = new SemaphoreSlim(1, 1);
            _producer = CreateNewTransactionalProducer();
            _asyncPolicy = BuildProduceExceptionRetryPolicy();
        }

        public Task WrapTransactionAsync(Func<Task> action)
        {
            return _asyncPolicy.ExecuteAsync(action);
        }

        public void BeginTransaction()
        {
            _producer.BeginTransaction();
        }

        public void CommitTransaction()
        {
            _producer.CommitTransaction();
        }

        public void AbortTransaction()
        {
            _producer.AbortTransaction();
        }

        public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout)
        {
            _producer.SendOffsetsToTransaction(offsets, groupMetadata, timeout);
        }

        public Task<DeliveryResult<TKey, TValue>> ProduceAsync(string topic, Message<TKey, TValue> message, CancellationToken cancellationToken = default(CancellationToken))
        {
            return _producer.ProduceAsync(topic, message, cancellationToken);
        }

        public void Initialize()
        {
            // [Important] init transactions, fence out old trans
            _producer.InitTransactions(TimeSpan.FromSeconds(30));

            Console.WriteLine($"Initialized transaction {TransactionalId}");

            LastInitialization = DateTime.UtcNow;
        }

        public void Unlock()
        {
            LastLock = null;

            _semaphoreSlim.Release();
        }

        public void Lock(TimeSpan? timeout = null)
        {
            bool lockWasTaken = _semaphoreSlim.Wait(timeout ?? TimeSpan.FromSeconds(7));

            if (lockWasTaken)
            {
                LastLock = DateTime.UtcNow;
            }
            else
            {
                throw new TimeoutException("Timeout waiting for producer");
            }
        }

        private IProducer<TKey, TValue> CreateNewTransactionalProducer()
        {
            ProducerBuilder<TKey, TValue> builder = new ProducerBuilder<TKey, TValue>(_config);

            Type valueType = typeof(TValue);

            if (valueType.IsClass || valueType.IsInterface)
            {
                builder.SetValueSerializer(new SimpleJsonSerdes<TValue>());
            }

            IProducer<TKey, TValue> producer = builder.Build();

            return producer;
        }

        private IAsyncPolicy BuildProduceExceptionRetryPolicy()
        {
            var jitterDelay = Backoff.DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromSeconds(2), retryCount: 2);

            AsyncRetryPolicy produceRetry = Policy
                .Handle<ProduceException<TKey, TValue>>(ex => ex.Error.IsLocalError)
                .Or<KafkaException>(ex => ex.Error.Code == ErrorCode.InvalidProducerIdMapping)
                .WaitAndRetryAsync(jitterDelay, onRetry: (exception, delay, count, context) =>
                {
                    if (count == 1)
                    {
                        Console.WriteLine("Re-initializing");
                        _producer.Dispose();
                        _producer = CreateNewTransactionalProducer();
                        Initialize();
                        Console.WriteLine("Re-initialized");
                    }
                });

            return produceRetry;
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                _producer.Dispose();

                _disposedValue = true;
            }
        }

        ~TransactionalProducerWrapper()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
