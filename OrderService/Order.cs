
namespace OrderService
{
    public class Order
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public OrderStatus Status { get; set; } = OrderStatus.NEW;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public enum OrderStatus
    {
        NEW,
        FINISHED,
        CANCELLED
    }
}
