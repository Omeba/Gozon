using Messages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OrderService
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly OrderContext _context;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(OrderContext context, ILogger<OrdersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (!Request.Headers.TryGetValue("User-Id", out var userIdHeader) ||
                !Guid.TryParse(userIdHeader, out var userId))
            {
                return BadRequest("Valid User-Id header is required");
            }

            var executionStrategy = _context.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // 1. Создаем заказ (NEW статус)
                    var order = new Order
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Amount = request.Amount,
                        Description = request.Description,
                        Status = OrderStatus.NEW,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Orders.AddAsync(order);

                    // 2. Создаем сообщение для outbox
                    var orderEvent = new OrderCreatedEvent
                    {
                        OrderId = order.Id,
                        UserId = order.UserId,
                        Amount = order.Amount,
                        CreatedAt = order.CreatedAt
                    };

                    var outboxMessage = new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        EventType = "OrderCreated",
                        EventData = JsonSerializer.Serialize(orderEvent),
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.OutboxMessages.AddAsync(outboxMessage);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Order {OrderId} created for user {UserId}", order.Id, userId);

                    return Ok(new
                    {
                        order.Id,
                        order.UserId,
                        order.Amount,
                        order.Description,
                        order.Status,
                        order.CreatedAt
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creating order for user {UserId}", userId);
                    return StatusCode(500, "Internal server error");
                }
            });
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "Healthy", service = "OrderService", timestamp = DateTime.UtcNow });
        }

        [HttpGet]
        public async Task<IActionResult> GetOrders()
        {
            if (!Request.Headers.TryGetValue("User-Id", out var userIdHeader) ||
                !Guid.TryParse(userIdHeader, out var userId))
            {
                return BadRequest("Valid User-Id header is required");
            }

            try
            {
                var orders = await _context.Orders
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new
                    {
                        o.Id,
                        o.UserId,
                        o.Amount,
                        o.Description,
                        o.Status,
                        o.CreatedAt,
                        o.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders for user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrder(Guid id)
        {
            if (!Request.Headers.TryGetValue("User-Id", out var userIdHeader) ||
                !Guid.TryParse(userIdHeader, out var userId))
            {
                return BadRequest("Valid User-Id header is required");
            }

            try
            {
                var order = await _context.Orders
                    .Where(o => o.Id == id && o.UserId == userId)
                    .Select(o => new
                    {
                        o.Id,
                        o.UserId,
                        o.Amount,
                        o.Description,
                        o.Status,
                        o.CreatedAt,
                        o.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    return NotFound();
                }

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId} for user {UserId}", id, userId);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}