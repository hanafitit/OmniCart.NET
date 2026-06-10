namespace OmniCart.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required User User { get; set; }
    public decimal TotalPrice { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = [];
}