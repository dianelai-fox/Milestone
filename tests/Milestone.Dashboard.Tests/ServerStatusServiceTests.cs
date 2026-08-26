using Milestone.Dashboard.Options;
using Milestone.Dashboard.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Milestone.Dashboard.Tests;

public class ServerStatusServiceTests
{
    [Fact]
    public async Task Uses_demo_servers_when_none_configured_and_demo_mode_is_on()
    {
        var options = new MilestoneOptions { UseDemoData = true, MonitoredServers = [] };
        var catalog = new MonitoredServerCatalog(options, Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"), NullLogger<MonitoredServerCatalog>.Instance);
        var service = new ServerStatusService(catalog, new MonitoredServerProbe(), options);

        var overview = await service.GetOverviewAsync(CancellationToken.None);

        Assert.Equal("demo", overview.Source);
        Assert.True(overview.TotalServers >= 2);
    }

    [Fact]
    public async Task Returns_empty_when_none_configured_and_demo_mode_is_off()
    {
        var options = new MilestoneOptions { UseDemoData = false, MonitoredServers = [] };
        var catalog = new MonitoredServerCatalog(options, Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"), NullLogger<MonitoredServerCatalog>.Instance);
        var service = new ServerStatusService(catalog, new MonitoredServerProbe(), options);

        var overview = await service.GetOverviewAsync(CancellationToken.None);

        Assert.Equal(0, overview.TotalServers);
        Assert.Equal("live", overview.Source);
    }
}
