using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Messages;
using System.Text.Json;

namespace PaymentService
{
    public class PaymentOutboxProcessor : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PaymentOutboxProcessor> _logger;
        private readonly IBus _bus;

        public PaymentOutboxProcessor(
            IServiceScopeFactory scopeFactory,
            ILogger<PaymentOutboxProcessor> logger,
            IBus bus)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _bus = bus;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Payment Outbox Processor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<PaymentContext>();

                    var messages = await context.OutboxMessages
                        .Where(m => m.ProcessedAt == null)
                        .OrderBy(m => m.CreatedAt)
                        .Take(10)
                        .ToListAsync(stoppingToken);

                    foreach (var message in messages)
                    {
                        try
                        {
                            if (message.EventType == "PaymentProcessed")
                            {
                                var paymentEvent = JsonSerializer.Deserialize<PaymentProcessedEvent>(message.EventData);
                                if (paymentEvent != null)
                                {
                                    await PublishWithRetryAsync(paymentEvent, stoppingToken);
                                    _logger.LogInformation(
                                        "Published PaymentProcessedEvent for order {OrderId}",
                                        paymentEvent.OrderId);
                                }
                            }

                            message.ProcessedAt = DateTime.UtcNow;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error processing outbox message {message.Id}");
                            continue;
                        }
                    }

                    if (messages.Any())
                    {
                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PaymentOutboxProcessor");
                }

                await Task.Delay(5000, stoppingToken);
            }
        }

        private async Task PublishWithRetryAsync(PaymentProcessedEvent paymentEvent, CancellationToken stoppingToken)
        {
            int maxRetries = 5;
            int retryDelay = 2000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await _bus.Publish(paymentEvent, stoppingToken);
                    return;
                }
                catch (Exception ex) when (IsConnectionException(ex) && attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "Failed to publish message (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms...",
                        attempt, maxRetries, retryDelay);
                    await Task.Delay(retryDelay, stoppingToken);
                }
            }
            await _bus.Publish(paymentEvent, stoppingToken);
        }

        private bool IsConnectionException(Exception ex)
        {
            return ex is RabbitMQ.Client.Exceptions.BrokerUnreachableException ||
                   ex is RabbitMQ.Client.Exceptions.ConnectFailureException ||
                   (ex.InnerException?.GetType() == typeof(System.Net.Sockets.SocketException));
        }
    }
}