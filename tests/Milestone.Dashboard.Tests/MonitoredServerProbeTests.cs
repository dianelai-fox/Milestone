using Milestone.Dashboard.Options;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class MonitoredServerProbeTests
{
    [Fact]
    public void Accepts_hostnames_and_ipv4_addresses()
    {
        Assert.True(MonitoredServerProbe.IsValidHost("LENELNEWAPP.INT.APPS.FOX"));
        Assert.True(MonitoredServerProbe.IsValidHost("10.20.30.11"));
        Assert.True(MonitoredServerProbe.IsValidHost("2001:db8::1"));
        Assert.False(MonitoredServerProbe.IsValidHost("bad host"));
        Assert.False(MonitoredServerProbe.IsValidHost(""));
    }

    [Fact]
    public async Task Demo_mode_marks_second_server_offline()
    {
        var probe = new MonitoredServerProbe();
        var overview = await probe.ProbeAsync(MonitoredServerCatalog.DemoServers(), demoOnly: true, CancellationToken.None);

        Assert.Equal("demo", overview.Source);
        Assert.Equal(3, overview.TotalServers);
        Assert.Equal(2, overview.OnlineCount);
        Assert.Equal(1, overview.OfflineCount);
        Assert.Contains(overview.Servers, server => !server.Online && server.Name == "APP-BADGE-02");
        Assert.All(overview.Servers, server =>
        {
            Assert.False(string.IsNullOrWhiteSpace(server.HostName));
            Assert.False(string.IsNullOrWhiteSpace(server.IpAddress));
        });
    }

    [Fact]
    public void Probe_targets_prefer_ip_then_hostname_then_name()
    {
        var spec = new MonitoredServerSpec
        {
            Name = "FOXUSWDMSIA297",
            HostName = "LENELNEWAPP.INT.APPS.FOX",
            IpAddress = "10.1.2.3"
        };

        Assert.Equal(["10.1.2.3", "LENELNEWAPP.INT.APPS.FOX", "FOXUSWDMSIA297"], spec.ProbeTargets());
    }
}
