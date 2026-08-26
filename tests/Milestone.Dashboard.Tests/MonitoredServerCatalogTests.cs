using Microsoft.Extensions.Logging.Abstractions;
using Milestone.Dashboard.Options;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class MonitoredServerCatalogTests
{
    [Fact]
    public void Lists_configured_and_demo_servers_by_name()
    {
        var catalog = Catalog(new MilestoneOptions
        {
            UseDemoData = true,
            MonitoredServers =
            [
                new()
                {
                    Name = "FOXUSWDMSIA297",
                    HostName = "LENELNEWAPP.INT.APPS.FOX",
                    IpAddress = "10.50.12.97",
                    Role = "Lenel application"
                }
            ]
        });

        var servers = catalog.List();
        var lenel = Assert.Single(servers, server => server.Name == "FOXUSWDMSIA297");
        Assert.Equal("LENELNEWAPP.INT.APPS.FOX", lenel.HostName);
        Assert.Equal("10.50.12.97", lenel.IpAddress);
        Assert.Contains(servers, server => server.Name == "Dashboard host");
    }

    [Fact]
    public async Task Saved_server_overrides_config_and_can_be_removed()
    {
        var catalog = Catalog(new MilestoneOptions
        {
            UseDemoData = false,
            MonitoredServers =
            [
                new() { Name = "APP-01", HostName = "app01.lab.local", Role = "IIS" }
            ]
        });

        await catalog.SaveAsync(new MonitoredServerSpec
        {
            Name = "APP-01",
            HostName = "app01.lab.local",
            IpAddress = "192.168.10.21",
            Role = "IIS"
        }, CancellationToken.None);

        var saved = Assert.Single(catalog.List());
        Assert.Equal("192.168.10.21", saved.IpAddress);
        Assert.Equal("saved", saved.Source);

        Assert.True(await catalog.RemoveAsync("APP-01", CancellationToken.None));
        var configured = Assert.Single(catalog.List());
        Assert.Null(configured.IpAddress);
        Assert.Equal("config", configured.Source);
    }

    [Fact]
    public void Probe_targets_prefer_ip_then_host_name()
    {
        var spec = new MonitoredServerSpec
        {
            Name = "FOXUSWDMSIA297",
            HostName = "LENELNEWAPP.INT.APPS.FOX",
            IpAddress = "10.50.12.97"
        };

        Assert.Equal(["10.50.12.97", "LENELNEWAPP.INT.APPS.FOX"], spec.ProbeTargets());
    }

    private static MonitoredServerCatalog Catalog(MilestoneOptions options)
    {
        var path = Path.Combine(Path.GetTempPath(), "MilestoneDashboardTests", Guid.NewGuid().ToString("N"), "monitored-servers.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new MonitoredServerCatalog(options, path, NullLogger<MonitoredServerCatalog>.Instance);
    }
}
