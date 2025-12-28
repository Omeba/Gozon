using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;

namespace ApiGateway
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProxyController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ProxyController> _logger;

        public ProxyController(IHttpClientFactory httpClientFactory, ILogger<ProxyController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpPost("orders")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, [FromHeader(Name = "User-Id")] Guid? userId = null)
        {
            if (!userId.HasValue)
            {
                return BadRequest("User-Id header is required");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("OrderService");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/orders");
                httpRequest.Headers.Add("User-Id", userId.ToString());
                httpRequest.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, content);
                }
                else
                {
                    return StatusCode((int)response.StatusCode, content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying to OrderService");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromHeader(Name = "User-Id")] Guid? userId = null)
        {
            if (!userId.HasValue)
            {
                return BadRequest("User-Id header is required");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("OrderService");

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
                httpRequest.Headers.Add("User-Id", userId.ToString());

                var response = await client.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, content);
                }
                else
                {
                    return StatusCode((int)response.StatusCode, content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying to OrdersService");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrder(Guid id, [FromHeader(Name = "User-Id")] Guid? userId = null)
        {
            if (!userId.HasValue)
            {
                return BadRequest("User-Id header is required");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("OrderService");
                var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{id}");
                httpRequest.Headers.Add("User-Id", userId.ToString());

                var response = await client.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying to OrderService");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("accounts")]
        public async Task<IActionResult> CreateAccount([FromHeader(Name = "User-Id")] Guid? userId = null)
        {
            if (!userId.HasValue)
            {
                return BadRequest("User-Id header is required");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("PaymentService");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/accounts");
                httpRequest.Headers.Add("User-Id", userId.ToString());

                var response = await client.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, content);
                }
                else
                {
                    return StatusCode((int)response.StatusCode, content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying to PaymentService");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("accounts/{userId}/top-up")]
        public async Task<IActionResult> TopUpAccount(Guid userId, [FromBody] TopUpRequest request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PaymentService");

                var httpRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"/api/accounts/{userId}/top-up"
                );

                httpRequest.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, content);
                }
                else
                {
                    return StatusCode((int)response.StatusCode, content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying to PaymentService");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("accounts/{userId}/balance")]
        public async Task<IActionResult> GetBalance(Guid userId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PaymentService");

                var httpRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"/api/accounts/{userId}/balance"
                );

                var response = await client.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, content);
                }
                else
                {
                    return StatusCode((int)response.StatusCode, content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying to PaymentService");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("orders/health")]
        public async Task<IActionResult> GetOrderHealth()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("OrderService");
                var response = await client.GetAsync("/api/orders/health");
                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying to OrderService health check");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("accounts/health")]
        public async Task<IActionResult> GetAccountHealth()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PaymentService");
                var response = await client.GetAsync("/api/accounts/health");
                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying to PaymentService health check");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("accounts")]
        public async Task<IActionResult> GetAllAccounts()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PaymentService");
                var response = await client.GetAsync("/api/accounts");
                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying to PaymentService");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
