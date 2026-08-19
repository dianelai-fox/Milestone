using System.Text.Json;

namespace Milestone.Dashboard.Services;

public static class CustomPropertyReader
{
    private static readonly HashSet<string> SkippedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "relations", "definitions", "resources", "tasks", "self", "parent", "id",
        "displayName", "type", "lastModified", "createdDate", "gisPoint"
    };

    public static IReadOnlyDictionary<string, string> Read(JsonElement parent)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var current = JsonElementReader.Unwrap(parent);
        if (!current.TryGetProperty("customProperties", out var properties))
        {
            return values;
        }

        ReadNode(properties, values);
        return values;
    }

    private static void ReadNode(JsonElement element, Dictionary<string, string> values)
    {
        var current = JsonElementReader.Unwrap(element);
        if (current.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in current.EnumerateArray())
            {
                ReadNode(item, values);
            }

            return;
        }

        if (current.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var name = JsonElementReader.ReadOptionalString(current, "name")
                   ?? JsonElementReader.ReadOptionalString(current, "key")
                   ?? JsonElementReader.ReadOptionalString(current, "displayName");
        var value = JsonElementReader.ReadOptionalString(current, "value")
                    ?? JsonElementReader.ReadOptionalString(current, "text");
        if (!string.IsNullOrWhiteSpace(name)
            && !string.IsNullOrWhiteSpace(value)
            && !name.Equals("Custom properties", StringComparison.OrdinalIgnoreCase))
        {
            values.TryAdd(name, value);
        }

        if (current.TryGetProperty("properties", out var properties)
            && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                AddScalar(property.Name, property.Value, values);
            }
        }

        foreach (var property in current.EnumerateObject())
        {
            if (SkippedKeys.Contains(property.Name)
                || property.Name.Equals("name", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("key", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("value", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("text", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("properties", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                ReadNode(property.Value, values);
                continue;
            }

            AddScalar(property.Name, property.Value, values);
        }
    }

    private static void AddScalar(string name, JsonElement value, Dictionary<string, string> values)
    {
        if (SkippedKeys.Contains(name) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(text) && text is not "null")
        {
            values.TryAdd(name, text.Trim());
        }
    }
}
