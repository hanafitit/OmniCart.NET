using OmniCart.Infrastructure;
using OmniCart.Infrastructure.Configuration;
using OmniCart.Infrastructure.Services;
using OmniCart.Infrastructure.Telegram;
using OmniCart.TelegramBot.Workers;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

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
            options.UseNpgsql(connectionString);
        });

        // Telegram Bot
        services.Configure<TelegramBotSettings>(
            context.Configuration.GetSection("TelegramBotSettings"));

        services.AddSingleton<ITelegramBotClient>(
            new TelegramBotClient(telegramSettings.Token));

        services.Configure<GoogleSheetsSettings>(
            context.Configuration.GetSection("GoogleSheetsSettings"));
        services.AddScoped<IGoogleSheetsService, GoogleSheetsService>();

        services.AddScoped<UpdateHandler>();

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
        Console.WriteLine("✅ Миграции БД применены");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Ошибка миграций: {ex.Message}");
        throw;
    }
}

Console.WriteLine("🚀 TelegramBot запускается...");
await host.RunAsync();

