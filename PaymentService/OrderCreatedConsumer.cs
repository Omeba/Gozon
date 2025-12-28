using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace PaymentService
{
    public class OrderCreatedConsumer : IConsumer<Messages.OrderCreatedEvent>
    {
        private readonly PaymentContext _context;
        private readonly ILogger<OrderCreatedConsumer> _logger;

        public OrderCreatedConsumer(PaymentContext context, ILogger<OrderCreatedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Messages.OrderCreatedEvent> context)
        {
            var messageId = context.MessageId?.ToString();

            if (string.IsNullOrEmpty(messageId))
            {
                _logger.LogWarning("MessageId is null or empty");
                return;
            }

            _logger.LogInformation("Received OrderCreatedEvent for order {OrderId}, user {UserId}, amount {Amount}",
                context.Message.OrderId, context.Message.UserId, context.Message.Amount);

            // Используем стратегию выполнения для обработки транзакций с повторными попытками
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Transactional Inbox часть 1: Проверяем идемпотентность
                    var existingMessage = await _context.InboxMessages
                        .FirstOrDefaultAsync(m => m.MessageId == messageId);

                    if (existingMessage != null)
                    {
                        _logger.LogInformation("Message {MessageId} already processed, skipping", messageId);
                        await transaction.RollbackAsync();
                        return;
                    }

                    // Сохраняем в Inbox
                    var inboxMessage = new InboxMessage
                    {
                        Id = Guid.NewGuid(),
                        MessageId = messageId,
                        EventType = "OrderCreated",
                        EventData = JsonSerializer.Serialize(context.Message),
                        ProcessedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.InboxMessages.AddAsync(inboxMessage);
                    await _context.SaveChangesAsync();

                    // Transactional Inbox часть 2: Выполняем задачу
                    var account = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.UserId == context.Message.UserId);

                    bool success = false;
                    string? reason = null;

                    if (account == null)
                    {
                        reason = "Account not found";
                        _logger.LogWarning("Account not found for user {UserId}", context.Message.UserId);
                    }
                    else if (account.Balance < context.Message.Amount)
                    {
                        reason = "Insufficient funds";
                        _logger.LogWarning("Insufficient balance for user {UserId}. Balance: {Balance}, Required: {Amount}",
                            context.Message.UserId, account.Balance, context.Message.Amount);
                    }
                    else
                    {
                        var updatedRows = await _context.Accounts
                            .Where(a => a.Id == account.Id && a.Balance >= context.Message.Amount)
                            .ExecuteUpdateAsync(setters => setters
                                .SetProperty(a => a.Balance, a => a.Balance - context.Message.Amount)
                                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));

                        success = updatedRows > 0;

                        if (success)
                        {
                            _logger.LogInformation("Payment successful for order {OrderId}. New balance: {Balance}",
                                context.Message.OrderId, account.Balance - context.Message.Amount);
                        }
                        else
                        {
                            reason = "Concurrent modification detected";
                            _logger.LogWarning("Concurrent modification for account {AccountId}", account.Id);
                        }
                    }

                    // Transactional Outbox часть 1: Создаем задачу на отправку результата
                    var paymentEvent = new Messages.PaymentProcessedEvent
                    {
                        OrderId = context.Message.OrderId,
                        UserId = context.Message.UserId,
                        Success = success,
                        Reason = reason
                    };

                    var outboxMessage = new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        EventType = "PaymentProcessed",
                        EventData = JsonSerializer.Serialize(paymentEvent),
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.OutboxMessages.AddAsync(outboxMessage);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Order {OrderId} payment processed. Success: {Success}, Reason: {Reason}",
                        context.Message.OrderId, success, reason ?? "None");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error processing OrderCreatedEvent for order {OrderId}",
                        context.Message.OrderId);
                    throw;
                }
            });
        }
    }
}