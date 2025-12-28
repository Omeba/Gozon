namespace PaymentService
{
    public class InboxMessage
    {
        public Guid Id { get; set; }
        public string MessageId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string EventData { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
        public DateTime CreatedAt { get; set; } 
    }
}