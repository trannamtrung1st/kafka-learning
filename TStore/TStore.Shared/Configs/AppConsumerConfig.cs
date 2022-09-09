using Confluent.Kafka;

namespace TStore.Shared.Configs
{
    public class AppConsumerConfig : ConsumerConfig
    {
        public int ConsumerCount { get; set; }
        public int ProcessingBatchSize { get; set; }
    }
}
