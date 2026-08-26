namespace Milestone.Dashboard.Services;

public sealed class StatusServerCatalog
{
    public sealed record Spec(string Name, string IpAddress, string Deck = "MasterMind");

    public static IReadOnlyList<Spec> MasterMind { get; } =
    [
        new("FOXUSWDMSDB303", "10.180.80.154"),
        new("FOXUSWDMSDB304", "10.180.80.155"),
        new("FOXUSWDMSDB305", "10.180.80.156"),
        new("AZTEC-FOX-1.corp.fox", "10.180.118.10"),
        new("AZTEC-FOX-2.corp.fox", "10.180.118.11"),
        new("AZTEC-FOX-3.corp.fox", "10.180.118.12"),
        new("FOX2204376", "10.138.201.11"),
        new("FOX2205442", "10.138.201.43")
    ];

    public IReadOnlyList<Spec> List() => MasterMind;
}
