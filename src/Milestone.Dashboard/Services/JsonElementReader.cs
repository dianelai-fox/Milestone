using System.Globalization;
using System.Text.Json;

namespace Milestone.Dashboard.Services;

internal static class JsonElementReader
{
    public static JsonElement Unwrap(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("data", out var data)
            && data.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            return data;
        }

        return element;
    }

    public static List<JsonElement> ReadArray(JsonElement root)
    {
        var current = Unwrap(root);
        if (current.ValueKind == JsonValueKind.Array)
        {
            return current.EnumerateArray().Select(item => item.Clone()).ToList();
        }

        if (current.ValueKind == JsonValueKind.Object
            && current.TryGetProperty("array", out var array)
            && array.ValueKind == JsonValueKind.Array)
        {
            return array.EnumerateArray().Select(item => item.Clone()).ToList();
        }

        return [];
    }

    public static List<JsonElement> ReadChildArray(JsonElement element, string name)
    {
        var current = Unwrap(element);
        if (!current.TryGetProperty(name, out var value))
        {
            return [];
        }

        return value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray()
                .Where(item => item.ValueKind is JsonValueKind.Object or JsonValueKind.String)
                .Select(item => item.Clone())
                .ToList(),
            JsonValueKind.Object => [value.Clone()],
            _ => []
        };
    }

    public static string ReadString(JsonElement element, string name)
    {
        return ReadOptionalString(element, name) ?? string.Empty;
    }

    public static string? ReadOptionalString(JsonElement element, string name)
    {
        var current = Unwrap(element);
        if (!current.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => EmptyToNull(value.GetString()),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public static bool ReadBool(JsonElement element, string name, bool fallback = false)
    {
        var current = Unwrap(element);
        if (!current.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            JsonValueKind.Number => value.TryGetInt32(out var number) && number != 0,
            _ => fallback
        };
    }

    public static bool? ReadOptionalBool(JsonElement element, string name)
    {
        var current = Unwrap(element);
        if (!current.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number != 0,
            _ => null
        };
    }

    public static long ReadLong(JsonElement element, string name)
    {
        return ReadOptionalLong(element, name) ?? 0;
    }

    public static int? ReadOptionalInt(JsonElement element, string name)
    {
        var value = ReadOptionalLong(element, name);
        return value is null ? null : (int)value.Value;
    }

    public static long? ReadOptionalLong(JsonElement element, string name)
    {
        var current = Unwrap(element);
        if (!current.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    public static DateTimeOffset? ReadDate(JsonElement element, string name)
    {
        var text = ReadOptionalString(element, name);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    public static string? ReadRelationId(JsonElement element, string relationName)
    {
        var current = Unwrap(element);
        if (current.TryGetProperty(relationName, out var direct))
        {
            var nestedId = ReadOptionalString(direct, "id");
            if (!string.IsNullOrWhiteSpace(nestedId))
            {
                return nestedId;
            }
        }

        if (!current.TryGetProperty("relations", out var relations))
        {
            return null;
        }

        if (!relations.TryGetProperty(relationName, out var relation))
        {
            return null;
        }

        return ReadOptionalString(relation, "id");
    }

    public static string? ReadPathId(JsonElement element, string name)
    {
        var current = Unwrap(element);
        if (!current.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Object => ReadOptionalString(value, "id"),
            JsonValueKind.String => EmptyToNull(value.GetString()),
            _ => null
        };
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
