using Milestone.Dashboard.Models;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class SiteInventoryTests
{
    [Fact]
    public void Groups_cameras_into_sites_with_status_and_device_counts()
    {
        var sites = SiteInventory.FromCameras(
        [
            Camera("c01", "Lobby", "Building A", true, new CameraLocation(-118.25, 34.05), "Active", null),
            Camera("c02", "Vault", "Building A", true, null, "Active", null),
            Camera("c03", "Dock", "Warehouse", false, new CameraLocation(-118.24, 34.04), "EOS", "High"),
            Camera("c04", "Unset", null, true, null, "EOL", "Medium")
        ]);

        Assert.Equal(3, sites.Count);

        var building = sites.Single(site => site.Name == "Building A");
        Assert.Equal("Partial", building.Status);
        Assert.Equal(2, building.ManagedCount);
        Assert.Equal(2, building.EnabledCount);
        Assert.Equal(1, building.UnmappedCount);
        Assert.Equal("Building A camera", building.Description);
        Assert.NotNull(building.Location);
        Assert.Equal(34.05, building.Location!.Latitude, 2);

        var warehouse = sites.Single(site => site.Name == "Warehouse");
        Assert.Equal("Disconnected", warehouse.Status);
        Assert.Equal(1, warehouse.DisabledCount);
        Assert.Equal(1, warehouse.EosCount);
        Assert.Equal(1, warehouse.HighVulnCount);
        Assert.Equal(1, warehouse.OutdatedFirmwareCount);

        var unassigned = sites.Single(site => site.Name == "Unassigned");
        Assert.Equal("N/A", unassigned.Status);
        Assert.Equal(1, unassigned.EolCount);
        Assert.Equal(1, unassigned.MediumVulnCount);
    }

    [Fact]
    public void Connected_site_requires_every_camera_enabled_and_mapped()
    {
        var sites = SiteInventory.FromCameras(
        [
            Camera("c01", "Gate", "Campus", true, new CameraLocation(-118.25, 34.05), "Active", null),
            Camera("c02", "Lot", "Campus", true, new CameraLocation(-118.26, 34.06), "Active", null)
        ]);

        var campus = Assert.Single(sites);
        Assert.Equal("Connected", campus.Status);
        Assert.Equal(2, campus.ManagedCount);
        Assert.Equal(0, campus.UnmappedCount);
        Assert.Contains("Perimeter", campus.Labels);
    }

    private static CameraInfo Camera(
        string id,
        string name,
        string? site,
        bool enabled,
        CameraLocation? location,
        string? lifecycle,
        string? vulnerability) =>
        new()
        {
            Id = id,
            Name = name,
            Description = site is null ? null : $"{site} camera",
            Enabled = enabled,
            Site = site,
            Location = location,
            Firmware = vulnerability == "High" ? "1.0" : "11.11.65",
            Labels = site is null ? [] : [$"{site} / Perimeter"],
            Intelligence = new DeviceIntelligence
            {
                LifecycleStatus = lifecycle,
                VulnerabilitySeverity = vulnerability,
                SuggestedFirmware = "11.11.65"
            }
        };
}
