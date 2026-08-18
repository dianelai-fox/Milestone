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

        Assert.NotNull(snapshot.Cameras[0].Location);
        Assert.Equal(34.05, snapshot.Cameras[0].Location.Latitude);
        Assert.Equal(-118.25, snapshot.Cameras[0].Location.Longitude);
        Assert.True(snapshot.Cameras[0].LocationIsOverride);
        Assert.Equal("Main Campus", snapshot.Cameras[0].Site);
        Assert.Equal(34.06, snapshot.Cameras[1].Location!.Latitude);
        Assert.True(snapshot.Cameras[1].LocationIsOverride);
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
    }
}
