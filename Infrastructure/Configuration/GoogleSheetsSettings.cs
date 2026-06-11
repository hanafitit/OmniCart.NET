namespace OmniCart.Infrastructure.Configuration;

public class GoogleSheetsSettings
{
    public string SpreadsheetId { get; set; } = string.Empty;
    public string ServiceAccountEmail { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = "OmniCartBot";
}
