namespace PaymentService
{
    public class OutboxMessage
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string EventData { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
