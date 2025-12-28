namespace Messages
{
    public class PaymentProcessedEvent
    {
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public bool Success { get; set; }
        public string? Reason { get; set; }
    }
}
