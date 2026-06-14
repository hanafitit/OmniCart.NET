using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using OmniCart.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace OmniCart.Infrastructure;

public class GoogleSheetsService
{
    private readonly GoogleSheetsSettings _settings;
    private readonly ILogger<GoogleSheetsService> _logger;
    private static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };

    public GoogleSheetsService(IOptions<GoogleSheetsSettings> settings, ILogger<GoogleSheetsService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task AddOrderAsync(Order order, User user, List<OrderItem> items)
    {
        if (string.IsNullOrEmpty(_settings.SpreadsheetId) || string.IsNullOrEmpty(_settings.CredentialsJson))
        {
            _logger.LogWarning("Google Sheets settings are missing. Skipping logging.");
            return;
        }

        try
        {
            var service = CreateService();
            var range = "A1:G1";
            var valueRange = new ValueRange();

            var rowValues = new List<object>
            {
                order.Id,
                order.CreatedAt.ToString("g"),
                user.FirstName + " " + (user.LastName ?? ""),
                user.TelegramUserId,
                string.Join(", ", items.Select(i => i.Product?.Name ?? "Товар")),
                order.TotalPrice,
                user.DeliveryAddress ?? ""
            };

            valueRange.Values = new List<IList<object>> { rowValues };

            var appendRequest = service.Spreadsheets.Values.Append(valueRange, _settings.SpreadsheetId, range);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
            await appendRequest.ExecuteAsync();

            _logger.LogInformation("Order {OrderId} logged to Google Sheets", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging order to Google Sheets");
        }
    }

    private SheetsService CreateService()
    {
        GoogleCredential credential;
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_settings.CredentialsJson)))
        {
            credential = ServiceAccountCredential
                .FromServiceAccountData(stream)
                .ToGoogleCredential()
                .CreateScoped(Scopes);
        }

        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "OmniCart"
        });
    }
}
