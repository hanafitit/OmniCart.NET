namespace OmniCart.Domain.Entities;

public class CartItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required User User { get; set; }
    public int ProductId { get; set; }
    public required Product Product { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; }
}