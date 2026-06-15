using System.Threading.Channels;
using OmniCart.Domain.Entities;
using OmniCart.Infrastructure.Telegram;
using Microsoft.AspNetCore.SignalR.Client;
using Telegram.Bot;
using Microsoft.Extensions.Options;

namespace OmniCart.TelegramBot.Workers;

public class OrderNotificationWorker : BackgroundService, IOrderNotificationService
{
    private readonly Channel<Order> _orderChannel;
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramBotSettings _settings;
    private readonly ILogger<OrderNotificationWorker> _logger;
    private readonly string _hubUrl;
    private HubConnection? _hubConnection;

    public OrderNotificationWorker(
        ITelegramBotClient botClient,
        IOptions<TelegramBotSettings> settings,
        IConfiguration configuration,
        ILogger<OrderNotificationWorker> logger)
    {
        _orderChannel = Channel.CreateUnbounded<Order>();
        _botClient = botClient;
        _settings = settings.Value;
        _logger = logger;

        // В докере или локально URL может отличаться.
        // По умолчанию берем из конфигурации или строим стандартный.
        _hubUrl = configuration["SignalR:HubUrl"] ?? "http://web:8080/orderhub";
    }

    public void NotifyNewOrder(Order order)
    {
        _orderChannel.Writer.TryWrite(order);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderNotificationWorker is starting.");

        await ConnectWithRetryAsync(stoppingToken);

        await foreach (var order in _orderChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await SendTelegramNotification(order, stoppingToken);
                await SendSignalRNotification(order, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order notification for order {OrderId}", order.Id);
            }
        }
    }

    private async Task SendTelegramNotification(Order order, CancellationToken ct)
    {
        if (_settings.OwnerChatId == 0)
        {
            _logger.LogWarning("OwnerChatId is not set. Telegram notification skipped.");
            return;
        }

        var message = $"🆕 <b>Новый заказ #{order.Id}</b>\n\n" +
                      $"👤 Клиент: {HtmlEscape(order.User?.FirstName)} {HtmlEscape(order.User?.LastName)}\n" +
                      $"💰 Сумма: {order.TotalPrice} руб.\n" +
                      $"📍 Адрес: {HtmlEscape(order.User?.DeliveryAddress)}";

        await _botClient.SendMessage(
            chatId: _settings.OwnerChatId,
            text: message,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            cancellationToken: ct);

        _logger.LogInformation("Telegram notification sent for order {OrderId}", order.Id);
    }

    private static string HtmlEscape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return System.Net.WebUtility.HtmlEncode(text);
    }

    private async Task SendSignalRNotification(Order order, CancellationToken ct)
    {
        try
        {
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
            {
                await ConnectWithRetryAsync(ct);
            }

            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SendOrderNotification",
                    order.Id,
                    order.TotalPrice,
                    $"{order.User?.FirstName} {order.User?.LastName}",
                    cancellationToken: ct);

                _logger.LogInformation("SignalR notification sent for order {OrderId}", order.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR notification");
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        if (_hubConnection == null)
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_hubUrl)
                .WithAutomaticReconnect()
                .Build();
        }

        while (true)
        {
            try
            {
                await _hubConnection.StartAsync(ct);
                _logger.LogInformation("Connected to SignalR Hub at {HubUrl}", _hubUrl);
                return;
            }
            catch (Exception ex) when (ct.IsCancellationRequested == false)
            {
                _logger.LogWarning("Failed to connect to SignalR Hub. Retrying in 5s... Error: {Message}", ex.Message);
                await Task.Delay(5000, ct);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
