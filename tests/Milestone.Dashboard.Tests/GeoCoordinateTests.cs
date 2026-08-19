using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class GeoCoordinateTests
{
    [Fact]
    public void TryNormalize_repairs_missing_decimal_in_st_louis_longitude()
    {
        Assert.True(GeoCoordinate.TryNormalize(38.627827, -90189505, out var latitude, out var longitude));
        Assert.Equal(38.627827, latitude, 6);
        Assert.Equal(-90.189505, longitude, 6);
    }

    [Fact]
    public void TryNormalize_rejects_values_that_cannot_be_repaired()
    {
        Assert.False(GeoCoordinate.TryNormalize(999, 99999, out _, out _));
    }
}
