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

    private readonly string? _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LocationOverrideStore(IWebHostEnvironment environment, ILogger<LocationOverrideStore> logger)
    {
        _path = AppDataPaths.CombineFile(environment, "location-overrides.json", logger);
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
                mutable[request.CameraId] = mutable.TryGetValue(request.CameraId, out var existing)
                    ? Merge(existing, request)
                    : request;
            }

            await WriteUnlockedAsync(mutable.Values, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ReplaceAllAsync(IEnumerable<LocationOverrideRequest> requests, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await WriteUnlockedAsync(requests, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyDictionary<string, LocationOverrideRequest>> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        if (_path is null || !File.Exists(_path))
        {
            return new Dictionary<string, LocationOverrideRequest>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            var items = await JsonSerializer.DeserializeAsync<List<LocationOverrideRequest>>(stream, JsonOptions, cancellationToken)
                        ?? [];
            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.CameraId))
                .ToDictionary(item => item.CameraId, item => item, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return new Dictionary<string, LocationOverrideRequest>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task WriteUnlockedAsync(IEnumerable<LocationOverrideRequest> requests, CancellationToken cancellationToken)
    {
        if (_path is null)
        {
            throw new InvalidOperationException(AppDataPaths.GrantHint);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, requests, JsonOptions, cancellationToken);
    }

    private static LocationOverrideRequest Merge(LocationOverrideRequest existing, LocationOverrideRequest incoming) =>
        new()
        {
            CameraId = incoming.CameraId,
            Latitude = incoming.Latitude,
            Longitude = incoming.Longitude,
            Site = First(incoming.Site, existing.Site),
            Address = First(incoming.Address, existing.Address),
            SiteName = First(incoming.SiteName, First(incoming.Site, existing.SiteName))
        };

    private static string? First(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}
