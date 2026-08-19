using System.Net;
using System.Text.RegularExpressions;

namespace Milestone.Dashboard.Services;

public static partial class CameraIdentity
{
    private static readonly (string Token, string Vendor)[] Vendors =
    [
        ("axis", "Axis"),
        ("hanwha", "Hanwha"),
        ("wisenet", "Hanwha"),
        ("bosch", "Bosch"),
        ("hikvision", "Hikvision"),
        ("sony", "Sony"),
        ("panasonic", "Panasonic"),
        ("avigilon", "Avigilon"),
        ("pelco", "Pelco"),
        ("vivotek", "Vivotek"),
        ("dahua", "Dahua"),
        ("uniview", "Uniview"),
        ("canon", "Canon"),
        ("arecont", "Arecont"),
        ("mobotix", "Mobotix"),
        ("onvif", "ONVIF")
    ];

    [GeneratedRegex(@"\b\d{1,3}(?:\.\d{1,3}){3}\b", RegexOptions.CultureInvariant)]
    private static partial Regex IPv4Expression();

    public static string? Vendor(string? model, string? driver = null)
    {
        return FromText(model) ?? FromText(driver);
    }

    public static string? Host(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var value = address.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        if (IPAddress.TryParse(value, out _))
        {
            return value;
        }

        var match = IPv4Expression().Match(value);
        return match.Success ? match.Value : null;
    }

    public static string? DisplayModel(string? model, string? vendor = null)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        var display = model.Trim();
        vendor ??= Vendor(display);
        if (!string.IsNullOrWhiteSpace(vendor)
            && display.StartsWith(vendor, StringComparison.OrdinalIgnoreCase))
        {
            display = display[vendor.Length..].Trim(' ', '-');
        }

        return string.IsNullOrWhiteSpace(display) ? model.Trim() : display;
    }

    private static string? FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text.Trim();
        foreach (var (token, vendor) in Vendors)
        {
            if (normalized.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return vendor;
            }
        }

        return null;
    }
}
