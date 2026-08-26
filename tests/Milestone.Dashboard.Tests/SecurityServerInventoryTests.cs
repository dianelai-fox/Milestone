using Milestone.Dashboard.Models;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class SecurityServerInventoryTests
{
    [Fact]
    public void Scores_online_storage_and_cpu_from_recording_servers()
    {
        var overview = SecurityServerInventory.From(
        [
            Server("rec1", "REC-01 Downtown", enabled: true, used: 400, max: 1000, cpu: 36),
            Server("rec2", "REC-02 Studio", enabled: true, used: 800, max: 1000, cpu: 81),
            Server("rec3", "REC-03 Warehouse", enabled: false, used: 930, max: 1000, cpu: null)
        ],
        [
            Volume("s1", "rec1", 400, 1000),
            Volume("s2", "rec2", 870, 1000),
            Volume("s3", "rec2", 200, 2000),
            Volume("s4", "rec3", 930, 1000)
        ]);

        Assert.Equal(3, overview.TotalServers);
        Assert.Equal(2, overview.OnlineCount);
        Assert.Equal(1, overview.OfflineCount);
        Assert.Equal(1, overview.StorageHealthyCount);
        Assert.Equal(1, overview.StorageWarningCount);
        Assert.Equal(1, overview.StorageCriticalCount);
        Assert.Equal(1, overview.CpuHealthyCount);
        Assert.Equal(1, overview.CpuAttentionCount);
        Assert.Equal(1, overview.CpuUnreportedCount);
        Assert.Equal(2, overview.AttentionCount);

        var downtown = overview.Servers.Single(server => server.Name == "REC-01 Downtown");
        Assert.Equal("Online", downtown.Status);
        Assert.Equal("Healthy", downtown.StorageHealth);
        Assert.Equal("Healthy", downtown.CpuHealth);
        Assert.False(downtown.NeedsAttention);
        Assert.Equal(1, downtown.VolumeCount);

        var studio = overview.Servers.Single(server => server.Name == "REC-02 Studio");
        Assert.Equal(87, studio.WorstVolumeUsagePercent);
        Assert.Equal("Warning", studio.StorageHealth);
        Assert.Equal("Warning", studio.CpuHealth);
        Assert.True(studio.NeedsAttention);

        var warehouse = overview.Servers.Single(server => server.Name == "REC-03 Warehouse");
        Assert.Equal("Offline", warehouse.Status);
        Assert.Equal("Critical", warehouse.StorageHealth);
        Assert.Equal("Not reported", warehouse.CpuHealth);
        Assert.True(warehouse.NeedsAttention);
        Assert.Equal(warehouse.Name, overview.AttentionServers[0].Name);
    }

    [Fact]
    public void Live_servers_without_cpu_are_not_reported()
    {
        var overview = SecurityServerInventory.From(
        [
            Server("rec1", "REC-01", enabled: true, used: 100, max: 1000, cpu: null)
        ],
        [
            Volume("s1", "rec1", 100, 1000)
        ]);

        var server = Assert.Single(overview.Servers);
        Assert.Null(server.CpuPercent);
        Assert.Equal("Not reported", server.CpuHealth);
        Assert.Equal(1, overview.CpuUnreportedCount);
        Assert.Equal(0, overview.CpuAttentionCount);
        Assert.False(server.NeedsAttention);
    }

    private static RecordingServerInfo Server(string id, string name, bool enabled, long used, long max, double? cpu) =>
        new()
        {
            Id = id,
            Name = name,
            HostName = $"{id}.campus.local",
            Enabled = enabled,
            UsedSpaceMb = used,
            MaxSizeMb = max,
            CpuPercent = cpu
        };

    private static StorageVolume Volume(string id, string serverId, long used, long max) =>
        new()
        {
            Id = id,
            Name = id,
            RecordingServerId = serverId,
            UsedSpaceMb = used,
            MaxSizeMb = max
        };
}
