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

    /// <summary>
    /// Hosts to show on the Server Status page (name, hostname, IP).
    /// Online/offline is probed from the web server.
    /// </summary>
    public List<MonitoredServerSpec> MonitoredServers { get; set; } = [];

    public string ResolvedTokenUrl()
    {
        var urls = XprotectAuth.TokenUrlCandidates(GatewayBaseUrl, TokenUrl);
        return urls.Count > 0 ? urls[0] : string.Empty;
    }

    public string ResolvedApiBaseUrl() =>
        $"{XprotectAuth.NormalizeGatewayBaseUrl(GatewayBaseUrl)}/api/rest/v1";
}

public sealed class MonitoredServerSpec
{
    public string Name { get; set; } = string.Empty;
    public string? HostName { get; set; }
    public string? IpAddress { get; set; }
    public string Role { get; set; } = "Server";
    public int[] ProbePorts { get; set; } = [445, 3389, 80, 443];

    public string DisplayName() =>
        string.IsNullOrWhiteSpace(Name)
            ? (HostName ?? IpAddress ?? string.Empty).Trim()
            : Name.Trim();

    public IReadOnlyList<string> ProbeTargets() =>
        new[] { IpAddress, HostName, Name }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
