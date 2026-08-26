using System.Text.Json;
using Milestone.Dashboard.Options;

namespace Milestone.Dashboard.Services;

public sealed class MonitoredServerCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly MilestoneOptions _options;
    private readonly string _dataPath;
    private readonly ILogger<MonitoredServerCatalog> _logger;

    public MonitoredServerCatalog(
        MilestoneOptions options,
        IWebHostEnvironment environment,
        ILogger<MonitoredServerCatalog> logger)
        : this(options, Path.Combine(environment.ContentRootPath, "App_Data", "monitored-servers.json"), logger)
    {
    }

    internal MonitoredServerCatalog(
        MilestoneOptions options,
        string dataPath,
        ILogger<MonitoredServerCatalog> logger)
    {
        _options = options;
        _dataPath = dataPath;
        _logger = logger;
    }

    public IReadOnlyList<MonitoredServerSpec> List()
    {
        var merged = new Dictionary<string, MonitoredServerSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in _options.MonitoredServers.Concat(ReadFile()))
        {
            var key = spec.DisplayName();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            merged[key] = spec;
        }

        return merged.Values
            .OrderBy(spec => spec.DisplayName(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<MonitoredServerSpec> DemoServers() =>
    [
        new()
        {
            Name = "APP-LENEL-01",
            HostName = "lenel-app.campus.local",
            IpAddress = "10.20.30.11",
            Role = "Access control"
        },
        new()
        {
            Name = "APP-BADGE-02",
            HostName = "badge-db.campus.local",
            IpAddress = "10.20.30.12",
            Role = "Database"
        },
        new()
        {
            Name = "APP-NVR-GATE",
            HostName = "gate-nvr.campus.local",
            IpAddress = "10.20.30.13",
            Role = "Edge appliance"
        }
    ];

    private IEnumerable<MonitoredServerSpec> ReadFile()
    {
        if (!File.Exists(_dataPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<MonitoredServerSpec>>(File.ReadAllText(_dataPath), JsonOptions)
                   ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read monitored servers from {Path}", _dataPath);
            return [];
        }
    }
}
