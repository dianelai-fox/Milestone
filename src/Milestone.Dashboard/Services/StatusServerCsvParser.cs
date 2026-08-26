namespace Milestone.Dashboard.Services;

public static class StatusServerCsvParser
{
    public static List<StatusServerCatalog.Spec> Parse(string text)
    {
        var items = new List<StatusServerCatalog.Spec>();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
        {
            return items;
        }

        var headers = CsvText.Split(lines[0])
            .Select(header => Normalize(header.Trim().TrimStart('\uFEFF')))
            .ToList();
        int Index(params string[] names)
        {
            foreach (var name in names)
            {
                var index = headers.IndexOf(Normalize(name));
                if (index >= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        var nameIndex = Index("server name", "servername", "name");
        var ipIndex = Index("ip address", "ipaddress", "ip");
        var descriptionIndex = Index("server description", "description", "deck");
        var functionIndex = Index("server function", "function", "role");
        var domainIndex = Index("domain");
        var environmentIndex = Index("environment");
        var osIndex = Index("os", "operating system");
        var sqlIndex = Index("sql", "sql server");

        foreach (var line in lines.Skip(1))
        {
            var cells = CsvText.Split(line);
            var name = Read(cells, nameIndex);
            var ip = Read(cells, ipIndex);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ip))
            {
                continue;
            }

            var description = Read(cells, descriptionIndex);
            var function = Read(cells, functionIndex);
            var deck = string.IsNullOrWhiteSpace(description) ? "MasterMind" : description;
            items.Add(new StatusServerCatalog.Spec(
                name,
                ip,
                string.IsNullOrWhiteSpace(function) ? InferRole(name) : function,
                deck,
                description ?? deck,
                Read(cells, domainIndex),
                Read(cells, environmentIndex),
                Read(cells, osIndex),
                Read(cells, sqlIndex)));
        }

        return items;
    }

    public static string ToCsv(IEnumerable<StatusServerCatalog.Spec> servers)
    {
        var lines = new List<string>
        {
            "Server Name,IP Address,Server Description,Server Function,Domain,Environment,OS,SQL"
        };
        lines.AddRange(servers.Select(server => string.Join(',',
            CsvText.Escape(server.Name),
            server.IpAddress,
            CsvText.Escape(server.Description ?? server.Deck),
            CsvText.Escape(server.Role),
            CsvText.Escape(server.Domain),
            CsvText.Escape(server.Environment),
            CsvText.Escape(server.CatalogOs),
            CsvText.Escape(server.Sql))));
        return string.Join('\n', lines) + "\n";
    }

    internal static string InferRole(string name)
    {
        if (name.Contains("DB", StringComparison.OrdinalIgnoreCase))
        {
            return "Database";
        }

        if (name.Contains("AP", StringComparison.OrdinalIgnoreCase))
        {
            return "Application";
        }

        return "Server";
    }

    private static string Normalize(string value) =>
        new([.. value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

    private static string? Read(IReadOnlyList<string> cells, int index)
    {
        if (index < 0 || index >= cells.Count)
        {
            return null;
        }

        var value = cells[index].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
