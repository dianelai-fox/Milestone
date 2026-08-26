using Milestone.Dashboard.Models;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class SecurityServerInventoryTests
{
    [Fact]
    public void Scores_online_status_and_storage_from_recording_servers()
    {
        var overview = SecurityServerInventory.From(
        [
            Server("rec1", "REC-01 Downtown", enabled: true, used: 400, max: 1000),
            Server("rec2", "REC-02 Studio", enabled: true, used: 800, max: 1000),
            Server("rec3", "REC-03 Warehouse", enabled: false, used: 930, max: 1000)
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
        Assert.Equal(2, overview.AttentionCount);

        var downtown = overview.Servers.Single(server => server.Name == "REC-01 Downtown");
        Assert.Equal("Online", downtown.Status);
        Assert.Equal("Healthy", downtown.StorageHealth);
        Assert.False(downtown.NeedsAttention);
        Assert.Equal(1, downtown.VolumeCount);

        var studio = overview.Servers.Single(server => server.Name == "REC-02 Studio");
        Assert.Equal(87, studio.WorstVolumeUsagePercent);
        Assert.Equal("Warning", studio.StorageHealth);
        Assert.True(studio.NeedsAttention);

        var warehouse = overview.Servers.Single(server => server.Name == "REC-03 Warehouse");
        Assert.Equal("Offline", warehouse.Status);
        Assert.Equal("Critical", warehouse.StorageHealth);
        Assert.True(warehouse.NeedsAttention);
        Assert.Equal(warehouse.Name, overview.AttentionServers[0].Name);
    }

    [Fact]
    public void Includes_configured_application_servers_such_as_lenel()
    {
        var overview = SecurityServerInventory.From(
        [
            Server("rec1", "REC-01 Downtown", enabled: true, used: 400, max: 1000)
        ],
        [
            Volume("s1", "rec1", 400, 1000)
        ],
        [
            new RecordingServerInfo
            {
                Id = "managed:FOXUSWDMSIA297",
                Name = "FOXUSWDMSIA297",
                HostName = "FOXUSWDMSIA297",
                Enabled = true,
                Role = "Lenel",
                Application = "Lenel",
                Kind = "application",
                Source = "Managed",
                UsedSpaceMb = 184_320,
                MaxSizeMb = 524_288,
                VolumeCount = 2,
                WorstVolumeUsagePercent = 48,
                StorageReported = true
            }
        ]);

        Assert.Equal(2, overview.TotalServers);
        Assert.Equal(1, overview.RecordingServerCount);
        Assert.Equal(1, overview.ApplicationServerCount);
        var lenel = overview.Servers.Single(server => server.Name == "FOXUSWDMSIA297");
        Assert.Equal("Lenel", lenel.Role);
        Assert.Equal("Online", lenel.Status);
        Assert.Equal("Healthy", lenel.StorageHealth);
        Assert.False(lenel.IsRecordingServer);
    }

    [Fact]
    public void Unreported_storage_is_not_treated_as_healthy()
    {
        var overview = SecurityServerInventory.From(
            [],
            [],
            [
                new RecordingServerInfo
                {
                    Id = "managed:FOXUSWDMSIA297",
                    Name = "FOXUSWDMSIA297",
                    Enabled = true,
                    Role = "Lenel",
                    Kind = "application",
                    Source = "Managed",
                    StorageReported = false
                }
            ]);

        var server = Assert.Single(overview.Servers);
        Assert.Equal("Not reported", server.StorageHealth);
        Assert.Equal(1, overview.StorageUnknownCount);
        Assert.Equal(0, overview.StorageHealthyCount);
        Assert.False(server.NeedsAttention);
    }

    [Fact]
    public void Online_server_with_enough_storage_does_not_need_attention()
    {
        var overview = SecurityServerInventory.From(
        [
            Server("rec1", "REC-01", enabled: true, used: 100, max: 1000)
        ],
        [
            Volume("s1", "rec1", 100, 1000)
        ]);

        var server = Assert.Single(overview.Servers);
        Assert.Equal("Healthy", server.StorageHealth);
        Assert.Equal(0, overview.AttentionCount);
        Assert.False(server.NeedsAttention);
    }

    private static RecordingServerInfo Server(string id, string name, bool enabled, long used, long max) =>
        new()
        {
            Id = id,
            Name = name,
            HostName = $"{id}.campus.local",
            Enabled = enabled,
            UsedSpaceMb = used,
            MaxSizeMb = max
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
