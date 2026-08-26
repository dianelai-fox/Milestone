using System.Text.Json;
using Milestone.Dashboard.Options;

namespace Milestone.Dashboard.Services;

public sealed class ManagedServerCatalog
{
    public static readonly ManagedServerSpec LenelFoxuswdmsia297 = new()
    {
        Name = "FOXUSWDMSIA297",
        HostName = "LENELNEWAPP.INT.APPS.FOX",
        Role = "Lenel application",
        Application = "Lenel"
    };

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
        : this(options, Path.Combine(environment.ContentRootPath, "App_Data", "security-servers.json"), logger)
    {
    }

    internal ManagedServerCatalog(MilestoneOptions options, string dataPath, ILogger<ManagedServerCatalog> logger)
    {
        _options = options;
        _dataPath = dataPath;
        _logger = logger;
    }

    public IReadOnlyList<ManagedServerSpec> List()
    {
        var merged = new Dictionary<string, ManagedServerSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in BuiltIn().Concat(_options.ManagedServers).Concat(ReadFile()))
        {
            var key = string.IsNullOrWhiteSpace(spec.Name) ? spec.ResolvedHost() : spec.Name.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            merged[key] = spec;
        }

        return merged.Values.ToList();
    }

    internal static IReadOnlyList<ManagedServerSpec> BuiltIn() => [LenelFoxuswdmsia297];

    private IEnumerable<ManagedServerSpec> ReadFile()
    {
        if (!File.Exists(_dataPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ManagedServerSpec>>(File.ReadAllText(_dataPath), JsonOptions)
                   ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read extra security servers from {Path}", _dataPath);
            return [];
        }
    }
}
