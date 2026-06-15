namespace OmniCart.Domain.Entities;

/// <summary>
/// Сохраненный адрес пользователя
/// </summary>
public class UserAddress
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public required User User { get; set; }

    /// <summary>
    /// Полный адрес доставки
    /// </summary>
    public required string AddressLine { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
