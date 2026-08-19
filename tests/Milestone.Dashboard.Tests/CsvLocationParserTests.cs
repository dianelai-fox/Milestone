using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class CsvLocationParserTests
{
    [Fact]
    public void Parse_skips_empty_coordinates_and_reads_filled_rows()
    {
        const string csv = """
            cameraId,name,latitude,longitude,site
            f28c87bd-8b7b-4e17-b102-0c8e9c308155,10271 PICO-CE101-PRM-EAST CORNER,,,FOXUSWDMSAP663
            11a35d10-1a09-4754-8f29-051d38787368,2121 AOTS-C0201-INT-MAIN LOBBY INTERCOM,34.054244,-118.414072,FOXUSWDMSAP663
            """;

        var items = CsvLocationParser.Parse(csv);

        var item = Assert.Single(items);
        Assert.Equal("11a35d10-1a09-4754-8f29-051d38787368", item.CameraId);
        Assert.Equal(34.054244, item.Latitude);
        Assert.Equal(-118.414072, item.Longitude);
        Assert.Equal("FOXUSWDMSAP663", item.Site);
    }
}
