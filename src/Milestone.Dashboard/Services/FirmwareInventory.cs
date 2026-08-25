using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public static class FirmwareInventory
{
    public static FirmwareOverview FromCameras(IEnumerable<CameraInfo> cameras)
    {
        var list = cameras.ToList();
        var na = list.Count(HasNoFirmware);
        var outdated = list.Where(IsOutdated).ToList();
        var compliant = list.Count(IsCompliant);
        var scored = compliant + outdated.Count;
        var sites = list
            .GroupBy(camera => string.IsNullOrWhiteSpace(camera.Site) ? "Unassigned" : camera.Site.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(BuildSite)
            .ToList();

        return new FirmwareOverview
        {
            TotalDevices = list.Count,
            CompliantCount = compliant,
            NonCompliantCount = outdated.Count,
            NaCount = na,
            OverallCompliancePercent = scored == 0 ? 0 : Math.Round(compliant * 100d / scored, 0),
            CompliantVersionCount = compliant,
            VulnerableCount = list.Count(IsVulnerable),
            AvailableUpgradeCount = list.Count(HasUpgrade),
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
            TopNonCompliantModels = TopSlices(outdated, ModelLabel),
            TopNonCompliantTypes = TopSlices(outdated, LifecycleInventory.DeviceType),
            Details = list
                .Where(camera => IsOutdated(camera) || IsVulnerable(camera) || HasUpgrade(camera))
                .Select(camera => new FirmwareDetailRow
                {
                    Id = camera.Id,
                    Name = camera.Name,
                    Site = camera.Site,
                    Vendor = camera.Vendor,
                    Model = camera.Model,
                    Firmware = camera.Firmware,
                    SuggestedFirmware = camera.Intelligence?.SuggestedFirmware,
                    Status = DetailStatus(camera),
                    Vulnerability = camera.Intelligence?.VulnerabilitySeverity
                })
                .OrderBy(row => row.Status == "Outdated" ? 0 : row.Status == "Upgrade available" ? 1 : 2)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public static bool IsOutdated(CameraInfo camera)
    {
        if (camera.Intelligence?.LifecycleStatus == "EOS")
        {
            return true;
        }

        return HasUpgrade(camera);
    }

    public static bool IsCompliant(CameraInfo camera) =>
        !HasNoFirmware(camera) && !IsOutdated(camera);

    public static bool HasNoFirmware(CameraInfo camera) =>
        string.IsNullOrWhiteSpace(camera.Firmware);

    public static bool IsVulnerable(CameraInfo camera) =>
        camera.Intelligence?.VulnerabilitySeverity is "High" or "Medium";

    public static bool HasUpgrade(CameraInfo camera)
    {
        var firmware = camera.Firmware;
        var suggested = camera.Intelligence?.SuggestedFirmware;
        return !string.IsNullOrWhiteSpace(firmware)
               && !string.IsNullOrWhiteSpace(suggested)
               && !firmware.StartsWith(suggested, StringComparison.OrdinalIgnoreCase);
    }

    private static string DetailStatus(CameraInfo camera) =>
        IsOutdated(camera) ? "Outdated"
        : HasUpgrade(camera) ? "Upgrade available"
        : IsVulnerable(camera) ? "Vulnerable"
        : "Current";

    private static PasswordSiteAlert BuildSite(IGrouping<string, CameraInfo> group)
    {
        var cameras = group.ToList();
        var alerted = cameras.Count(IsOutdated);
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

    private static string ModelLabel(CameraInfo camera)
    {
        var vendor = string.IsNullOrWhiteSpace(camera.Vendor) ? "" : camera.Vendor.Trim();
        var model = string.IsNullOrWhiteSpace(camera.Model) ? "Unknown model" : camera.Model.Trim();
        if (string.IsNullOrWhiteSpace(vendor) || model.StartsWith(vendor, StringComparison.OrdinalIgnoreCase))
        {
            return model;
        }

        return $"{vendor} {model}";
    }
}
