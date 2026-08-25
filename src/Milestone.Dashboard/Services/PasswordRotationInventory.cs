using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public static class PasswordRotationInventory
{
    public static PasswordRotationOverview FromCameras(IEnumerable<CameraInfo> cameras, DateTimeOffset? now = null)
    {
        var clock = now ?? DateTimeOffset.UtcNow;
        var list = cameras.ToList();
        var upToDate = list.Count(camera => RotationStatus(camera) == "Up To Date");
        var never = list.Count(camera => RotationStatus(camera) == "Never Rotated");
        var expired = list.Count(camera => RotationStatus(camera) == "Overdue");
        var soon = list.Count(camera => RotationStatus(camera) == "Due Soon");
        var na = list.Count - upToDate - never - expired - soon;
        var compliant = upToDate + soon;
        var nonCompliant = never + expired;
        var scored = compliant + nonCompliant;
        var sites = list
            .GroupBy(camera => string.IsNullOrWhiteSpace(camera.Site) ? "Unassigned" : camera.Site.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(BuildSite)
            .ToList();
        var alerted = list.Where(IsAlerted).ToList();

        return new PasswordRotationOverview
        {
            TotalDevices = list.Count,
            CompliantCount = compliant,
            NonCompliantCount = nonCompliant,
            NaCount = na,
            OverallCompliancePercent = scored == 0 ? 0 : Math.Round(compliant * 100d / scored, 0),
            UpToDateCount = upToDate,
            NeverRotatedCount = never,
            ExpiredCount = expired,
            SoonCount = soon,
            CompliantSites = sites.Count(site => site.Alerted == 0),
            NonCompliantSites = sites.Count(site => site.Alerted > 0),
            TotalSites = sites.Count,
            TopAlertedSites = sites
                .Where(site => site.Alerted > 0)
                .OrderByDescending(site => site.RiskPercent)
                .ThenByDescending(site => site.Alerted)
                .ThenBy(site => site.Site, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList(),
            NonCompliantByUserType = TopSlices(alerted, UserType),
            NonCompliantByDeviceType = TopSlices(alerted, LifecycleInventory.DeviceType),
            ExpirationBreakdown =
            [
                new() { Label = "180 days", Count = list.Count(camera => RemainingDays(camera, clock) > 90) },
                new() { Label = "90 days", Count = list.Count(camera => RemainingDays(camera, clock) is > 0 and <= 90) },
                new() { Label = "Expired", Count = expired }
            ]
        };
    }

    internal static string UserType(CameraInfo camera)
    {
        var user = (camera.HardwareUserName ?? "").Trim().ToLowerInvariant();
        if (user is "root" or "admin" or "administrator")
        {
            return "admin";
        }

        if (user is "operator" or "ops")
        {
            return "operator";
        }

        if (user is "viewer" or "user" or "guest" or "live")
        {
            return "viewer";
        }

        return string.IsNullOrWhiteSpace(user) ? "unknown" : user;
    }

    internal static bool IsAlerted(CameraInfo camera)
    {
        var status = RotationStatus(camera);
        return status is "Never Rotated" or "Overdue";
    }

    private static PasswordSiteAlert BuildSite(IGrouping<string, CameraInfo> group)
    {
        var cameras = group.ToList();
        var alerted = cameras.Count(IsAlerted);
        return new PasswordSiteAlert
        {
            Site = group.Key,
            Alerted = alerted,
            Total = cameras.Count,
            RiskPercent = cameras.Count == 0
                ? 0
                : Math.Round(alerted * 1000d / cameras.Count) / 10d
        };
    }

    private static IReadOnlyList<LifecycleSlice> TopSlices(
        IEnumerable<CameraInfo> cameras,
        Func<CameraInfo, string> label) =>
        cameras
            .GroupBy(label, StringComparer.OrdinalIgnoreCase)
            .Select(group => new LifecycleSlice { Label = group.Key, Count = group.Count() })
            .OrderByDescending(slice => slice.Count)
            .ThenBy(slice => slice.Label, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

    private static string? RotationStatus(CameraInfo camera) => camera.Intelligence?.PasswordExpiryStatus;

    private static double? RemainingDays(CameraInfo camera, DateTimeOffset now)
    {
        var expiry = camera.Intelligence?.PasswordExpiryDate;
        if (expiry is null)
        {
            return null;
        }

        return (expiry.Value - now).TotalDays;
    }
}
