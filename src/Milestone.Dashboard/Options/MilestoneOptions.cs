using Milestone.Dashboard.Services;

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

    public List<ManagedServerSpec> ManagedServers { get; set; } = [];

    public string ResolvedTokenUrl()
    {
        var urls = XprotectAuth.TokenUrlCandidates(GatewayBaseUrl, TokenUrl);
        return urls.Count > 0 ? urls[0] : string.Empty;
    }

    public string ResolvedApiBaseUrl() =>
        $"{XprotectAuth.NormalizeGatewayBaseUrl(GatewayBaseUrl)}/api/rest/v1";
}

public sealed class ManagedServerSpec
{
    public string Name { get; set; } = string.Empty;
    public string? HostName { get; set; }
    public string Role { get; set; } = "Application server";
    public string? Application { get; set; }
    public int[] ProbePorts { get; set; } = [445, 3389, 80, 443];
    public bool CheckStorage { get; set; } = true;

    public string DisplayName() =>
        string.IsNullOrWhiteSpace(Name) ? ResolvedHost() : Name.Trim();

    public string ResolvedHost() =>
        string.IsNullOrWhiteSpace(HostName) ? Name.Trim() : HostName.Trim();

    public IReadOnlyList<string> ProbeHosts() =>
        new[] { HostName, Name }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
