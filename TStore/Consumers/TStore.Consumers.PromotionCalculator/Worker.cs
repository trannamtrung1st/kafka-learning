using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using TStore.Shared.Configs;
using TStore.Shared.Constants;
using TStore.Shared.Models;
using TStore.Shared.Serdes;
using TStore.Shared.Services;

namespace TStore.Consumers.PromotionCalculator
{
    public class Worker : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IApplicationLog _log;
        private readonly AppConsumerConfig _baseConfig;

        public Worker(IServiceProvider serviceProvider, IConfiguration configuration,
            IApplicationLog log)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _log = log;
            _baseConfig = new AppConsumerConfig();
            _configuration.Bind("PromotionCalculatorConsumerConfig", _baseConfig);
        }

        private void StartConsumerThread(int idx)
        {
            Thread thread = new Thread(async () =>
            {
                try
                {
                    using (IConsumer<string, OrderModel> consumer
                        = new ConsumerBuilder<string, OrderModel>(_baseConfig)
                            .SetValueDeserializer(new SimpleJsonSerdes<OrderModel>())
                            .Build())
                    {
                        consumer.Subscribe(EventConstants.Events.NewOrder);

                        bool cancelled = false;

                        while (!cancelled)
                        {
                            ConsumeResult<string, OrderModel> message = consumer.Consume(default(CancellationToken));

                            await _log.LogAsync($"Consumer {idx} begins handle message {message.Message.Timestamp.UtcDateTime}");

                            using (IServiceScope scope = _serviceProvider.CreateScope())
                            {
                                await HandleNewOrderAsync(
                                    Guid.Parse(message.Message.Key),
                                    message.Message.Value);
                            }

                            try
                            {
                                consumer.Commit();
                            }
                            catch (Exception ex)
                            {
                                await _log.LogAsync(ex.Message);
                            }
                        }

                        consumer.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
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

            for (int i = 0; i < _baseConfig.ConsumerCount; i++)
            {
                StartConsumerThread(i);
            }
        }
    }
}
