using System.Globalization;
using System.Text.RegularExpressions;
using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public static partial class GisPointParser
{
    [GeneratedRegex(
        @"^POINT\s*(?:EMPTY|\(\s*(?<lon>-?\d+(?:\.\d+)?)\s+(?<lat>-?\d+(?:\.\d+)?)(?:\s+(?<alt>-?\d+(?:\.\d+)?))?\s*\))$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PointExpression();

    public static CameraLocation? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = PointExpression().Match(value.Trim());
        if (!match.Success || !match.Groups["lon"].Success)
        {
            return null;
        }

        var longitude = double.Parse(match.Groups["lon"].Value, CultureInfo.InvariantCulture);
        var latitude = double.Parse(match.Groups["lat"].Value, CultureInfo.InvariantCulture);
        double? altitude = match.Groups["alt"].Success
            ? double.Parse(match.Groups["alt"].Value, CultureInfo.InvariantCulture)
            : null;

        return new CameraLocation(longitude, latitude, altitude);
    }
}
