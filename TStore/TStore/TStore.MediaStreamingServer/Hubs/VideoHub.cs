using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TStore.MediaStreamingServer.Hubs
{
    public class VideoHub : Hub
    {
        public struct VideoData
        {
            public int Index { get; }
            public string Part { get; }


            [JsonConstructor]
            public VideoData(int index, string part) => (Index, Part) = (index, part);
        }

        public async Task SendVideoData(IAsyncEnumerable<VideoData> videoData)
        {
            await foreach (var d in videoData)
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = "localhost:9092",
                    ClientId = "Stream1",
                };

                using (var producer = new ProducerBuilder<Null, string>(config).Build())
                {
                    await producer.ProduceAsync("stream", new Message<Null, string> { Value = JsonConvert.SerializeObject(d) });
                    Console.WriteLine("Sent to kafka");
                }
            }
        }
    }
}
