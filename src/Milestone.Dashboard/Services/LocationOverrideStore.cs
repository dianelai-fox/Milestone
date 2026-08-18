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

    public LocationOverrideStore(IWebHostEnvironment environment)
    {
        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "location-overrides.json");
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
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadUnlockedAsync(cancellationToken);
            var mutable = current.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            mutable[request.CameraId] = request;
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
