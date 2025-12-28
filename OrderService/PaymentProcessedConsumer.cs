using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace OrderService
{
    public class PaymentProcessedConsumer : IConsumer<Messages.PaymentProcessedEvent>
    {
        private readonly OrderContext _context;
        private readonly ILogger<PaymentProcessedConsumer> _logger;

        public PaymentProcessedConsumer(OrderContext context, ILogger<PaymentProcessedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Messages.PaymentProcessedEvent> context)
        {
            _logger.LogInformation("Received PaymentProcessedEvent for order {OrderId}, success: {Success}",
                context.Message.OrderId, context.Message.Success);

            // Используем стратегию выполнения для обработки транзакций с повторными попытками
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var order = await _context.Orders
                        .FirstOrDefaultAsync(o => o.Id == context.Message.OrderId);

                    if (order == null)
                    {
                        _logger.LogWarning("Order {OrderId} not found", context.Message.OrderId);
                        await transaction.RollbackAsync();
                        return;
                    }

                    if (order.Status == OrderStatus.NEW)
                    {
                        order.Status = context.Message.Success
                            ? OrderStatus.FINISHED
                            : OrderStatus.CANCELLED;

                        order.UpdatedAt = DateTime.UtcNow;

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation(
                            "Order {OrderId} status updated to {Status} (Success: {Success})",
                            order.Id, order.Status, context.Message.Success);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Order {OrderId} already has status {Status}, skipping update",
                            order.Id, order.Status);
                        await transaction.RollbackAsync();
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error processing PaymentProcessedEvent for order {OrderId}",
                        context.Message.OrderId);
                    throw;
                }
            });
        }
    }
}