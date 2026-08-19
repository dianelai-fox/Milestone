using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public static class DeviceIntelligenceCatalog
{
    public const int PasswordValidDays = 365;

    private sealed record Profile(
        string Match,
        DateTimeOffset? EosDate,
        bool Discontinued,
        string? SuggestedFirmware,
        string? Replacement,
        string? NdaaStatus = null);

    private static readonly Profile[] Profiles =
    [
        new("P3225-LV MK II", new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero), true, "8.40", "Axis P3275-LV, Axis P3285-LV", "Compliant"),
        new("P3225-LVE MK II", new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero), true, "8.40", "Axis P3275-LVE, Axis P3285-LVE", "Compliant"),
        new("P3225-LV", new DateTimeOffset(2021, 10, 31, 0, 0, 0, TimeSpan.Zero), true, "6.50", "Axis P3275-LV, Axis P3285-LV", "Compliant"),
        new("P3225-LVE", new DateTimeOffset(2021, 10, 31, 0, 0, 0, TimeSpan.Zero), true, "6.50", "Axis P3275-LVE, Axis P3285-LVE", "Compliant"),
        new("P3245-LVE", new DateTimeOffset(2029, 12, 31, 0, 0, 0, TimeSpan.Zero), true, "11.11", "Axis P3275-LVE, Axis P3285-LVE", "Compliant"),
        new("P3245-LV", new DateTimeOffset(2029, 12, 31, 0, 0, 0, TimeSpan.Zero), true, "11.11", "Axis P3275-LV, Axis P3285-LV", "Compliant"),
        new("P3265-LVE", new DateTimeOffset(2031, 12, 31, 0, 0, 0, TimeSpan.Zero), true, "11.11", "Axis P3275-LVE, Axis P3285-LVE", "Compliant"),
        new("P3265-LV", new DateTimeOffset(2031, 12, 31, 0, 0, 0, TimeSpan.Zero), true, "11.11", "Axis P3275-LV, Axis P3285-LV", "Compliant"),
        new("P3265-V", new DateTimeOffset(2031, 12, 31, 0, 0, 0, TimeSpan.Zero), true, "11.11", "Axis P3275-V", "Compliant"),
        new("P1465-LE", new DateTimeOffset(2031, 12, 31, 0, 0, 0, TimeSpan.Zero), true, "11.11", "Axis P1475-LE, Axis P1485-LE", "Compliant"),
        new("Q1700-LE", new DateTimeOffset(2028, 12, 31, 0, 0, 0, TimeSpan.Zero), true, "10.12", "Axis Q1701-LE", "Compliant"),
        new("Q6135-LE", new DateTimeOffset(2029, 12, 31, 0, 0, 0, TimeSpan.Zero), false, "11.11", null, "Compliant"),
        new("Q6318-LE", new DateTimeOffset(2032, 12, 31, 0, 0, 0, TimeSpan.Zero), false, "11.11", null, "Compliant"),
        new("M3086-V", new DateTimeOffset(2031, 12, 31, 0, 0, 0, TimeSpan.Zero), false, "11.11", null, "Compliant"),
        new("M2036-LE", new DateTimeOffset(2031, 12, 31, 0, 0, 0, TimeSpan.Zero), false, "11.11", null, "Compliant"),
        new("PNV-A6081R", null, false, null, "Hanwha PNV-A9081R", "Compliant"),
        new("XNV-8083R", null, false, null, null, "Compliant"),
        new("FLEXIDOME 5100", null, false, null, null, "Compliant"),
        new("DS-2CD2686G2", null, false, null, null, "Restricted")
    ];

    private static readonly HashSet<string> RestrictedVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hikvision", "Dahua", "Huawei", "Hytera", "ZTE"
    };

    private static readonly HashSet<string> CompliantVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        "Axis", "Bosch", "Hanwha", "Sony", "Panasonic", "Avigilon", "Pelco",
        "Canon", "Mobotix", "Vivotek", "Arecont"
    };

    public static DeviceIntelligence Evaluate(CameraInfo camera, DateTimeOffset? now = null)
    {
        var clock = now ?? DateTimeOffset.UtcNow;
        var profile = Find(camera.Model) ?? Find(camera.HardwareName);
        var eos = profile?.EosDate;
        var lifecycle = Lifecycle(eos, profile?.Discontinued == true, clock);
        var passwordExpiry = camera.PasswordLastModified?.AddDays(PasswordValidDays);

        return new DeviceIntelligence
        {
            VulnerabilitySeverity = lifecycle switch
            {
                "EOS" => "High",
                "EOL" => "Medium",
                _ => null
            },
            PatchedFirmware = camera.Firmware,
            SuggestedFirmware = profile?.SuggestedFirmware ?? FirmwareFamily(camera.Firmware),
            LifecycleStatus = lifecycle,
            EosDate = eos,
            ReplacementModel = profile?.Replacement,
            WarrantyStatus = null,
            NdaaStatus = profile?.NdaaStatus ?? VendorNdaa(camera.Vendor),
            PasswordExpiryStatus = PasswordStatus(passwordExpiry, clock),
            PasswordExpiryDate = passwordExpiry,
            SslExpiryStatus = null,
            LastSslCertificate = null
        };
    }

    private static Profile? Find(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        var haystack = model.Replace("Network Camera", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Dome Camera", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Mk II", "MK II", StringComparison.OrdinalIgnoreCase)
            .Replace("Mark II", "MK II", StringComparison.OrdinalIgnoreCase);
        return Profiles.FirstOrDefault(profile =>
            haystack.Contains(profile.Match, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Lifecycle(DateTimeOffset? eos, bool discontinued, DateTimeOffset now)
    {
        if (eos is not null && eos <= now)
        {
            return "EOS";
        }

        if (discontinued)
        {
            return "EOL";
        }

        return eos is null && !discontinued ? null : "Active";
    }

    private static string? VendorNdaa(string? vendor)
    {
        if (string.IsNullOrWhiteSpace(vendor))
        {
            return null;
        }

        if (RestrictedVendors.Contains(vendor))
        {
            return "Restricted";
        }

        return CompliantVendors.Contains(vendor) ? "Compliant" : null;
    }

    private static string? PasswordStatus(DateTimeOffset? expiry, DateTimeOffset now)
    {
        if (expiry is null)
        {
            return null;
        }

        if (expiry <= now)
        {
            return "Overdue";
        }

        return expiry.Value - now <= TimeSpan.FromDays(30) ? "Due Soon" : "Up To Date";
    }

    private static string? FirmwareFamily(string? firmware)
    {
        if (string.IsNullOrWhiteSpace(firmware))
        {
            return null;
        }

        var parts = firmware.TrimStart('V', 'v').Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : firmware.Trim();
    }
}
