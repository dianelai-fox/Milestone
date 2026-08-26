namespace Milestone.Dashboard.Services;

public sealed class StatusServerCatalog
{
    public sealed record Spec(string Name, string IpAddress, string Role, string Deck = "MasterMind");

    public static IReadOnlyList<Spec> MasterMind { get; } =
    [
        new("FOXUSWDMSDB303", "10.180.80.154", "Database"),
        new("FOXUSWDMSDB304", "10.180.80.155", "Database"),
        new("FOXUSWDMSDB305", "10.180.80.156", "Database"),
        new("AZTEC-FOX-1.corp.fox", "10.180.118.10", "Application"),
        new("AZTEC-FOX-2.corp.fox", "10.180.118.11", "Application"),
        new("AZTEC-FOX-3.corp.fox", "10.180.118.12", "Application"),
        new("FOX2204376", "10.138.201.11", "Endpoint"),
        new("FOX2205442", "10.138.201.43", "Endpoint")
    ];

    public IReadOnlyList<Spec> List() => MasterMind;
}
