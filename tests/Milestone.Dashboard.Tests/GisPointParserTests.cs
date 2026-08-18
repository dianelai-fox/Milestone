using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class GisPointParserTests
{
    [Theory]
    [InlineData("POINT (12.3773200400488 55.6580462362318)", 12.3773200400488, 55.6580462362318, null)]
    [InlineData("POINT (55.656932878513 12.3763545558449 18.5)", 55.656932878513, 12.3763545558449, 18.5)]
    [InlineData("point (-118.2569 34.0522)", -118.2569, 34.0522, null)]
    public void Parse_reads_milestone_gis_points(string value, double longitude, double latitude, double? altitude)
    {
        var location = GisPointParser.Parse(value);

        Assert.NotNull(location);
        Assert.Equal(longitude, location!.Longitude, 6);
        Assert.Equal(latitude, location.Latitude, 6);
        Assert.Equal(altitude, location.Altitude);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("POINT EMPTY")]
    [InlineData("not-a-point")]
    public void Parse_returns_null_for_empty_or_invalid_values(string? value)
    {
        Assert.Null(GisPointParser.Parse(value));
    }
}
