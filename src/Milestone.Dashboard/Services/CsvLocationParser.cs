using System.Globalization;
using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public static class CsvLocationParser
{
    public static List<LocationImportItem> Parse(string text)
    {
        var items = new List<LocationImportItem>();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
        {
            return items;
        }

        var headers = Split(lines[0]).Select(header => header.Trim().TrimStart('\uFEFF').ToLowerInvariant()).ToList();
        int Index(params string[] names)
        {
            foreach (var name in names)
            {
                var index = headers.IndexOf(name);
                if (index >= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        var idIndex = Index("cameraid");
        var nameIndex = Index("name");
        var latIndex = Index("latitude");
        var lonIndex = Index("longitude");
        var siteIndex = Index("site");
        var addressIndex = Index("address");
        var siteNameIndex = Index("site_name", "sitename");

        foreach (var line in lines.Skip(1))
        {
            var cells = Split(line);
            var latitude = ReadDouble(cells, latIndex);
            var longitude = ReadDouble(cells, lonIndex);
            if (latitude is null || longitude is null)
            {
                continue;
            }

            items.Add(new LocationImportItem
            {
                CameraId = Read(cells, idIndex),
                Name = Read(cells, nameIndex),
                Latitude = latitude,
                Longitude = longitude,
                Site = Read(cells, siteIndex),
                Address = Read(cells, addressIndex),
                SiteName = Read(cells, siteNameIndex)
            });
        }

        return items;
    }

    private static string? Read(IReadOnlyList<string> cells, int index)
    {
        if (index < 0 || index >= cells.Count)
        {
            return null;
        }

        var value = cells[index].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static double? ReadDouble(IReadOnlyList<string> cells, int index)
    {
        var value = Read(cells, index);
        if (value is null)
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    internal static List<string> Split(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (character == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                quoted = !quoted;
                continue;
            }

            if (character == ',' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        values.Add(current.ToString());
        return values;
    }
}
