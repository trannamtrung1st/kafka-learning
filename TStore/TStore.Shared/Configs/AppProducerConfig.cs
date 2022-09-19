using Confluent.Kafka;

namespace TStore.Shared.Configs
{
    public class AppProducerConfig : ProducerConfig
    {
        public bool ProduceDuplication { get; set; }
    }
}
