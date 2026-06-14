using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace OmniCart.Infrastructure;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Определяем путь к настройкам бота (основной источник правды для БД)
        var botProjectPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "TelegramBot");
        
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(botProjectPath) ? botProjectPath : Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Берем строку из конфига или используем локальный fallback
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Port=5432;Database=omnicart;Username=postgres;Password=yourpassword;Trust Server Certificate=true;";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            // Помогает избежать падений CLI при нестабильном соединении с БД (например, с Neon)
            npgsqlOptions.EnableRetryOnFailure();
        });

        return new AppDbContext(optionsBuilder.Options);
    }
}
