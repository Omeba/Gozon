using MassTransit;
using Microsoft.EntityFrameworkCore;
using Messages;
using System.Text.Json;

namespace OrderService
{
    public class OutboxProcessorService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxProcessorService> _logger;
        private readonly IBus _bus;

        public OutboxProcessorService(IServiceScopeFactory scopeFactory,
                                      ILogger<OutboxProcessorService> logger,
                                      IBus bus)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _bus = bus;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<OrderContext>();

                    var messages = await context.OutboxMessages
                        .Where(m => m.ProcessedAt == null)
                        .OrderBy(m => m.CreatedAt)
                        .Take(100)
                        .ToListAsync(stoppingToken);

                    foreach (var message in messages)
                    {
                        try
                        {
                            if (message.EventType == "OrderCreated")
                            {
                                var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message.EventData);
                                await _bus.Publish(orderEvent, stoppingToken);
                            }

                            message.ProcessedAt = DateTime.UtcNow;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error processing outbox message {message.Id}");
                        }
                    }

                    if (messages.Any())
                        await context.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OutboxProcessorService");
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
