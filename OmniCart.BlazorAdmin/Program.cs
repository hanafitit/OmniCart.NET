using OmniCart.BlazorAdmin.Components;
using MudBlazor.Services;
using OmniCart.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OmniCart.BlazorAdmin.Hubs;

var builder = WebApplication.CreateBuilder(args);

var sharedSettingsPath = Path.Combine(builder.Environment.ContentRootPath, "..", "TelegramBot", "appsettings.json");
if (File.Exists(sharedSettingsPath))
{
    builder.Configuration.AddJsonFile(sharedSettingsPath, optional: true, reloadOnChange: true);
}

// Add services to the container.
builder.Services.AddMudServices();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024 * 128; // 128 KB
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10);
    }).AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 1024 * 128;
    });

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        // Включаем автоматические повторы при временных сбоях сети или БД
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<OrderHub>("/orderhub");

app.Run();
