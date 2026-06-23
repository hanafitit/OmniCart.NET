using OmniCart.Infrastructure;
using OmniCart.Infrastructure.Telegram;
using OmniCart.TelegramBot.Workers;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using OmniCart.Domain.Entities;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Конфигурация
        var telegramSettings = context.Configuration
            .GetSection("TelegramBotSettings")
            .Get<TelegramBotSettings>() 
            ?? throw new InvalidOperationException("TelegramBotSettings не найдены");

        // Database
        var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionString не найдена");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });
        });

            // Telegram Bot
            services.Configure<TelegramBotSettings>(
            context.Configuration.GetSection("TelegramBotSettings"));

            services.AddSingleton<ITelegramBotClient>(
            new TelegramBotClient(telegramSettings.Token));

            services.AddSingleton<UpdateHandler>();
            services.AddHostedService<TelegramBotWorker>();

            // Logging
            services.AddLogging();
    });

var host = builder.Build();

// Миграции БД
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        
        var productCount = await db.Products.CountAsync();
        Console.WriteLine($"✅ Миграции БД применены. Товаров в базе: {productCount}");
        
        if (productCount == 0)
        {
            Console.WriteLine("⚠️ Внимание: Таблица товаров пуста! Возможно, нужно переприменить миграции.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Ошибка миграций: {ex.Message}");
        throw;
    }
}

Console.WriteLine("🚀 TelegramBot запускается...");
await host.RunAsync();
