using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Messages;
using System.Text.Json;
using static MassTransit.Monitoring.Performance.BuiltInCounters;

namespace OrderService
{
    public class OutboxProcessorMassTransit : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessorMassTransit> _logger;

        public OutboxProcessorMassTransit(IServiceProvider serviceProvider, ILogger<OutboxProcessorMassTransit> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxProcessorMassTransit started.");

            // Даем время MassTransit подключиться к RabbitMQ
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<OrderContext>();
                    var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                    var messages = await context.OutboxMessages
                        .Where(m => m.ProcessedAt == null)
                        .OrderBy(m => m.CreatedAt)
                        .Take(10)
                        .ToListAsync(stoppingToken);

                    _logger.LogInformation("Found {Count} unprocessed outbox messages", messages.Count);

                    foreach (var message in messages)
                    {
                        try
                        {
                            // Десериализуем событие
                            var orderCreatedEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(
                                message.EventData,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            if (orderCreatedEvent != null)
                            {
                                _logger.LogInformation("Publishing OrderCreatedEvent for order {OrderId}", orderCreatedEvent.OrderId);

                                // Публикуем через MassTransit
                                await publishEndpoint.Publish(orderCreatedEvent, stoppingToken);

                                message.ProcessedAt = DateTime.UtcNow;
                                await context.SaveChangesAsync(stoppingToken);

                                _logger.LogInformation("Successfully published OrderCreatedEvent for order {OrderId}",
                                    orderCreatedEvent.OrderId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error publishing message {MessageId}", message.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OutboxProcessorMassTransit");
                }

                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}