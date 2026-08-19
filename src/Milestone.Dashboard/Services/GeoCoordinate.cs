using System.Globalization;

namespace Milestone.Dashboard.Services;

public static class GeoCoordinate
{
    public static bool TryNormalize(double? latitude, double? longitude, out double latitudeValue, out double longitudeValue)
    {
        latitudeValue = 0;
        longitudeValue = 0;
        if (latitude is null || longitude is null)
        {
            return false;
        }

        var lat = Repair(latitude.Value, -90, 90);
        var lon = Repair(longitude.Value, -180, 180);
        if (lat is null && lon is not null)
        {
            lat = Repair(longitude.Value, -90, 90);
            lon = Repair(latitude.Value, -180, 180);
        }

        if (lat is null || lon is null)
        {
            return false;
        }

        latitudeValue = lat.Value;
        longitudeValue = lon.Value;
        return true;
    }

    internal static double? Repair(double value, double min, double max)
    {
        if (value >= min && value <= max)
        {
            return value;
        }

        var sign = value < 0 ? -1 : 1;
        var digits = Math.Abs(value).ToString("F0", CultureInfo.InvariantCulture);
        foreach (var wholeDigits in new[] { 2, 3, 1 })
        {
            if (digits.Length <= wholeDigits)
            {
                continue;
            }

            if (digits.Length - wholeDigits < 4)
            {
                continue;
            }

            var text = digits.Insert(wholeDigits, ".");
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var repaired))
            {
                continue;
            }

            repaired *= sign;
            if (repaired >= min && repaired <= max)
            {
                return repaired;
            }
        }

        return null;
    }
}
