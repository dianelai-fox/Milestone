using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class ManagedServerMonitorTests
{
    [Theory]
    [InlineData("FOXUSWDMSIA297", true)]
    [InlineData("rec01.campus.local", true)]
    [InlineData("bad host", false)]
    [InlineData("host;calc.exe", false)]
    [InlineData("", false)]
    public void Accepts_only_safe_host_names(string host, bool expected)
    {
        Assert.Equal(expected, ManagedServerMonitor.IsValidHost(host));
    }

    [Fact]
    public void Parses_windows_logical_disk_json()
    {
        const string json = """
            [
              { "DeviceID": "C:", "Size": 536870912000, "FreeSpace": 322122547200 },
              { "DeviceID": "D:", "Size": 107374182400, "FreeSpace": 10737418240 }
            ]
            """;

        var reading = ManagedServerMonitor.ParseDiskJson(json);
        Assert.NotNull(reading);
        Assert.Equal(2, reading.VolumeCount);
        Assert.Equal(512000 + 102400, reading.MaxSizeMb);
        Assert.Equal(90, reading.WorstVolumeUsagePercent);
        Assert.Equal("Critical", Milestone.Dashboard.Models.ServerHealth.Storage(reading.WorstVolumeUsagePercent));
    }
}
