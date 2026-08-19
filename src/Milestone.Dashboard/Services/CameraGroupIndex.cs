using System.Text.Json;

namespace Milestone.Dashboard.Services;

public static class CameraGroupIndex
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Build(IEnumerable<JsonElement> groups)
    {
        var labels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            Walk(group, [], labels, visited);
        }

        return labels.ToDictionary(
            pair => pair.Key,
            IReadOnlyList<string> (pair) => pair.Value
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void Walk(
        JsonElement group,
        IReadOnlyList<string> path,
        Dictionary<string, List<string>> labels,
        HashSet<string> visited)
    {
        var current = JsonElementReader.Unwrap(group);
        var id = JsonElementReader.ReadString(current, "id");
        if (!string.IsNullOrWhiteSpace(id) && !visited.Add(id))
        {
            return;
        }

        var name = JsonElementReader.ReadOptionalString(current, "displayName")
                   ?? JsonElementReader.ReadOptionalString(current, "name");
        var nextPath = string.IsNullOrWhiteSpace(name)
            ? path.ToList()
            : path.Append(name).ToList();
        var label = string.Join(" / ", nextPath);

        foreach (var camera in JsonElementReader.ReadChildArray(current, "cameras"))
        {
            var cameraId = JsonElementReader.ReadOptionalString(camera, "id")
                           ?? JsonElementReader.ReadRelationId(camera, "self");
            if (string.IsNullOrWhiteSpace(cameraId) || string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            if (!labels.TryGetValue(cameraId, out var list))
            {
                list = [];
                labels[cameraId] = list;
            }

            if (!list.Contains(label, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(label);
            }
        }

        foreach (var child in JsonElementReader.ReadChildArray(current, "cameraGroups"))
        {
            Walk(child, nextPath, labels, visited);
        }
    }
}
