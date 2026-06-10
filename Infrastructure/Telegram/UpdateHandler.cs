using OmniCart.Domain.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using DomainUser = OmniCart.Domain.Entities.User;

namespace OmniCart.Infrastructure.Telegram;

public class UpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(
        ITelegramBotClient botClient,
        IServiceProvider serviceProvider,
        ILogger<UpdateHandler> logger)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        try
        {
            if (update.Message?.Text is { } text)
            {
                var chatId = update.Message.Chat.Id;
                var telegramUserId = update.Message.From?.Id ?? 0;

                var user = await GetOrCreateUserAsync(
                    telegramUserId,
                    update.Message.From,
                    ct);

                switch (text)
                {
                    case "/start":
                        await HandleStartCommandAsync(user, chatId, ct);
                        return;

                    case "🛍️ Каталог":
                        await HandleCatalogAsync(user, chatId, ct);
                        return;

                    case "🛒 Корзина":
                        await HandleCartAsync(user, chatId, ct);
                        return;

                    case "👤 Профиль":
                        await HandleProfileAsync(user, chatId, ct);
                        return;

                    case "⚙️ Настройки":
                        await HandleSettingsAsync(user, chatId, ct);
                        return;

                    default:
                        await HandleByCurrentStepAsync(user, chatId, text, ct);
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

                var user = await GetOrCreateUserAsync(
                    telegramUserId,
                    query.From,
                    ct);

                if (!string.IsNullOrEmpty(query.Data))
                {
                    await HandleCallbackAsync(user, chatId, query.Data, ct);
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

    private async Task HandleStartCommandAsync(DomainUser user, long chatId, CancellationToken ct)
    {
        user.CurrentStep = (int)UserStep.MainPage;
        user.UpdatedAt = DateTime.UtcNow;
        user.IsActive = true;

        await SaveUserAsync(user, ct);

        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("🛍️ Каталог"), new KeyboardButton("🛒 Корзина") },
            new[] { new KeyboardButton("👤 Профиль"), new KeyboardButton("⚙️ Настройки") }
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

    private async Task HandleCatalogAsync(DomainUser user, long chatId, CancellationToken ct)
    {
        user.CurrentStep = (int)UserStep.BrowsingCatalog;
        await SaveUserAsync(user, ct);

        await SendCatalogPageAsync(user, chatId, page: 0, ct);
    }

    private async Task SendCatalogPageAsync(
        DomainUser user,
        long chatId,
        int page,
        CancellationToken ct)
    {
        const int pageSize = 5;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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

    private async Task HandleCartAsync(DomainUser user, long chatId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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

            await HandleAddToCartAsync(user, chatId, productId, ct);
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

            await SendCatalogPageAsync(user, chatId, page, ct);
            return;
        }

        if (callbackData == "checkout")
        {
            user.CurrentStep = (int)UserStep.EnteringDeliveryAddress;
            await SaveUserAsync(user, ct);

            await _botClient.SendMessage(
                chatId: chatId,
                text: "📍 Введите адрес доставки:",
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
            return;
        }
    }

    private async Task HandleAddToCartAsync(
        DomainUser user,
        long chatId,
        int productId,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cartItems = await db.CartItems
                    .Include(ci => ci.Product)
                    .Where(ci => ci.UserId == user.Id)
                    .ToListAsync(ct);

                if (cartItems.Count == 0)
                {
                    user.CurrentStep = (int)UserStep.MainPage;
                    await SaveUserAsync(user, ct);

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

                user.CurrentStep = (int)UserStep.MainPage;
                user.DeliveryAddress = text;
                user.UpdatedAt = DateTime.UtcNow;
                await SaveUserAsync(user, ct);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"✅ Заказ оформлен!\n\nАдрес: {text}\nСумма: {total} руб.\nСтатус: Created",
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: ct);
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

    private async Task<DomainUser> GetOrCreateUserAsync(
        long telegramUserId,
        global::Telegram.Bot.Types.User? telegramUser,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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

    private async Task SaveUserAsync(DomainUser user, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await dbContext.Users.FindAsync(
            new object[] { user.Id },
            cancellationToken: ct);

        if (existing != null)
        {
            existing.CurrentStep = user.CurrentStep;
            existing.DeliveryAddress = user.DeliveryAddress;
            existing.PhoneNumber = user.PhoneNumber;
            existing.IsActive = user.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(ct);
        }
    }
}
