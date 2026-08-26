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
    public int[]? ProbePorts { get; set; }
    public string Source { get; set; } = "config";

    public string DisplayName()
    {
        if (!string.IsNullOrWhiteSpace(Name))
        {
            return Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(HostName))
        {
            return HostName.Trim();
        }

        return IpAddress?.Trim() ?? string.Empty;
    }

    public int[] ResolvedPorts() =>
        ProbePorts is { Length: > 0 } ? ProbePorts : [445, 3389, 5985, 80, 443, 22];

    public IReadOnlyList<string> ProbeTargets()
    {
        var targets = new List<string>();
        foreach (var value in new[] { IpAddress, HostName })
        {
            var host = value?.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                continue;
            }

            if (!targets.Contains(host, StringComparer.OrdinalIgnoreCase))
            {
                targets.Add(host);
            }
        }

        if (targets.Count == 0 && !string.IsNullOrWhiteSpace(Name))
        {
            targets.Add(Name.Trim());
        }

        return targets;
    }
}
