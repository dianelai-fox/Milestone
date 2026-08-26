using System.Text.Json;
using Milestone.Dashboard.Options;

namespace Milestone.Dashboard.Services;

public sealed class MonitoredServerCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly MilestoneOptions _options;
    private readonly string _dataPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<MonitoredServerCatalog> _logger;

    public MonitoredServerCatalog(
        MilestoneOptions options,
        IWebHostEnvironment environment,
        ILogger<MonitoredServerCatalog> logger)
        : this(options, ResolvePath(environment, logger), logger)
    {
    }

    internal MonitoredServerCatalog(MilestoneOptions options, string dataPath, ILogger<MonitoredServerCatalog> logger)
    {
        _options = options;
        _dataPath = dataPath;
        _logger = logger;
    }

    public IReadOnlyList<MonitoredServerSpec> List()
    {
        var merged = new Dictionary<string, MonitoredServerSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in SeedServers().Concat(ReadFile()))
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

    public async Task<MonitoredServerSpec> SaveAsync(MonitoredServerSpec spec, CancellationToken cancellationToken)
    {
        var name = spec.DisplayName();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Server name, host name, or IP address is required.");
        }

        if (spec.ProbeTargets().Count == 0)
        {
            throw new ArgumentException("Enter a host name or IP address to check.");
        }

        spec.Name = name;
        spec.Source = "saved";
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var current = ReadFileUnlocked().ToDictionary(item => item.DisplayName(), StringComparer.OrdinalIgnoreCase);
            current[name] = spec;
            await WriteUnlockedAsync(current.Values.ToList(), cancellationToken);
            return spec;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var current = ReadFileUnlocked().ToList();
            var remaining = current
                .Where(item => !item.DisplayName().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (remaining.Count == current.Count)
            {
                return false;
            }

            await WriteUnlockedAsync(remaining, cancellationToken);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    internal IReadOnlyList<MonitoredServerSpec> SeedServers()
    {
        var seeded = new List<MonitoredServerSpec>();
        if (_options.UseDemoData)
        {
            seeded.AddRange(DemoSamples());
        }

        foreach (var spec in _options.MonitoredServers)
        {
            spec.Source = string.IsNullOrWhiteSpace(spec.Source) ? "config" : spec.Source;
            seeded.Add(spec);
        }

        return seeded;
    }

    internal static IReadOnlyList<MonitoredServerSpec> DemoSamples() =>
    [
        new()
        {
            Name = "FOXUSWDMSIA297",
            HostName = "LENELNEWAPP.INT.APPS.FOX",
            Role = "Lenel application",
            Source = "demo"
        },
        new()
        {
            Name = "Dashboard host",
            HostName = "localhost",
            IpAddress = "127.0.0.1",
            Role = "This website",
            Source = "demo"
        }
    ];

    private IEnumerable<MonitoredServerSpec> ReadFile()
    {
        if (!_lock.Wait(0))
        {
            return ReadFileUnlocked();
        }

        try
        {
            return ReadFileUnlocked();
        }
        finally
        {
            _lock.Release();
        }
    }

    private IReadOnlyList<MonitoredServerSpec> ReadFileUnlocked()
    {
        if (!File.Exists(_dataPath))
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<MonitoredServerSpec>>(File.ReadAllText(_dataPath), JsonOptions)
                        ?? [];
            foreach (var item in items)
            {
                item.Source = "saved";
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read monitored servers from {Path}", _dataPath);
            return [];
        }
    }

    private async Task WriteUnlockedAsync(IReadOnlyList<MonitoredServerSpec> servers, CancellationToken cancellationToken)
    {
        var folder = Path.GetDirectoryName(_dataPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var payload = JsonSerializer.Serialize(servers, JsonOptions);
        await File.WriteAllTextAsync(_dataPath, payload, cancellationToken);
    }

    private static string ResolvePath(IWebHostEnvironment environment, ILogger logger)
    {
        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "App_Data"),
            Path.Combine(environment.WebRootPath ?? environment.ContentRootPath, "..", "App_Data"),
            Path.Combine(Path.GetTempPath(), "MilestoneDashboard")
        };

        foreach (var folder in candidates)
        {
            try
            {
                var fullFolder = Path.GetFullPath(folder);
                Directory.CreateDirectory(fullFolder);
                var probe = Path.Combine(fullFolder, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return Path.Combine(fullFolder, "monitored-servers.json");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not use monitored-server folder {Folder}", folder);
            }
        }

        return Path.Combine(Path.GetTempPath(), "MilestoneDashboard", "monitored-servers.json");
    }
}
