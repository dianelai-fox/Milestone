using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public static class SiteInventory
{
    public static IReadOnlyList<SiteInfo> FromCameras(IEnumerable<CameraInfo> cameras)
    {
        return cameras
            .GroupBy(camera => string.IsNullOrWhiteSpace(camera.Site) ? "Unassigned" : camera.Site.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(Build)
            .OrderBy(site => site.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static SiteInfo Build(IGrouping<string, CameraInfo> group)
    {
        var cameras = group.ToList();
        var enabled = cameras.Count(camera => camera.Enabled);
        var disabled = cameras.Count - enabled;
        var unmapped = cameras.Count(camera => camera.Location is null);
        var high = cameras.Count(camera => camera.Intelligence.VulnerabilitySeverity == "High");
        var medium = cameras.Count(camera => camera.Intelligence.VulnerabilitySeverity == "Medium");
        var outdated = cameras.Count(IsOutdated);
        var current = cameras.Count(camera => !string.IsNullOrWhiteSpace(camera.Firmware) && !IsOutdated(camera));
        var eos = cameras.Count(camera => camera.Intelligence.LifecycleStatus == "EOS");
        var eol = cameras.Count(camera => camera.Intelligence.LifecycleStatus == "EOL");
        var active = cameras.Count(camera => camera.Intelligence.LifecycleStatus is "Active" or null);
        var mapped = cameras.Where(camera => camera.Location is not null).ToList();
        var labels = cameras
            .SelectMany(camera => camera.Labels)
            .Select(label => label.Split(" / ").Last().Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        return new SiteInfo
        {
            Name = group.Key,
            Description = cameras
                .Select(camera => FirstNonEmpty(
                    camera.Address,
                    GetProperty(camera, "Address"),
                    GetProperty(camera, "Location"),
                    camera.Description,
                    string.Equals(group.Key, "Unassigned", StringComparison.OrdinalIgnoreCase)
                        ? "Site not assigned in XProtect"
                        : camera.Site))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            Status = string.Equals(group.Key, "Unassigned", StringComparison.OrdinalIgnoreCase) ? "N/A"
                : disabled == cameras.Count ? "Disconnected"
                : disabled > 0 || unmapped > 0 ? "Partial"
                : "Connected",
            ManagedCount = cameras.Count,
            EnabledCount = enabled,
            DisabledCount = disabled,
            UnmappedCount = unmapped,
            HighVulnCount = high,
            MediumVulnCount = medium,
            OkVulnCount = Math.Max(0, cameras.Count - high - medium),
            CurrentFirmwareCount = current,
            OutdatedFirmwareCount = outdated,
            UnknownFirmwareCount = cameras.Count - current - outdated,
            ActiveLifecycleCount = active,
            EolCount = eol,
            EosCount = eos,
            Labels = labels,
            Location = mapped.Count == 0
                ? null
                : new CameraLocation(
                    mapped.Average(camera => camera.Location!.Longitude),
                    mapped.Average(camera => camera.Location!.Latitude))
        };
    }

    private static string? GetProperty(CameraInfo camera, string key) =>
        camera.CustomProperties.TryGetValue(key, out var value) ? value : null;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsOutdated(CameraInfo camera)
    {
        if (camera.Intelligence.LifecycleStatus == "EOS")
        {
            return true;
        }

        var firmware = camera.Firmware;
        var suggested = camera.Intelligence.SuggestedFirmware;
        return !string.IsNullOrWhiteSpace(firmware)
               && !string.IsNullOrWhiteSpace(suggested)
               && !firmware.StartsWith(suggested, StringComparison.OrdinalIgnoreCase);
    }
}
