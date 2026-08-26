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

        var delimiter = DetectDelimiter(lines[0]);
        var headers = CsvText.Split(lines[0], delimiter)
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
        var descriptionIndex = Index("server description", "description");
        var deckIndex = Index("deck");
        var functionIndex = Index("server function", "function", "role");
        var domainIndex = Index("domain");
        var environmentIndex = Index("environment");
        var osIndex = Index("os", "operating system");
        var sqlIndex = Index("sql", "sql server");

        foreach (var line in lines.Skip(1))
        {
            var cells = CsvText.Split(line, delimiter);
            var name = Read(cells, nameIndex);
            var ip = Read(cells, ipIndex);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ip))
            {
                continue;
            }

            var description = Read(cells, descriptionIndex);
            var function = Read(cells, functionIndex);
            var deck = ResolveApplication(description, Read(cells, deckIndex));
            items.Add(new StatusServerCatalog.Spec
            {
                Name = name,
                IpAddress = ip,
                Role = string.IsNullOrWhiteSpace(function) ? InferRole(name) : function,
                Deck = deck,
                Description = description ?? deck,
                Domain = Read(cells, domainIndex),
                Environment = Read(cells, environmentIndex),
                CatalogOs = Read(cells, osIndex),
                Sql = Read(cells, sqlIndex)
            });
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

    internal static string ResolveApplication(string? description, string? deck, string fallback = "MasterMind")
    {
        var knownFromDescription = TryKnownDeck(description);
        if (knownFromDescription is not null)
        {
            return knownFromDescription;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            return description.Trim();
        }

        var knownFromDeck = TryKnownDeck(deck);
        if (knownFromDeck is not null)
        {
            return knownFromDeck;
        }

        return string.IsNullOrWhiteSpace(deck) ? fallback : deck.Trim();
    }

    internal static string? TryKnownDeck(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var compact = new string([.. value.Where(character => !char.IsWhiteSpace(character))]);
        if (compact.Equals("Perspective", StringComparison.OrdinalIgnoreCase))
        {
            return "Perspective";
        }

        if (compact.Equals("MasterMind", StringComparison.OrdinalIgnoreCase))
        {
            return "MasterMind";
        }

        return null;
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

    private static char DetectDelimiter(string header)
    {
        var commas = CsvText.Split(header, ',').Count;
        var semicolons = CsvText.Split(header, ';').Count;
        return semicolons > commas ? ';' : ',';
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
