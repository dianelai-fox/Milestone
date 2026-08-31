namespace Milestone.Dashboard.Services;

public sealed class StatusServerCatalog
{
    public sealed class Spec
    {
        public string Name { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string Role { get; set; } = "Server";
        public string Deck { get; set; } = "MasterMind";
        public string? Description { get; set; }
        public string? Domain { get; set; }
        public string? Environment { get; set; }
        public string? CatalogOs { get; set; }
        public string? Sql { get; set; }
        public List<string> Services { get; set; } = [];
    }

    public static IReadOnlyList<Spec> MasterMind { get; } =
    [
        Row("FOXUSWDMSDB303", "10.180.80.154", "App/DB", "MasterMind", "corp.fox", "Prod", "Windows Server 2022", "Microsoft SQL Server 2022"),
        Row("FOXUSWDMSDB304", "10.180.80.155", "App/DB", "MasterMind", "corp.fox", "Prod", "Windows Server 2022", "Microsoft SQL Server 2022"),
        Row("FOXUSWDMSDB305", "10.180.80.156", "App/DB", "MasterMind", "corp.fox", "QA", "Windows Server 2022", "Microsoft SQL Server 2022"),
        Row("AZTEC-FOX-1.corp.fox", "10.180.118.10", "Aztec Receivers", "MasterMind", "corp.fox", "Prod", "Linux", null),
        Row("AZTEC-FOX-2.corp.fox", "10.180.118.11", "Aztec Receivers", "MasterMind", "corp.fox", "Prod", "Linux", null),
        Row("AZTEC-FOX-3.corp.fox", "10.180.118.12", "Aztec Receivers", "MasterMind", "corp.fox", "Prod", "Linux", null),
        Row("FOX2204376", "10.138.201.11", "MAS Signal Processor", "MasterMind", "corp.fox", "Prod", "Windows 10", null),
        Row("FOX2205442", "10.138.201.43", "MAS Signal Processor \"Backup\"", "MasterMind", "corp.fox", "Prod", "Windows 11", null)
    ];

    public static IReadOnlyList<Spec> Perspective { get; } =
    [
        new() { Name = "FOXUSWDMSDB298", IpAddress = "10.180.80.36", Role = "Database", Deck = "Perspective", Description = "Perspective" },
        new() { Name = "FOXUSWDMSAP654", IpAddress = "10.180.80.37", Role = "Application", Deck = "Perspective", Description = "Perspective" },
        new() { Name = "FOXUSWDMSDB299", IpAddress = "10.180.96.23", Role = "Database", Deck = "Perspective", Description = "Perspective" },
        new() { Name = "FOXUSWDMSAP655", IpAddress = "10.180.96.24", Role = "Application", Deck = "Perspective", Description = "Perspective" }
    ];

    public IReadOnlyList<Spec> List() => [.. MasterMind, .. Perspective];

    public IReadOnlyList<Spec> Combine(IReadOnlyList<Spec> imported, bool replaceDecks = true)
    {
        var builtIn = List();
        if (imported.Count == 0)
        {
            return builtIn;
        }

        var importedMap = IndexByIdentity(imported);
        if (replaceDecks)
        {
            var decks = importedMap.Values
                .Select(server => ResolveDeck(server))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return
            [
                .. importedMap.Values,
                .. builtIn.Where(server =>
                    !importedMap.ContainsKey(IdentityKey(server)) && !decks.Contains(ResolveDeck(server)))
            ];
        }

        var byKey = IndexByIdentity(builtIn);
        foreach (var row in importedMap.Values)
        {
            var key = IdentityKey(row);
            byKey[key] = byKey.TryGetValue(key, out var existing)
                ? Merge(existing, row)
                : row;
        }

        return byKey.Values.ToList();
    }

    internal static Dictionary<string, Spec> IndexByIdentity(IEnumerable<Spec> servers)
    {
        var map = new Dictionary<string, Spec>(StringComparer.OrdinalIgnoreCase);
        foreach (var server in servers)
        {
            var key = IdentityKey(server);
            if (key.Length == 0)
            {
                continue;
            }

            map[key] = map.TryGetValue(key, out var existing)
                ? Merge(existing, server)
                : server;
        }

        return map;
    }

    internal static string IdentityKey(Spec server)
    {
        if (string.IsNullOrWhiteSpace(server.Name))
        {
            return "";
        }

        return $"{server.Name.Trim()}\u001f{ResolveDeck(server)}";
    }

    internal static string ResolveDeck(Spec server) =>
        StatusServerCsvParser.ResolveApplication(server.Description, server.Deck);

    internal static Spec Merge(Spec existing, Spec incoming)
    {
        var knownDeck = StatusServerCsvParser.ResolveApplication(incoming.Description, incoming.Deck, existing.Deck);
        return new Spec
        {
            Name = incoming.Name,
            IpAddress = First(incoming.IpAddress, existing.IpAddress) ?? existing.IpAddress,
            Role = First(incoming.Role, existing.Role) ?? existing.Role,
            Deck = knownDeck,
            Description = incoming.Description ?? existing.Description ?? knownDeck,
            Domain = incoming.Domain ?? existing.Domain,
            Environment = incoming.Environment ?? existing.Environment,
            CatalogOs = incoming.CatalogOs ?? existing.CatalogOs,
            Sql = incoming.Sql ?? existing.Sql,
            Services = incoming.Services.Count > 0 ? incoming.Services : existing.Services
        };
    }

    private static Spec Row(
        string name,
        string ip,
        string role,
        string deck,
        string? domain,
        string? environment,
        string? os,
        string? sql) =>
        new()
        {
            Name = name,
            IpAddress = ip,
            Role = role,
            Deck = deck,
            Description = deck,
            Domain = domain,
            Environment = environment,
            CatalogOs = os,
            Sql = sql
        };

    private static string? First(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}
