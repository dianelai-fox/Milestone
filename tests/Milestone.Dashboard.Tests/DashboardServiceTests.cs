using Milestone.Dashboard.Models;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class DashboardServiceTests
{
    [Fact]
    public void ApplyOverrides_replaces_missing_or_existing_coordinates()
    {
        var snapshot = new DashboardSnapshot
        {
            Cameras =
            [
                new CameraInfo { Id = "c01", Name = "Lobby", Site = "Building A" },
                new CameraInfo { Id = "c02", Name = "Dock", Location = new CameraLocation(1, 2) }
            ]
        };

        var overrides = new Dictionary<string, LocationOverrideRequest>(StringComparer.OrdinalIgnoreCase)
        {
            ["c01"] = new() { CameraId = "c01", Latitude = 34.05, Longitude = -118.25, Site = "Main Campus" },
            ["c02"] = new() { CameraId = "c02", Latitude = 34.06, Longitude = -118.26 }
        };

        DashboardService.ApplyOverrides(snapshot, overrides);

        var lobby = snapshot.Cameras[0].Location;
        Assert.NotNull(lobby);
        Assert.Equal(34.05, lobby.Latitude);
        Assert.Equal(-118.25, lobby.Longitude);
        Assert.True(snapshot.Cameras[0].LocationIsOverride);
        Assert.Equal("Main Campus", snapshot.Cameras[0].Site);
        var dock = snapshot.Cameras[1].Location;
        Assert.NotNull(dock);
        Assert.Equal(34.06, dock.Latitude);
        Assert.True(snapshot.Cameras[1].LocationIsOverride);
    }

    [Fact]
    public void ApplyOverrides_uses_site_name_and_address_from_import()
    {
        var snapshot = new DashboardSnapshot
        {
            Cameras =
            [
                new CameraInfo { Id = "c01", Name = "Lobby", Site = "Building A" }
            ]
        };

        DashboardService.ApplyOverrides(snapshot, new Dictionary<string, LocationOverrideRequest>(StringComparer.OrdinalIgnoreCase)
        {
            ["c01"] = new()
            {
                CameraId = "c01",
                Latitude = 34.05,
                Longitude = -118.25,
                Site = "FOXUSWDMSAP663",
                Address = "10201 W Pico Blvd, Los Angeles, CA 90064, USA",
                SiteName = "Fox Studio Lot"
            }
        });

        var camera = snapshot.Cameras[0];
        Assert.Equal("Fox Studio Lot", camera.Site);
        Assert.Equal("10201 W Pico Blvd, Los Angeles, CA 90064, USA", camera.Address);
        Assert.Equal("FOXUSWDMSAP663", camera.CustomProperties["SiteCode"]);
        Assert.Contains("FOXUSWDMSAP663", camera.Labels);
    }

    [Fact]
    public void ApplyOverrides_accepts_cameras_with_missing_custom_properties()
    {
        var snapshot = new DashboardSnapshot
        {
            Cameras =
            [
                new CameraInfo { Id = "c01", Name = "Lobby", CustomProperties = null!, Labels = null! }
            ]
        };

        DashboardService.ApplyOverrides(snapshot, new Dictionary<string, LocationOverrideRequest>(StringComparer.OrdinalIgnoreCase)
        {
            ["c01"] = new()
            {
                CameraId = "c01",
                Latitude = 34.05,
                Longitude = -118.25,
                Address = "10201 W Pico Blvd"
            }
        });

        Assert.Equal("10201 W Pico Blvd", snapshot.Cameras[0].Address);
    }
}

public class DemoVmsClientTests
{
    [Fact]
    public async Task Demo_catalog_includes_mapped_cameras_and_storage()
    {
        var snapshot = await new DemoVmsClient().GetSnapshotAsync(CancellationToken.None);

        Assert.Equal("demo", snapshot.Source);
        Assert.True(snapshot.Cameras.Count >= 10);
        Assert.Contains(snapshot.Cameras, camera => camera.Location is not null);
        Assert.Contains(snapshot.Cameras, camera => camera.Location is null);
        Assert.NotEmpty(snapshot.Storages);
        Assert.True(snapshot.Summary.StorageUsagePercent > 0);
        Assert.Equal(snapshot.Storages.Sum(item => item.UsedSpaceMb), snapshot.Summary.UsedSpaceMb);
        Assert.Contains(snapshot.Cameras, camera => !string.IsNullOrWhiteSpace(camera.Firmware));
        Assert.Contains(snapshot.Cameras, camera => camera.Labels.Count > 0);
        Assert.Contains(snapshot.Cameras, camera => camera.CustomProperties.ContainsKey("Owner"));
        Assert.Contains(snapshot.Cameras, camera => camera.PtzEnabled == true);
    }
}
