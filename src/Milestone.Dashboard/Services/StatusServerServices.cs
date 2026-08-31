using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public static class StatusServerServices
{
    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MSSQLSERVER"] = "SQL Server",
        ["SQLSERVERAGENT"] = "SQL Agent",
        ["SQLBrowser"] = "SQL Browser",
        ["W3SVC"] = "IIS",
        ["WAS"] = "IIS Admin",
        ["TermService"] = "RDP"
    };

    public static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Sanitize)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> Watched(StatusServerCatalog.Spec spec)
    {
        if (spec.Services is { Count: > 0 })
        {
            return spec.Services
                .Select(Sanitize)
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return DefaultFor(spec);
    }

    public static IReadOnlyList<string> DefaultFor(StatusServerCatalog.Spec spec)
    {
        if (IsLinux(spec.CatalogOs))
        {
            return [];
        }

        var names = new List<string>();
        if (LooksLikeSql(spec))
        {
            names.Add("MSSQLSERVER");
            names.Add("SQLSERVERAGENT");
        }

        if (LooksLikeApp(spec))
        {
            names.Add("W3SVC");
        }

        return names;
    }

    public static string DisplayName(string serviceName) =>
        DisplayNames.TryGetValue(serviceName, out var label) ? label : serviceName;

    public static IReadOnlyList<StatusServiceInfo> Unreachable(
        IEnumerable<string> names,
        string status,
        string detail)
    {
        return names.Select(name => new StatusServiceInfo
        {
            Name = name,
            DisplayName = DisplayName(name),
            Status = status,
            Detail = detail
        }).ToList();
    }

    public static IReadOnlyList<StatusServiceInfo> FromReadings(
        IEnumerable<string> watched,
        IReadOnlyDictionary<string, StatusServiceInfo> found)
    {
        return watched.Select(name =>
        {
            if (found.TryGetValue(name, out var reading))
            {
                return reading;
            }

            return new StatusServiceInfo
            {
                Name = name,
                DisplayName = DisplayName(name),
                Status = "Not found",
                Detail = "The service was not returned by the host."
            };
        }).ToList();
    }

    internal static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }

        var trimmed = name.Trim();
        return trimmed.All(character => char.IsLetterOrDigit(character) || character is '_' or '.' or '$' or '-')
            ? trimmed
            : "";
    }

    private static bool IsLinux(string? os) =>
        !string.IsNullOrWhiteSpace(os) && os.Contains("linux", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSql(StatusServerCatalog.Spec spec)
    {
        if (!string.IsNullOrWhiteSpace(spec.Sql))
        {
            return true;
        }

        var role = spec.Role ?? "";
        return role.Contains("db", StringComparison.OrdinalIgnoreCase)
               || role.Contains("sql", StringComparison.OrdinalIgnoreCase)
               || role.Contains("database", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeApp(StatusServerCatalog.Spec spec)
    {
        var role = spec.Role ?? "";
        return role.Contains("app", StringComparison.OrdinalIgnoreCase)
               || role.Contains("application", StringComparison.OrdinalIgnoreCase)
               || role.Contains("iis", StringComparison.OrdinalIgnoreCase)
               || role.Contains("web", StringComparison.OrdinalIgnoreCase);
    }
}
