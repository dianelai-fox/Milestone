namespace Milestone.Dashboard.Services;

public sealed class StatusServerCatalog
{
    public sealed record Spec(
        string Name,
        string IpAddress,
        string Role,
        string Deck = "MasterMind",
        string? Description = null,
        string? Domain = null,
        string? Environment = null,
        string? CatalogOs = null,
        string? Sql = null);

    public static IReadOnlyList<Spec> MasterMind { get; } =
    [
        new("FOXUSWDMSDB303", "10.180.80.154", "App/DB", "MasterMind", "MasterMind", "corp.fox", "Prod", "Windows Server 2022", "Microsoft SQL Server 2022"),
        new("FOXUSWDMSDB304", "10.180.80.155", "App/DB", "MasterMind", "MasterMind", "corp.fox", "Prod", "Windows Server 2022", "Microsoft SQL Server 2022"),
        new("FOXUSWDMSDB305", "10.180.80.156", "App/DB", "MasterMind", "MasterMind", "corp.fox", "QA", "Windows Server 2022", "Microsoft SQL Server 2022"),
        new("AZTEC-FOX-1.corp.fox", "10.180.118.10", "Aztec Receivers", "MasterMind", "MasterMind", "corp.fox", "Prod", "Linux", null),
        new("AZTEC-FOX-2.corp.fox", "10.180.118.11", "Aztec Receivers", "MasterMind", "MasterMind", "corp.fox", "Prod", "Linux", null),
        new("AZTEC-FOX-3.corp.fox", "10.180.118.12", "Aztec Receivers", "MasterMind", "MasterMind", "corp.fox", "Prod", "Linux", null),
        new("FOX2204376", "10.138.201.11", "MAS Signal Processor", "MasterMind", "MasterMind", "corp.fox", "Prod", "Windows 10", null),
        new("FOX2205442", "10.138.201.43", "MAS Signal Processor \"Backup\"", "MasterMind", "MasterMind", "corp.fox", "Prod", "Windows 11", null)
    ];

    public static IReadOnlyList<Spec> Perspective { get; } =
    [
        new("FOXUSWDMSDB298", "10.180.80.36", "Database", "Perspective"),
        new("FOXUSWDMSAP654", "10.180.80.37", "Application", "Perspective"),
        new("FOXUSWDMSDB299", "10.180.96.23", "Database", "Perspective"),
        new("FOXUSWDMSAP655", "10.180.96.24", "Application", "Perspective")
    ];

    public IReadOnlyList<Spec> List() => [.. MasterMind, .. Perspective];

    public IReadOnlyList<Spec> Combine(IReadOnlyList<Spec> imported, bool replaceDecks = true)
    {
        var builtIn = List();
        if (imported.Count == 0)
        {
            return builtIn;
        }

        if (replaceDecks)
        {
            var decks = imported
                .Select(server => server.Deck)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return [.. imported, .. builtIn.Where(server => !decks.Contains(server.Deck))];
        }

        var byName = builtIn.ToDictionary(server => server.Name, StringComparer.OrdinalIgnoreCase);
        var extras = new List<Spec>();
        foreach (var row in imported)
        {
            if (byName.ContainsKey(row.Name))
            {
                byName[row.Name] = row;
            }
            else
            {
                extras.Add(row);
            }
        }

        return [.. byName.Values, .. extras];
    }
}
