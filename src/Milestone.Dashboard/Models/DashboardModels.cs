namespace Milestone.Dashboard.Models;

public sealed record CameraLocation(double Longitude, double Latitude, double? Altitude = null);

public sealed class CameraInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? ShortName { get; init; }
    public string? Description { get; init; }
    public bool Enabled { get; init; }
    public int? Channel { get; init; }
    public string? HardwareId { get; init; }
    public string? HardwareName { get; init; }
    public string? HardwareAddress { get; init; }
    public string? HardwareUserName { get; init; }
    public bool? HardwareEnabled { get; init; }
    public string? HardwareDriver { get; init; }
    public string? Vendor { get; set; }
    public string? Model { get; init; }
    public string? IpAddress { get; set; }
    public string? DeviceSource { get; set; }
    public string? Firmware { get; init; }
    public string? SerialNumber { get; init; }
    public string? MacAddress { get; init; }
    public string? RecordingServerId { get; init; }
    public string? RecordingServerName { get; init; }
    public string? RecordingStorageId { get; init; }
    public string? RecordingStorageName { get; set; }
    public string? FailoverSetting { get; init; }
    public bool? RecordingEnabled { get; init; }
    public bool? EdgeStorageEnabled { get; init; }
    public bool? EdgeStoragePlaybackEnabled { get; init; }
    public bool? PrebufferEnabled { get; init; }
    public int? PrebufferSeconds { get; init; }
    public bool? PtzEnabled { get; init; }
    public DateTimeOffset? CreatedDate { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public DateTimeOffset? PasswordLastModified { get; init; }
    public IReadOnlyList<string> Labels { get; set; } = [];
    public IReadOnlyDictionary<string, string> CustomProperties { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public DeviceIntelligence Intelligence { get; set; } = new();
    public string? Site { get; set; }
    public string? Address { get; set; }
    public CameraLocation? Location { get; set; }
    public bool LocationIsOverride { get; set; }
}

public sealed class DeviceIntelligence
{
    public string? VulnerabilitySeverity { get; init; }
    public string? PatchedFirmware { get; init; }
    public string? SuggestedFirmware { get; init; }
    public DateTimeOffset? LastFirmwareUpgrade { get; init; }
    public string? LifecycleStatus { get; init; }
    public DateTimeOffset? EosDate { get; init; }
    public string? ReplacementModel { get; init; }
    public string? WarrantyStatus { get; init; }
    public DateTimeOffset? WarrantyDate { get; init; }
    public string? NdaaStatus { get; init; }
    public string? PasswordExpiryStatus { get; init; }
    public DateTimeOffset? PasswordExpiryDate { get; init; }
    public string? SslExpiryStatus { get; init; }
    public DateTimeOffset? SslExpiryDate { get; init; }
    public string? LastSslCertificate { get; init; }
    public string? SslCompliance { get; init; }
    public string? Dot1xStatus { get; init; }
    public string? LastHardened { get; init; }
    public string? RecordingStatus { get; init; }
    public string? StorageServer { get; init; }
    public string? SdStatus { get; init; }
    public string? SdWearStatus { get; init; }
    public string? AlertStatus { get; init; }
}

public sealed class StorageVolume
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? RecordingServerId { get; init; }
    public string? RecordingServerName { get; init; }
    public string? DiskPath { get; init; }
    public string Kind { get; init; } = "Recording";
    public long MaxSizeMb { get; init; }
    public long UsedSpaceMb { get; init; }
    public long LockedUsedSpaceMb { get; init; }
    public int RetainMinutes { get; init; }
    public bool IsDefault { get; init; }
    public bool IsAvailable { get; init; } = true;
    public bool IsMounted { get; init; } = true;
    public string? EncryptionMethod { get; init; }

    public double UsagePercent => StorageMetrics.UsagePercent(UsedSpaceMb, MaxSizeMb);
}

