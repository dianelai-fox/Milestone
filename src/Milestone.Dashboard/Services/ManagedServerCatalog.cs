using System.Text.Json;
using Milestone.Dashboard.Options;

namespace Milestone.Dashboard.Services;

public sealed class ManagedServerCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly MilestoneOptions _options;
    private readonly string _dataPath;
    private readonly ILogger<ManagedServerCatalog> _logger;

    public ManagedServerCatalog(
        MilestoneOptions options,
        IWebHostEnvironment environment,
        ILogger<ManagedServerCatalog> logger)
    {
        _options = options;
        _logger = logger;
        _dataPath = Path.Combine(environment.ContentRootPath, "App_Data", "security-servers.json");
    }

    public IReadOnlyList<ManagedServerSpec> List()
    {
        var merged = new Dictionary<string, ManagedServerSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in _options.ManagedServers.Concat(ReadFile()))
        {
            var key = spec.ResolvedHost();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            merged[key] = spec;
        }

        return merged.Values.ToList();
    }

    private IEnumerable<ManagedServerSpec> ReadFile()
    {
        if (!File.Exists(_dataPath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_dataPath);
            return JsonSerializer.Deserialize<List<ManagedServerSpec>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read extra security servers from {Path}", _dataPath);
            return [];
        }
    }
}
