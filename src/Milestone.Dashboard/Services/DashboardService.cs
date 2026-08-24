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
            AnnotateCameras(snapshot);
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
            AnnotateCameras(cached);
            return cached;
        }
    }

    public async Task SaveOverrideAsync(LocationOverrideRequest request, CancellationToken cancellationToken)
    {
        await _overrides.SaveAsync(request, cancellationToken);
    }

    public async Task<LocationImportResult> ImportOverridesAsync(
        IEnumerable<LocationImportItem> items,
        CancellationToken cancellationToken,
        bool replaceExisting = true)
    {
        var pending = items.ToList();
        var skipped = 0;
        var unmatched = new List<string>();
        var invalid = new List<string>();
        var overrides = new List<LocationOverrideRequest>();

        IReadOnlyList<CameraInfo> cameras = [];
        if (pending.Any(item => string.IsNullOrWhiteSpace(item.CameraId)))
        {
            cameras = (await GetSnapshotAsync(cancellationToken)).Cameras;
        }

        foreach (var item in pending)
        {
            if (!GeoCoordinate.TryNormalize(item.Latitude, item.Longitude, out var latitude, out var longitude))
            {
                if (item.Latitude is null || item.Longitude is null)
                {
                    skipped++;
                }
                else
                {
                    invalid.Add(item.Name ?? item.CameraId ?? "(unknown camera)");
                }

                continue;
            }

            var cameraId = item.CameraId;
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                var camera = cameras.FirstOrDefault(candidate =>
                    !string.IsNullOrWhiteSpace(item.Name)
                    && candidate.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
                cameraId = camera?.Id;
            }

            if (string.IsNullOrWhiteSpace(cameraId))
            {
                unmatched.Add(item.Name ?? "(blank row)");
                continue;
            }

            overrides.Add(new LocationOverrideRequest
            {
                CameraId = cameraId,
                Latitude = latitude,
                Longitude = longitude,
                Site = item.Site,
                Address = item.Address,
                SiteName = item.SiteName
            });
        }

        var previous = await _overrides.GetAllAsync(cancellationToken);
        var importedIds = new HashSet<string>(overrides.Select(item => item.CameraId), StringComparer.OrdinalIgnoreCase);
        var removed = 0;
        if (replaceExisting)
        {
            await _overrides.ReplaceAllAsync(overrides, cancellationToken);
            removed = previous.Keys.Count(id => !importedIds.Contains(id));
        }
        else if (overrides.Count > 0)
        {
            await _overrides.SaveManyAsync(overrides, cancellationToken);
        }

        var cameraCount = 0;
        try
        {
            cameraCount = (await GetSnapshotAsync(cancellationToken)).Cameras.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Imported locations but could not reload the camera count.");
        }

        return new LocationImportResult
        {
            Saved = overrides.Count,
            Removed = removed,
            CameraCount = cameraCount,
            Skipped = skipped,
            Unmatched = unmatched,
            Invalid = invalid
        };
    }

    internal static void AnnotateCameras(DashboardSnapshot snapshot)
    {
        var source = snapshot.Source.StartsWith("demo", StringComparison.OrdinalIgnoreCase)
            ? "Demo"
            : "Milestone Production";
        foreach (var camera in snapshot.Cameras)
        {
            camera.DeviceSource ??= source;
            camera.Vendor ??= CameraIdentity.Vendor(camera.Model, camera.HardwareDriver);
            camera.IpAddress ??= CameraIdentity.Host(camera.HardwareAddress);
            camera.Intelligence = DeviceIntelligenceCatalog.Evaluate(camera);
        }

        snapshot.Sites = SiteInventory.FromCameras(snapshot.Cameras);
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
            if (!string.IsNullOrWhiteSpace(location.SiteName))
            {
                camera.Site = location.SiteName.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(location.Site))
            {
                camera.Site = location.Site.Trim();
            }

            if (!string.IsNullOrWhiteSpace(location.Address))
            {
                camera.Address = location.Address.Trim();
            }

            var properties = new Dictionary<string, string>(
                camera.CustomProperties ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(location.Address))
            {
                properties["Address"] = location.Address.Trim();
            }

            if (!string.IsNullOrWhiteSpace(location.Site))
            {
                properties["SiteCode"] = location.Site.Trim();
                var labels = (camera.Labels ?? []).ToList();
                if (!labels.Any(label => label.Equals(location.Site.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    labels.Add(location.Site.Trim());
                }

                camera.Labels = labels;
            }

            camera.CustomProperties = properties;
        }
    }
}
