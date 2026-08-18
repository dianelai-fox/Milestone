using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Tests;

public class StorageMetricsTests
{
    [Theory]
    [InlineData(0, 1000, 0)]
    [InlineData(250, 1000, 25)]
    [InlineData(1000, 1000, 100)]
    [InlineData(1500, 1000, 150)]
    [InlineData(10, 0, 0)]
    public void UsagePercent_handles_normal_and_edge_cases(long used, long max, double expected)
    {
        Assert.Equal(expected, StorageMetrics.UsagePercent(used, max));
    }

    [Theory]
    [InlineData(512, "512 MB")]
    [InlineData(2048, "2.0 GB")]
    [InlineData(2_097_152, "2.00 TB")]
    public void FormatSize_uses_readable_units(long megaBytes, string expected)
    {
        Assert.Equal(expected, StorageMetrics.FormatSize(megaBytes));
    }

    [Theory]
    [InlineData(0, "Not set")]
    [InlineData(45, "45 minutes")]
    [InlineData(120, "2 hours")]
    [InlineData(1440, "1 day")]
    [InlineData(10080, "7 days")]
    public void FormatRetention_converts_minutes(int minutes, string expected)
    {
        Assert.Equal(expected, StorageMetrics.FormatRetention(minutes));
    }
}
