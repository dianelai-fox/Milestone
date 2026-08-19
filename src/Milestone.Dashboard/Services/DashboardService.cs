using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public sealed class DashboardService
{
    private readonly IVmsClient _vmsClient;
    private readonly LocationOverrideStore _overrides;
    private readonly SnapshotCache _cache;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IVmsClient vmsClient,
        LocationOverrideStore overrides,
        SnapshotCache cache,
        ILogger<DashboardService> logger)
    {
        _vmsClient = vmsClient;
        _overrides = overrides;
        _cache = cache;
        _logger = logger;
    }

    public bool SqlCacheEnabled => _cache.IsEnabled;

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _vmsClient.GetSnapshotAsync(cancellationToken);
            ApplyOverrides(snapshot, await _overrides.GetAllAsync(cancellationToken));
            await _cache.SaveAsync(snapshot, cancellationToken);
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load live Milestone data. Trying the SQL cache.");
            var cached = await _cache.TryLoadAsync(cancellationToken);
            if (cached is null)
            {
                throw;
            }

            ApplyOverrides(cached, await _overrides.GetAllAsync(cancellationToken));
            cached.Source = $"{cached.Source}-cached";
            return cached;
        }
    }

    public async Task SaveOverrideAsync(LocationOverrideRequest request, CancellationToken cancellationToken)
    {
        await _overrides.SaveAsync(request, cancellationToken);
    }

    public async Task<LocationImportResult> ImportOverridesAsync(
        IEnumerable<LocationImportItem> items,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        var unmatched = new List<string>();
        var saved = 0;

        foreach (var item in items)
        {
            var camera = snapshot.Cameras.FirstOrDefault(candidate =>
                (!string.IsNullOrWhiteSpace(item.CameraId)
                 && candidate.Id.Equals(item.CameraId, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(item.Name)
                    && candidate.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)));

            if (camera is null)
            {
                unmatched.Add(item.CameraId ?? item.Name ?? "(blank row)");
                continue;
            }

            await SaveOverrideAsync(new LocationOverrideRequest
            {
                CameraId = camera.Id,
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                Site = item.Site
            }, cancellationToken);
            saved++;
        }

        return new LocationImportResult { Saved = saved, Unmatched = unmatched };
    }

    internal static void ApplyOverrides(
        DashboardSnapshot snapshot,
        IReadOnlyDictionary<string, LocationOverrideRequest> overrides)
    {
        foreach (var camera in snapshot.Cameras)
        {
            if (!overrides.TryGetValue(camera.Id, out var location))
            {
                continue;
            }

            camera.Location = new CameraLocation(location.Longitude, location.Latitude);
            camera.LocationIsOverride = true;
            if (!string.IsNullOrWhiteSpace(location.Site))
            {
                camera.Site = location.Site;
            }
        }
    }
}