public sealed class RecordingServerInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? HostName { get; init; }
    public bool Enabled { get; init; } = true;
    public int CameraCount { get; init; }
    public long UsedSpaceMb { get; init; }
    public long MaxSizeMb { get; init; }
}

public sealed class SiteInfo
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string Status { get; init; } = "Connected";
    public int ManagedCount { get; init; }
    public int EnabledCount { get; init; }
    public int DisabledCount { get; init; }
    public int UnmappedCount { get; init; }
    public int HighVulnCount { get; init; }
    public int MediumVulnCount { get; init; }
    public int OkVulnCount { get; init; }
    public int CurrentFirmwareCount { get; init; }
    public int OutdatedFirmwareCount { get; init; }
    public int UnknownFirmwareCount { get; init; }
    public int ActiveLifecycleCount { get; init; }
    public int EolCount { get; init; }
    public int EosCount { get; init; }
    public IReadOnlyList<string> Labels { get; init; } = [];
    public CameraLocation? Location { get; init; }
}

public sealed class DashboardSnapshot
{
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = "demo";
    public string? SiteName { get; init; }
    public IReadOnlyList<CameraInfo> Cameras { get; init; } = [];
    public IReadOnlyList<SiteInfo> Sites { get; set; } = [];
    public IReadOnlyList<StorageVolume> Storages { get; init; } = [];
    public IReadOnlyList<RecordingServerInfo> RecordingServers { get; init; } = [];
    public CameraLocation? SuggestedMapCenter { get; set; }
    public LifecycleOverview Lifecycle { get; set; } = new();
    public PasswordRotationOverview PasswordRotation { get; set; } = new();

    public DashboardSummary Summary => DashboardSummary.From(this);

    public CameraLocation? ResolveMapCenter()
    {
        var mapped = Cameras.FirstOrDefault(camera => camera.Location is not null)?.Location;
        return mapped ?? SuggestedMapCenter;
    }
}

public sealed class LocationImportItem
{
    public string? CameraId { get; init; }
    public string? Name { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? Site { get; init; }
    public string? Address { get; init; }
    public string? SiteName { get; init; }
}

public sealed class LocationImportResult
{
    public int Saved { get; init; }
    public int Removed { get; init; }
    public int CameraCount { get; init; }
    public int Skipped { get; init; }
    public IReadOnlyList<string> Unmatched { get; init; } = [];
    public IReadOnlyList<string> Invalid { get; init; } = [];
}

public sealed class DashboardSummary
{
    public int CameraCount { get; init; }
    public int EnabledCameraCount { get; init; }
    public int MappedCameraCount { get; init; }
    public int UnmappedCameraCount { get; init; }
    public int RecordingServerCount { get; init; }
    public int StorageCount { get; init; }
    public long UsedSpaceMb { get; init; }
    public long MaxSizeMb { get; init; }
    public double StorageUsagePercent { get; init; }

    public static DashboardSummary From(DashboardSnapshot snapshot)
    {
        var used = snapshot.Storages.Sum(s => s.UsedSpaceMb);
        var max = snapshot.Storages.Sum(s => s.MaxSizeMb);
        return new DashboardSummary
        {
            CameraCount = snapshot.Cameras.Count,
            EnabledCameraCount = snapshot.Cameras.Count(c => c.Enabled),
            MappedCameraCount = snapshot.Cameras.Count(c => c.Location is not null),
            UnmappedCameraCount = snapshot.Cameras.Count(c => c.Location is null),
            RecordingServerCount = snapshot.RecordingServers.Count,
            StorageCount = snapshot.Storages.Count,
            UsedSpaceMb = used,
            MaxSizeMb = max,
            StorageUsagePercent = StorageMetrics.UsagePercent(used, max)
        };
    }
}

public static class StorageMetrics
{
    public static double UsagePercent(long usedMb, long maxMb)
    {
        if (maxMb <= 0)
        {
            return 0;
        }

        var percent = usedMb * 100d / maxMb;
        return Math.Clamp(Math.Round(percent, 1), 0, 999);
    }

