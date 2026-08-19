using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public static partial class GisPointParser
{
    [GeneratedRegex(
        @"^POINT\s*(?:EMPTY|\(\s*(?<lon>-?\d+(?:\.\d+)?)\s+(?<lat>-?\d+(?:\.\d+)?)(?:\s+(?<alt>-?\d+(?:\.\d+)?))?\s*\))$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PointExpression();

    [GeneratedRegex(
        @"^(?<lat>-?\d+(?:\.\d+)?)\s*,\s*(?<lon>-?\d+(?:\.\d+)?)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LatLonExpression();

    public static CameraLocation? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var point = PointExpression().Match(trimmed);
        if (point.Success && point.Groups["lon"].Success)
        {
            return new CameraLocation(
                double.Parse(point.Groups["lon"].Value, CultureInfo.InvariantCulture),
                double.Parse(point.Groups["lat"].Value, CultureInfo.InvariantCulture),
                point.Groups["alt"].Success
                    ? double.Parse(point.Groups["alt"].Value, CultureInfo.InvariantCulture)
                    : null);
        }

        var latLon = LatLonExpression().Match(trimmed);
        if (latLon.Success)
        {
            return new CameraLocation(
                double.Parse(latLon.Groups["lon"].Value, CultureInfo.InvariantCulture),
                double.Parse(latLon.Groups["lat"].Value, CultureInfo.InvariantCulture));
        }

        return null;
    }

    public static CameraLocation? FromElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return Parse(element.GetString());
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var unwrapped = Unwrap(element);
        return Parse(ReadString(unwrapped, "gisPoint"))
               ?? ReadCoordinatePair(unwrapped, "longitude", "latitude")
               ?? ReadCoordinatePair(unwrapped, "lng", "lat")
               ?? ReadCoordinatePair(unwrapped, "positionX", "positionY");
    }

    public static JsonElement Unwrap(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object)
        {
            return data;
        }

        return element;
    }

    private static CameraLocation? ReadCoordinatePair(JsonElement element, string longitudeName, string latitudeName)
    {
        if (!TryReadDouble(element, longitudeName, out var longitude)
            || !TryReadDouble(element, latitudeName, out var latitude))
        {
            return null;
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return null;
        }

        return new CameraLocation(longitude, latitude);
    }

    private static bool TryReadDouble(JsonElement element, string name, out double value)
    {
        value = 0;
        if (!element.TryGetProperty(name, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetDouble(out value),
            JsonValueKind.String => double.TryParse(property.GetString(), CultureInfo.InvariantCulture, out value),
            _ => false
        };
    }

    private static string ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
}
