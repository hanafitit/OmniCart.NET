namespace OmniCart.Domain.Entities;

/// <summary>
/// Сущность пользователя Telegram-бота
/// </summary>
public class User
{
    /// <summary>ID пользователя в БД</summary>
    public int Id { get; set; }

    /// <summary>Telegram User ID</summary>
    public long TelegramUserId { get; set; }

    /// <summary>Username в Telegram</summary>
    public required string Username { get; set; }

    /// <summary>First Name из Telegram</summary>
    public required string FirstName { get; set; }

    /// <summary>Last Name из Telegram (опционально)</summary>
    public string? LastName { get; set; }

    /// <summary>Текущий шаг FSM</summary>
    public int CurrentStep { get; set; } = (int)UserStep.MainPage;

    /// <summary>Адрес доставки</summary>
    public string? DeliveryAddress { get; set; }

    /// <summary>Номер телефона</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Дата создания</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Дата обновления</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Активен ли пользователь</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// FSM состояния пользователя
/// </summary>
public enum UserStep
{
    MainPage = 0,
    EnteringDeliveryAddress = 1,
    EnteringPayment = 2,
    BrowsingCatalog = 3
}