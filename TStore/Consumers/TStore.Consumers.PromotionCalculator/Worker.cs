using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;
using TStore.Shared.Constants;
using TStore.Shared.Models;
using TStore.Shared.Services;

namespace TStore.Consumers.PromotionCalculator
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
                ConsumerConfig config = new ConsumerConfig
                {
                    BootstrapServers = _configuration.GetSection("KafkaServers").Value,
                    GroupId = _configuration.GetSection("KafkaGroupId").Value,
                    AutoOffsetReset = AutoOffsetReset.Earliest
                };

                using (IConsumer<string, string> consumer = new ConsumerBuilder<string, string>(config).Build())
                {
                    consumer.Subscribe(EventConstants.Events.NewOrder);

                    bool cancelled = false;

                    while (!cancelled)
                    {
                        ConsumeResult<string, string> message = consumer.Consume(default(CancellationToken));

                        await _log.LogAsync($"Consumer {idx} begins handle message {message.Message.Timestamp.UtcDateTime}");

                        using (IServiceScope scope = _serviceProvider.CreateScope())
                        {
                            await HandleNewOrderAsync(
                                Guid.Parse(message.Message.Key),
                                JsonConvert.DeserializeObject<OrderModel>(message.Message.Value));
                        }
                    }

                    consumer.Close();
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private async Task HandleNewOrderAsync(Guid key, OrderModel orderModel)
        {
            IOrderService orderService = _serviceProvider.GetService<IOrderService>();

            double discount = await orderService.ApplyDiscountAsync(key, orderModel);

            await _log.LogAsync($"Finish applying discount of ${discount} for order {orderModel.Id}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _log.LogAsync("[PROMOTION CALCULATOR]");

            int consumerCount = _configuration.GetValue<int>("KafkaConsumerCount");

            for (int i = 0; i < consumerCount; i++)
            {
                StartConsumerThread(i);
            }
        }
    }
}
