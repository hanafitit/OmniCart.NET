using OmniCart.Domain.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using DomainUser = OmniCart.Domain.Entities.User;

namespace OmniCart.Infrastructure.Telegram;

public class UpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly GoogleSheetsService _googleSheetsService;
    private readonly IConfiguration _configuration;

    public UpdateHandler(
        ITelegramBotClient botClient,
        IServiceProvider serviceProvider,
        ILogger<UpdateHandler> logger,
        GoogleSheetsService googleSheetsService,
        IConfiguration configuration)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _googleSheetsService = googleSheetsService;
        _configuration = configuration;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (update.Message is { } message)
            {
                var chatId = message.Chat.Id;
                var telegramUserId = message.From?.Id ?? 0;

                var user = await GetOrCreateUser(
                    db,
                    telegramUserId,
                    message.From, 
                    ct);

                if (message.Text is { } text)
                {
                    switch (text)
                    {
                        case "/start":
                            await HandleStartCommandAsync(db, user, chatId, ct);
                            return;

                        case "🛍️ Каталог":
                            await HandleCatalogAsync(db, user, chatId, ct);
                            return;

                        case "🛒 Корзина":
                            await HandleCartAsync(db, user, chatId, ct);
                            return;

                        case "📋 Мои заказы":
                            await HandleMyOrdersAsync(db, user, chatId, ct);
                            return;

                        case "👤 Профиль":
                            await HandleProfileAsync(user, chatId, ct);
                            return;

                        case "⚙️ Настройки":
                            await HandleSettingsAsync(user, chatId, ct);
                            return;

                        default:
                            await HandleByCurrentStepAsync(db, user, chatId, text, ct);
                            return;
                    }
                }
                else
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "⚠️ Извините, бот пока поддерживает только текстовые сообщения.",
                        cancellationToken: ct);
                    return;
                }
            }

            if (update.CallbackQuery is { } query)
            {
                if (query.Message == null)
                {
                    await _botClient.AnswerCallbackQuery(
                        callbackQueryId: query.Id,
                        cancellationToken: ct);
                    return;
                }

                var chatId = query.Message.Chat.Id;
                var telegramUserId = query.From?.Id ?? 0;

                var user = await GetOrCreateUser(
                    db,
                    telegramUserId,
                    query.From,
                    ct);

                if (!string.IsNullOrEmpty(query.Data))
                {
                    await HandleCallbackAsync(db, user, chatId, query.Data, ct);
                }

                await _botClient.AnswerCallbackQuery(
                    callbackQueryId: query.Id,
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка в UpdateHandler: {Message}", ex.Message);
        }
    }

    private async Task HandleStartCommandAsync(AppDbContext db, DomainUser user, long chatId, CancellationToken ct)
    {
        user.CurrentStep = (int)UserStep.MainPage;
        user.UpdatedAt = DateTime.UtcNow;
        user.IsActive = true;
        await db.SaveChangesAsync(ct);

        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("🛍️ Каталог"), new KeyboardButton("🛒 Корзина") },
            new[] { new KeyboardButton("📋 Мои заказы"), new KeyboardButton("👤 Профиль") },
            new[] { new KeyboardButton("⚙️ Настройки") }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false,
            IsPersistent = true
        };

        var greeting = $"👋 Привет, {(user.FirstName ?? "гость")}!\n\n" +
                      "Добро пожаловать в OmniCart! 🛒\n\n" +
                      "Выбери действие:";

        await _botClient.SendMessage(
            chatId: chatId,
            text: greeting,
            replyMarkup: keyboard,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: ct);

        _logger.LogInformation("✅ Пользователь {TelegramUserId} начал работу", user.TelegramUserId);
    }

    private async Task HandleCatalogAsync(AppDbContext db, DomainUser user, long chatId, CancellationToken ct)
    {
        user.CurrentStep = (int)UserStep.BrowsingCatalog;
        await db.SaveChangesAsync(ct);

        await SendCatalogPageAsync(db, user, chatId, page: 0, ct);
    }

    private async Task SendCatalogPageAsync(
        AppDbContext db,
        DomainUser user,
        long chatId,
        int page,
        CancellationToken ct)
    {
        const int pageSize = 5;

        var totalCount = await db.Products.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        page = Math.Clamp(page, 0, Math.Max(0, totalPages - 1));

        var products = await db.Products
            .OrderBy(p => p.Id)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (products.Count == 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "📚 В каталоге пока нет товаров.",
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
            return;
        }

        foreach (var product in products)
        {
            var text = FormatProductText(product);

            var addToCartCallback = $"addtocart_{product.Id}";
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("🛒 Добавить в корзину", addToCartCallback)
            });

            await _botClient.SendMessage(
                chatId: chatId,
                text: text,
                replyMarkup: inlineKeyboard,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
        }

        if (totalPages > 1)
        {
            var navButtons = new List<InlineKeyboardButton>();

            if (page > 0)
                navButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"catalog_{page - 1}"));

            navButtons.Add(InlineKeyboardButton.WithCallbackData($"📄 {page + 1}/{totalPages}", "noop"));

            if (page < totalPages - 1)
                navButtons.Add(InlineKeyboardButton.WithCallbackData("Вперёд ➡️", $"catalog_{page + 1}"));

            var navKeyboard = new InlineKeyboardMarkup(navButtons.ToArray());

            await _botClient.SendMessage(
                chatId: chatId,
                text: "Навигация по каталогу:",
                replyMarkup: navKeyboard,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
        }
    }

    private static string FormatProductText(Product product)
    {
        var stockInfo = product.Stock > 0
            ? $"В наличии: {product.Stock} шт."
            : "❌ Нет в наличии";

        return $"{EscapeMarkdown(product.Name)}\n" +
               $"{EscapeMarkdown(product.Description)}\n\n" +
               $"Цена: {product.Price} руб.\n" +
               $"{stockInfo}";
    }

    private static string EscapeMarkdown(string text)
    {
        return text
            .Replace("_", "\\_")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("~", "\\~")
            .Replace("`", "\\`")
            .Replace(">", "\\>")
            .Replace("#", "\\#")
            .Replace("+", "\\+")
            .Replace("-", "\\-")
            .Replace("=", "\\=")
            .Replace("|", "\\|")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace(".", "\\.")
            .Replace("!", "\\!");
    }

   private async Task HandleProfileAsync(DomainUser user, long chatId, CancellationToken ct)
    {
        var text = $"👤 Профиль\n\n" +
                   $"Имя: {user.FirstName} {user.LastName ?? ""}\n" +
                   $"Username: @{user.Username}\n" +
                   $"Telegram ID: {user.TelegramUserId}";

        await _botClient.SendMessage(
            chatId: chatId,
            text: text,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: ct);
    }

    private async Task HandleSettingsAsync(DomainUser user, long chatId, CancellationToken ct)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: "⚙️ Настройки\n\n(Заглушка)",
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: ct);
    }

    private async Task HandleMyOrdersAsync(AppDbContext db, DomainUser user, long chatId, CancellationToken ct)
    {
        var orders = await db.Orders
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (orders.Count == 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "📋 У вас пока нет заказов.",
                cancellationToken: ct);
            return;
        }

        var lines = new List<string> { "📋 Ваши последние заказы:" };
        foreach (var order in orders)
        {
            lines.Add($"📦 Заказ #{order.Id} от {order.CreatedAt:dd.MM.yyyy HH:mm}");
            lines.Add($"Статус: {order.Status}");
            lines.Add($"Сумма: {order.TotalPrice} руб.");
            lines.Add("");
        }

        await _botClient.SendMessage(
            chatId: chatId,
            text: string.Join("\n", lines),
            cancellationToken: ct);
    }

    private async Task HandleCartAsync(AppDbContext db, DomainUser user, long chatId, CancellationToken ct)
    {
        var cartItems = await db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.UserId == user.Id)
            .ToListAsync(ct);

        if (cartItems.Count == 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "🛒 Ваша корзина пуста.",
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
            return;
        }

        var lines = new List<string> { "🛒 Ваша корзина:" };
        decimal total = 0;

        foreach (var item in cartItems)
        {
            var sum = item.Product.Price * item.Quantity;
            total += sum;
            lines.Add($"{EscapeMarkdown(item.Product.Name)} — {item.Quantity} шт. = {sum} руб.");
        }

        lines.Add($"Итого: {total} руб.");

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("✅ Оформить заказ", "checkout")
        });

        await _botClient.SendMessage(
            chatId: chatId,
            text: string.Join("\n", lines),
            replyMarkup: inlineKeyboard,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: ct);
    }

    private async Task HandleCallbackAsync(
        AppDbContext db,
        DomainUser user,
        long chatId,
        string callbackData,
        CancellationToken ct)
    {
        if (callbackData.StartsWith("addtocart_", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(callbackData["addtocart_".Length..], out var productId))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Неверный товар.",
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: ct);
                return;
            }

            await HandleAddToCartAsync(db, user, chatId, productId, ct);
            return;
        }

        if (callbackData.StartsWith("catalog_", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(callbackData["catalog_".Length..], out var page))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Неверная страница.",
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: ct);
                return;
            }

            await SendCatalogPageAsync(db, user, chatId, page, ct);
            return;
        }

        if (callbackData == "checkout")
        {
            var addresses = await db.UserAddresses
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(ct);

            if (addresses.Count == 0)
            {
                user.CurrentStep = (int)UserStep.EnteringDeliveryAddress;
                await db.SaveChangesAsync(ct);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "📍 Введите адрес доставки:",
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: ct);
            }
            else
            {
                user.CurrentStep = (int)UserStep.SelectingDeliveryAddress;
                await db.SaveChangesAsync(ct);

                var buttons = addresses.Select(a =>
                    new[] { InlineKeyboardButton.WithCallbackData(a.AddressLine, $"select_address_{a.Id}") })
                    .ToList();

                buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить новый адрес", "add_new_address") });

                var keyboard = new InlineKeyboardMarkup(buttons);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "📍 Выберите адрес доставки или введите новый:",
                    replyMarkup: keyboard,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: ct);
            }
            return;
        }

        if (callbackData == "add_new_address")
        {
            user.CurrentStep = (int)UserStep.EnteringDeliveryAddress;
            await db.SaveChangesAsync(ct);

            await _botClient.SendMessage(
                chatId: chatId,
                text: "📍 Введите новый адрес доставки:",
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
            return;
        }

        if (callbackData.StartsWith("select_address_", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(callbackData["select_address_".Length..], out var addressId))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Неверный адрес.",
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: ct);
                return;
            }

            var address = await db.UserAddresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == user.Id, ct);
            if (address == null)
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Адрес не найден.",
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: ct);
                return;
            }

            await CreateOrderAsync(db, user, chatId, address.AddressLine, ct);
            return;
        }
    }

    private async Task HandleAddToCartAsync(
        AppDbContext db,
        DomainUser user,
        long chatId,
        int productId,
        CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product == null)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "⚠️ Товар не найден.",
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
            return;
        }

        if (product.Stock <= 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: $"{EscapeMarkdown(product.Name)} закончился на складе.",
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
            return;
        }

        var cartItem = await db.CartItems
            .FirstOrDefaultAsync(ci => ci.UserId == user.Id && ci.ProductId == product.Id, ct);

        if (cartItem == null)
        {
            var newItem = new CartItem
            {
                UserId = user.Id,
                ProductId = product.Id,
                Quantity = 1,
                AddedAt = DateTime.UtcNow,
                User = null!,
                Product = null!
            };

            db.CartItems.Add(newItem);
            await db.SaveChangesAsync(ct);

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"✅ {EscapeMarkdown(product.Name)} добавлен в корзину!\n\n" +
                      $"Цена: {product.Price} руб.\n" +
                      $"Количество: 1",
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
        }
        else
        {
            if (cartItem.Quantity >= product.Stock)
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"⚠️ {EscapeMarkdown(product.Name)} больше нельзя добавить. " +
                          $"У вас в корзине: {cartItem.Quantity} шт. Остаток: {product.Stock} шт.",
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: ct);
                return;
            }

            cartItem.Quantity += 1;
            await db.SaveChangesAsync(ct);

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"✅ {EscapeMarkdown(product.Name)} — обновлено в корзине\n\n" +
                      $"Цена: {product.Price} руб.\n" +
                      $"Количество: {cartItem.Quantity}",
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
        }
    }

    private async Task HandleByCurrentStepAsync(
        AppDbContext db,
        DomainUser user,
        long chatId,
        string text,
        CancellationToken ct)
    {
        var step = (UserStep)user.CurrentStep;

        switch (step)
        {
            case UserStep.EnteringDeliveryAddress:
            {
                // Сохраняем новый адрес
                var newAddress = new UserAddress
                {
                    UserId = user.Id,
                    User = null!,
                    AddressLine = text,
                    CreatedAt = DateTime.UtcNow
                };
                db.UserAddresses.Add(newAddress);
                await db.SaveChangesAsync(ct);
                
                await CreateOrderAsync(db, user, chatId, text, ct);
                break;
            }

            default:
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "🤔 Не понял команду. Используй меню ниже.",
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: ct);
                break;
        }
    }

    private async Task CreateOrderAsync(
        AppDbContext db,
        DomainUser user,
        long chatId,
        string address,
        CancellationToken ct)
    {
        var cartItems = await db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.UserId == user.Id)
            .ToListAsync(ct);

        if (cartItems.Count == 0)
        {
            user.CurrentStep = (int)UserStep.MainPage;
            await db.SaveChangesAsync(ct);

            await _botClient.SendMessage(
                chatId: chatId,
                text: "🛒 Ваша корзина пуста. Заказ не создан.",
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
            return;
        }

        var total = cartItems.Sum(ci => ci.Product.Price * ci.Quantity);

        var order = new Order
        {
            UserId = user.Id,
            User = null!,
            TotalPrice = total,
            Status = "Created",
            DeliveryAddress = address,
            CreatedAt = DateTime.UtcNow
        };

        var orderItems = cartItems.Select(ci => new OrderItem
        {
            Order = order,
            ProductId = ci.ProductId,
            Product = null!,
            Quantity = ci.Quantity,
            Price = ci.Product.Price
        }).ToList();

        order.OrderItems = orderItems;

        db.Orders.Add(order);
        db.CartItems.RemoveRange(cartItems);
        await db.SaveChangesAsync(ct);

        // Логирование в Google Sheets
        await _googleSheetsService.AddOrderAsync(order, user, orderItems);

        // Уведомление через SignalR
        await NotifyAdminAboutNewOrderAsync();

        user.CurrentStep = (int)UserStep.MainPage;
        user.DeliveryAddress = address;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await _botClient.SendMessage(
            chatId: chatId,
            text: $"✅ Заказ оформлен!\n\nАдрес: {address}\nСумма: {total} руб.\nСтатус: Created",
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: ct);
    }

    private async Task NotifyAdminAboutNewOrderAsync()
    {
        try
        {
            var hubUrl = _configuration["SignalR:HubUrl"] ?? "http://localhost:8080/orderhub";

            await using var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            await connection.StartAsync();
            await connection.InvokeAsync("SendNewOrderNotification");

            _logger.LogInformation("🔔 Отправлено уведомление о новом заказе в админ-панель");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при отправке уведомления через SignalR: {Message}", ex.Message);
        }
    }

    private async Task<DomainUser> GetOrCreateUser(
        AppDbContext dbContext,
        long telegramUserId,
        global::Telegram.Bot.Types.User? telegramUser,
        CancellationToken ct)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        if (user == null)
        {
            user = new DomainUser
            {
                TelegramUserId = telegramUserId,
                Username = telegramUser?.Username ?? "unknown",
                FirstName = telegramUser?.FirstName ?? "User",
                LastName = telegramUser?.LastName,
                CurrentStep = (int)UserStep.MainPage,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("✨ Создан новый пользователь: {TelegramUserId}", telegramUserId);
        }

        return user;
    }
}
