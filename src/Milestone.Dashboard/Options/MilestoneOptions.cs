namespace Milestone.Dashboard.Options;

public sealed class MilestoneOptions
{
    public const string SectionName = "Milestone";

    public bool UseDemoData { get; set; } = true;

    public string GatewayBaseUrl { get; set; } = "https://xprotect.example.com";

    public string TokenUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ClientId { get; set; } = "GrantValidatorClient";

    public bool BypassSslValidation { get; set; }

    public int PageSize { get; set; } = 200;

    public double DefaultLatitude { get; set; } = 34.0522;

    public double DefaultLongitude { get; set; } = -118.2437;

    public int DefaultZoom { get; set; } = 13;

    public string ResolvedTokenUrl()
    {
        if (!string.IsNullOrWhiteSpace(TokenUrl))
        {
            return TokenUrl.TrimEnd('/');
        }

        return $"{GatewayBaseUrl.TrimEnd('/')}/API/IDP/connect/token";
    }

    public string ResolvedApiBaseUrl() => $"{GatewayBaseUrl.TrimEnd('/')}/api/rest/v1";
}
