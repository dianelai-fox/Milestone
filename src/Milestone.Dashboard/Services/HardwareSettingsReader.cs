using System.Text.Json;

namespace Milestone.Dashboard.Services;

public sealed record HardwareDeviceDetails(
    string? Model,
    string? Firmware,
    string? SerialNumber,
    string? MacAddress);

public static class HardwareSettingsReader
{
    private static readonly HashSet<string> SkippedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "relations", "definitions", "resources", "tasks", "self", "parent", "id",
        "displayName", "type", "lastModified", "createdDate"
    };

    private static readonly string[] FirmwareKeys =
    [
        "firmwareversion", "firmware", "fwversion", "firmwarever",
        "softwareversion", "swversion", "detectedfirmware", "camerafirmware"
    ];

    private static readonly string[] SerialKeys =
    [
        "serialnumber", "serialno", "serial", "deviceserial", "cameraserial"
    ];

    private static readonly string[] MacKeys =
    [
        "macaddress", "mac", "macaddr", "ethernetaddress", "hwaddress"
    ];

    private static readonly string[] ModelKeys =
    [
        "detectedmodelname", "detectedmodel", "modelname", "productname", "product"
    ];

    public static HardwareDeviceDetails Read(JsonElement settingsRoot)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Collect(settingsRoot, values);
        return new HardwareDeviceDetails(
            First(values, ModelKeys),
            First(values, FirmwareKeys),
            First(values, SerialKeys),
            First(values, MacKeys));
    }

    public static string? ReadParentHardwareId(JsonElement settingsRoot)
    {
        return JsonElementReader.ReadRelationId(settingsRoot, "parent");
    }

    public static HardwareDeviceDetails Merge(HardwareDeviceDetails left, HardwareDeviceDetails right)
    {
        return new HardwareDeviceDetails(
            left.Model ?? right.Model,
            left.Firmware ?? right.Firmware,
            left.SerialNumber ?? right.SerialNumber,
            left.MacAddress ?? right.MacAddress);
    }

    private static void Collect(JsonElement element, Dictionary<string, string> values)
    {
        var current = JsonElementReader.Unwrap(element);
        if (current.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in current.EnumerateArray())
            {
                Collect(item, values);
            }

            return;
        }

        if (current.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in current.EnumerateObject())
        {
            if (SkippedKeys.Contains(property.Name))
            {
                continue;
            }

            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                Collect(property.Value, values);
                continue;
            }

            var text = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.ToString(),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(text) || text is "null" or "<<not set>>")
            {
                continue;
            }

            values.TryAdd(property.Name, text.Trim());
        }
    }

    private static string? First(IReadOnlyDictionary<string, string> values, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            var match = values.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                || pair.Key.Replace("_", "", StringComparison.Ordinal)
                    .Equals(key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }
}
