using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public static class LifecycleInventory
{
    public static LifecycleOverview FromCameras(IEnumerable<CameraInfo> cameras)
    {
        var list = cameras.ToList();
        var current = list.Count(camera => Status(camera) == "Active");
        var eol = list.Count(camera => Status(camera) == "EOL");
        var eos = list.Count(camera => Status(camera) == "EOS");
        var na = list.Count - current - eol - eos;
        var compliant = current + eol;
        var scored = compliant + eos;
        var sites = list
            .GroupBy(camera => string.IsNullOrWhiteSpace(camera.Site) ? "Unassigned" : camera.Site.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(BuildSite)
            .ToList();

        return new LifecycleOverview
        {
            TotalDevices = list.Count,
            CompliantCount = compliant,
            NonCompliantCount = eos,
            NaCount = na,
            OverallCompliancePercent = scored == 0
                ? 0
                : Math.Round(compliant * 100d / scored, 0),
            CurrentProductCount = current,
            EolCount = eol,
            EosCount = eos,
            CompliantSites = sites.Count(site => site.Eos == 0),
            NonCompliantSites = sites.Count(site => site.Eos > 0),
            TotalSites = sites.Count,
            TopAlertedSites = sites
                .Where(site => site.Eos + site.Eol > 0)
                .OrderByDescending(site => site.RiskPercent)
                .ThenByDescending(site => site.Eos + site.Eol)
                .ThenBy(site => site.Site, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList(),
            TopNonCompliantModels = TopSlices(list.Where(camera => Status(camera) == "EOS"), ModelLabel),
            TopNonCompliantTypes = TopSlices(list.Where(camera => Status(camera) == "EOS"), DeviceType),
            EosByYear = list
                .Select(camera => camera.Intelligence?.EosDate)
                .Where(date => date is not null)
                .GroupBy(date => date!.Value.Year)
                .Select(group => new LifecycleYearCount { Year = group.Key, Count = group.Count() })
                .OrderBy(item => item.Year)
                .ToList(),
            TopEolModels = TopSlices(list.Where(camera => Status(camera) == "EOL"), ModelLabel),
            EolByType = TopSlices(list.Where(camera => Status(camera) == "EOL"), DeviceType),
            NdaaCompliantCount = list.Count(camera => Ndaa(camera) == "Compliant"),
            NdaaRestrictedCount = list.Count(camera => Ndaa(camera) == "Restricted"),
            NdaaUnknownCount = list.Count(camera => Ndaa(camera) is not "Compliant" and not "Restricted")
        };
    }

    internal static string DeviceType(CameraInfo camera)
    {
        var haystack = $"{camera.Name} {camera.Model} {camera.HardwareName} {camera.HardwareDriver}";
        if (ContainsAny(haystack, "Access Control", "Door Controller", "Aperio"))
        {
            return "Access Control Panel";
        }

        if (ContainsAny(haystack, "Encoder", "Video Server"))
        {
            return "Encoder";
        }

        if (ContainsAny(haystack, "Horn Speaker", "Network Speaker"))
        {
            return "Network horn speaker";
        }

        if (ContainsAny(haystack, "Intrusion", "Alarm Panel"))
        {
            return "Intrusion Panel";
        }

        if (ContainsAny(haystack, "Gateway", "Bridge"))
        {
            return "Gateway";
        }

        return "Camera";
    }

    private static LifecycleSiteAlert BuildSite(IGrouping<string, CameraInfo> group)
    {
        var cameras = group.ToList();
        var eos = cameras.Count(camera => Status(camera) == "EOS");
        var eol = cameras.Count(camera => Status(camera) == "EOL");
        return new LifecycleSiteAlert
        {
            Site = group.Key,
            Eos = eos,
            Eol = eol,
            Total = cameras.Count,
            RiskPercent = cameras.Count == 0
                ? 0
                : Math.Round((eos + eol) * 1000d / cameras.Count) / 10d
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

    private static string? Status(CameraInfo camera) => camera.Intelligence?.LifecycleStatus;

    private static string? Ndaa(CameraInfo camera) => camera.Intelligence?.NdaaStatus;

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
