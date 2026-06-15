namespace OmniCart.Infrastructure.Telegram;

/// <summary>
/// Настройки Telegram-бота
/// </summary>
public class TelegramBotSettings
{
    /// <summary>API токен бота</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Таймаут HTTP запросов (сек)</summary>
    public int HttpClientTimeout { get; set; } = 30;

    /// <summary>Интервал опроса (сек)</summary>
    public int PollingIntervalSeconds { get; set; } = 1;

    /// <summary>ChatId владельца для получения уведомлений</summary>
    public long OwnerChatId { get; set; }
}
