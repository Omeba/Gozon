using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PaymentService
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountsController : ControllerBase
    {
        private readonly PaymentContext _context;
        private readonly ILogger<AccountsController> _logger;

        public AccountsController(PaymentContext context, ILogger<AccountsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAccount()
        {
            if (!Request.Headers.TryGetValue("User-Id", out var userIdHeader) ||
                !Guid.TryParse(userIdHeader, out var userId))
            {
                return BadRequest("Valid User-Id header is required");
            }

            try
            {
                // Проверяем, нет ли уже счета у пользователя
                var existingAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.UserId == userId);

                if (existingAccount != null)
                {
                    return Conflict("Account already exists for this user");
                }

                var account = new Account
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Balance = 0,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Accounts.AddAsync(account);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Account created for user {UserId}", userId);

                return Ok(new
                {
                    account.Id,
                    account.UserId,
                    account.Balance,
                    account.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account for user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "Healthy", service = "PaymentService", timestamp = DateTime.UtcNow });
        }

        [HttpPost("{userId}/top-up")]
        public async Task<IActionResult> TopUpAccount(Guid userId, [FromBody] TopUpRequest request)
        {
            if (request.Amount <= 0)
            {
                return BadRequest("Amount must be positive");
            }

            // Используем стратегию выполнения для обработки транзакций с повторными попытками
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var account = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.UserId == userId);

                    if (account == null)
                    {
                        return NotFound("Account not found");
                    }

                    // Пополнение баланса
                    account.Balance += request.Amount;
                    account.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Account {UserId} topped up by {Amount}", userId, request.Amount);

                    return Ok(new
                    {
                        NewBalance = account.Balance
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error topping up account for user {UserId}", userId);
                    return StatusCode(500, "Internal server error");
                }
            });
        }

        [HttpGet("{userId}/balance")]
        public async Task<IActionResult> GetBalance(Guid userId)
        {
            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.UserId == userId);

                if (account == null)
                {
                    return NotFound("Account not found");
                }

                return Ok(new
                {
                    Balance = account.Balance
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting balance for user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAccounts()
        {
            try
            {
                var accounts = await _context.Accounts
                    .Select(a => new
                    {
                        a.Id,
                        a.UserId,
                        a.Balance,
                        a.CreatedAt,
                        a.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(accounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all accounts");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
