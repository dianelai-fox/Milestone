using System.Text.Json;
using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public sealed class LocationOverrideStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LocationOverrideStore(IWebHostEnvironment environment, ILogger<LocationOverrideStore> logger)
    {
        _path = ResolvePath(environment, logger);
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
                return Path.Combine(fullFolder, "location-overrides.json");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not use location folder {Folder}", folder);
            }
        }

        throw new InvalidOperationException(
            "IIS cannot write camera locations. Grant Modify on C:\\inetpub\\xprotect-dashboard\\App_Data to IIS AppPool\\XProtectDashboard.");
    }

    public async Task<IReadOnlyDictionary<string, LocationOverrideRequest>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await ReadUnlockedAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(LocationOverrideRequest request, CancellationToken cancellationToken)
    {
        await SaveManyAsync([request], cancellationToken);
    }

    public async Task SaveManyAsync(IEnumerable<LocationOverrideRequest> requests, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadUnlockedAsync(cancellationToken);
            var mutable = current.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var request in requests)
            {
                mutable[request.CameraId] = request;
            }

            await using var stream = File.Create(_path);
            await JsonSerializer.SerializeAsync(stream, mutable.Values, JsonOptions, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyDictionary<string, LocationOverrideRequest>> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, LocationOverrideRequest>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = File.OpenRead(_path);
        var items = await JsonSerializer.DeserializeAsync<List<LocationOverrideRequest>>(stream, JsonOptions, cancellationToken)
                    ?? [];
        return items.ToDictionary(item => item.CameraId, item => item, StringComparer.OrdinalIgnoreCase);
    }
}
