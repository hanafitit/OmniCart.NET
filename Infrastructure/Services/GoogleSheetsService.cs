using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Options;
using OmniCart.Domain.Entities;
using OmniCart.Infrastructure.Configuration;

namespace OmniCart.Infrastructure.Services;

public class GoogleSheetsService : IGoogleSheetsService
{
    private readonly GoogleSheetsSettings _settings;
    private static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };

    public GoogleSheetsService(IOptions<GoogleSheetsSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task AddOrderAsync(Order order, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_settings.SpreadsheetId) || string.IsNullOrEmpty(_settings.PrivateKey))
        {
            // If not configured, we just skip (can be logged)
            return;
        }

        var credential = new ServiceAccountCredential(
            new ServiceAccountCredential.Initializer(_settings.ServiceAccountEmail)
            {
                Scopes = Scopes
            }.FromPrivateKey(_settings.PrivateKey));

        var service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _settings.ApplicationName
        });

        var range = "A1"; // Append to the first sheet
        var valueRange = new ValueRange
        {
            Values = new List<IList<object>>
            {
                new List<object>
                {
                    order.Id.ToString(),
                    order.CreatedAt.ToString("g"),
                    order.User?.FirstName ?? "Unknown",
                    order.TotalPrice.ToString("F2"),
                    order.Status
                }
            }
        };

        var appendRequest = service.Spreadsheets.Values.Append(valueRange, _settings.SpreadsheetId, range);
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;

        await appendRequest.ExecuteAsync(ct);
    }
}