    public static string FormatSize(long megaBytes)
    {
        if (megaBytes < 1024)
        {
            return $"{megaBytes:N0} MB";
        }

        var gigaBytes = megaBytes / 1024d;
        if (gigaBytes < 1024)
        {
            return $"{gigaBytes:N1} GB";
        }

        return $"{gigaBytes / 1024d:N2} TB";
    }

    public static string FormatRetention(int minutes)
    {
        if (minutes <= 0)
        {
            return "Not set";
        }

        if (minutes % 1440 == 0)
        {
            var days = minutes / 1440;
            return days == 1 ? "1 day" : $"{days} days";
        }

        if (minutes % 60 == 0)
        {
            var hours = minutes / 60;
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        return $"{minutes} minutes";
    }
}

public sealed class LocationOverrideRequest
{
    public required string CameraId { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public string? Site { get; init; }
    public string? Address { get; init; }
    public string? SiteName { get; init; }
}

public sealed class HealthStatus
{
    public required string Status { get; init; }
    public required string Source { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public bool SqlCacheEnabled { get; init; }
}

public sealed class LifecycleOverview
{
    public int TotalDevices { get; init; }
    public int CompliantCount { get; init; }
    public int NonCompliantCount { get; init; }
    public int NaCount { get; init; }
    public double OverallCompliancePercent { get; init; }
    public int CurrentProductCount { get; init; }
    public int EolCount { get; init; }
    public int EosCount { get; init; }
    public int CompliantSites { get; init; }
    public int NonCompliantSites { get; init; }
    public int TotalSites { get; init; }
    public IReadOnlyList<LifecycleSiteAlert> TopAlertedSites { get; init; } = [];
    public IReadOnlyList<LifecycleSlice> TopNonCompliantModels { get; init; } = [];
    public IReadOnlyList<LifecycleSlice> TopNonCompliantTypes { get; init; } = [];
    public IReadOnlyList<LifecycleYearCount> EosByYear { get; init; } = [];
    public IReadOnlyList<LifecycleSlice> TopEolModels { get; init; } = [];
    public IReadOnlyList<LifecycleSlice> EolByType { get; init; } = [];
    public int NdaaCompliantCount { get; init; }
    public int NdaaRestrictedCount { get; init; }
    public int NdaaUnknownCount { get; init; }
}

public sealed class LifecycleSlice
{
    public required string Label { get; init; }
    public int Count { get; init; }
}

public sealed class LifecycleSiteAlert
{
    public required string Site { get; init; }
    public int Eos { get; init; }
    public int Eol { get; init; }
    public int Total { get; init; }
    public double RiskPercent { get; init; }
}

public sealed class LifecycleYearCount
{
    public int Year { get; init; }
    public int Count { get; init; }
}

public sealed class PasswordRotationOverview
{
    public int TotalDevices { get; init; }
    public int CompliantCount { get; init; }
    public int NonCompliantCount { get; init; }
    public int NaCount { get; init; }
    public double OverallCompliancePercent { get; init; }
    public int UpToDateCount { get; init; }
    public int NeverRotatedCount { get; init; }
    public int ExpiredCount { get; init; }
    public int SoonCount { get; init; }
    public int CompliantSites { get; init; }
    public int NonCompliantSites { get; init; }
    public int TotalSites { get; init; }
    public IReadOnlyList<PasswordSiteAlert> TopAlertedSites { get; init; } = [];
    public IReadOnlyList<LifecycleSlice> NonCompliantByUserType { get; init; } = [];
    public IReadOnlyList<LifecycleSlice> NonCompliantByDeviceType { get; init; } = [];
    public IReadOnlyList<PasswordBucket> ExpirationBreakdown { get; init; } = [];
}

public sealed class PasswordSiteAlert
{
    public required string Site { get; init; }
    public int Alerted { get; init; }
    public int Total { get; init; }
    public double RiskPercent { get; init; }
}

public sealed class PasswordBucket
{
    public required string Label { get; init; }
    public int Count { get; init; }
}
