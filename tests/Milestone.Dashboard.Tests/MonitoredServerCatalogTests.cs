using Milestone.Dashboard.Options;
using Milestone.Dashboard.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Milestone.Dashboard.Tests;

public class MonitoredServerCatalogTests
{
    [Fact]
    public void Merges_appsettings_and_file_by_display_name()
    {
        var path = Path.Combine(Path.GetTempPath(), $"monitored-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            [
              { "Name": "APP-02", "HostName": "app02.local", "IpAddress": "10.0.0.2", "Role": "File" },
              { "Name": "APP-01", "HostName": "override.local", "IpAddress": "10.0.0.9", "Role": "Override" }
            ]
            """);

        try
        {
            var options = new MilestoneOptions
            {
                MonitoredServers =
                [
                    new MonitoredServerSpec
                    {
                        Name = "APP-01",
                        HostName = "app01.local",
                        IpAddress = "10.0.0.1",
                        Role = "Settings"
                    }
                ]
            };
            var catalog = new MonitoredServerCatalog(options, path, NullLogger<MonitoredServerCatalog>.Instance);
            var list = catalog.List();

            Assert.Equal(2, list.Count);
            var first = list.Single(server => server.Name == "APP-01");
            Assert.Equal("override.local", first.HostName);
            Assert.Equal("10.0.0.9", first.IpAddress);
            Assert.Equal("Override", first.Role);
            Assert.Contains(list, server => server.Name == "APP-02");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Demo_servers_include_name_hostname_and_ip()
    {
        var demo = MonitoredServerCatalog.DemoServers();
        Assert.True(demo.Count >= 2);
        Assert.All(demo, server =>
        {
            Assert.False(string.IsNullOrWhiteSpace(server.Name));
            Assert.False(string.IsNullOrWhiteSpace(server.HostName));
            Assert.False(string.IsNullOrWhiteSpace(server.IpAddress));
        });
    }
}
