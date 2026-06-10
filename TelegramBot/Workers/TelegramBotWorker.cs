using OmniCart.Infrastructure.Telegram;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace OmniCart.TelegramBot.Workers;

/// <summary>
/// Background Service для управления Telegram-ботом
/// </summary>
public class TelegramBotWorker : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly UpdateHandler _updateHandler;
    private readonly ILogger<TelegramBotWorker> _logger;

    public TelegramBotWorker(
        ITelegramBotClient botClient,
        UpdateHandler updateHandler,
        ILogger<TelegramBotWorker> logger)
    {
        _botClient = botClient;
        _updateHandler = updateHandler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var me = await _botClient.GetMe(cancellationToken: stoppingToken);
            _logger.LogInformation("✅ Telegram-бот запущен: @{BotUsername}", me.Username);

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] 
                { 
                    UpdateType.Message,
                    UpdateType.CallbackQuery,
                    UpdateType.EditedMessage
                }
            };

            await _botClient.ReceiveAsync(
                updateHandler: (botClient, update, ct) =>
                    _updateHandler.HandleUpdateAsync(update, ct),
                errorHandler: (botClient, exception, ct) =>
                    HandleErrorAsync(exception, ct),
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("🛑 Telegram-бот остановлен");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка в TelegramBotWorker");
            throw;
        }
    }

    private Task HandleErrorAsync(Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "❌ Ошибка при получении update'ов");
        return Task.CompletedTask;
    }
}
