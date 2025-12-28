namespace ApiGateway
{
    public class CreateOrderRequest
    {
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
