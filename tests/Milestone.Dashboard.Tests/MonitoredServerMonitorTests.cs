using Microsoft.Extensions.Logging.Abstractions;
using Milestone.Dashboard.Options;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class MonitoredServerMonitorTests
{
    private readonly MonitoredServerMonitor _monitor = new(NullLogger<MonitoredServerMonitor>.Instance);

    [Fact]
    public void Accepts_host_names_and_ip_addresses()
    {
        Assert.True(MonitoredServerMonitor.IsValidHost("LENELNEWAPP.INT.APPS.FOX"));
        Assert.True(MonitoredServerMonitor.IsValidHost("10.50.12.97"));
        Assert.True(MonitoredServerMonitor.IsValidHost("127.0.0.1"));
        Assert.False(MonitoredServerMonitor.IsValidHost("bad host"));
        Assert.False(MonitoredServerMonitor.IsValidHost(""));
    }

    [Fact]
    public async Task Loopback_ip_counts_as_online()
    {
        var reachable = await _monitor.IsReachableAsync("127.0.0.1", [9, 80, 443], CancellationToken.None);
        Assert.True(reachable);
    }

    [Fact]
    public async Task Unknown_host_counts_as_offline()
    {
        var spec = new MonitoredServerSpec
        {
            Name = "Missing server",
            HostName = "no-such-host.invalid"
        };

        var status = await _monitor.ProbeOneAsync(spec, CancellationToken.None);
        Assert.False(status.Online);
        Assert.Equal("Offline", status.Status);
        Assert.Equal("Missing server", status.Name);
        Assert.Equal("no-such-host.invalid", status.HostName);
    }

    [Fact]
    public async Task Reports_name_host_and_ip_for_loopback()
    {
        var status = await _monitor.ProbeOneAsync(new MonitoredServerSpec
        {
            Name = "Dashboard host",
            HostName = "localhost",
            IpAddress = "127.0.0.1",
            Role = "This website"
        }, CancellationToken.None);

        Assert.True(status.Online);
        Assert.Equal("Online", status.Status);
        Assert.Equal("Dashboard host", status.Name);
        Assert.Equal("localhost", status.HostName);
        Assert.Equal("127.0.0.1", status.IpAddress);
    }
}
