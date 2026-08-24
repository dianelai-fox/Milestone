using Milestone.Dashboard.Models;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class LifecycleInventoryTests
{
    [Fact]
    public void Summarizes_compliance_from_lifecycle_status()
    {
        var overview = LifecycleInventory.FromCameras(
        [
            Camera("c01", "Lobby", "Building A", "Active", new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero), "Axis", "P3265-LVE"),
            Camera("c02", "Dock", "Building A", "EOL", new DateTimeOffset(2029, 1, 1, 0, 0, 0, TimeSpan.Zero), "Axis", "P3245-LVE"),
            Camera("c03", "Gate", "Campus", "EOS", new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero), "Axis", "P3225-LV Mk II"),
            Camera("c04", "Vault", "Campus", null, null, "Hanwha", "XNV-8083R"),
            Camera("c05", "Lot", "Parking", "EOS", new DateTimeOffset(2021, 10, 31, 0, 0, 0, TimeSpan.Zero), "Axis", "P3225-LV")
        ]);

        Assert.Equal(5, overview.TotalDevices);
        Assert.Equal(2, overview.CompliantCount);
        Assert.Equal(2, overview.NonCompliantCount);
        Assert.Equal(1, overview.NaCount);
        Assert.Equal(50, overview.OverallCompliancePercent);
        Assert.Equal(1, overview.CurrentProductCount);
        Assert.Equal(1, overview.EolCount);
        Assert.Equal(2, overview.EosCount);
        Assert.Equal(3, overview.TotalSites);
        Assert.Equal(1, overview.CompliantSites);
        Assert.Equal(2, overview.NonCompliantSites);

        var campus = overview.TopAlertedSites.Single(site => site.Site == "Campus");
        Assert.Equal(1, campus.Eos);
        Assert.Equal(0, campus.Eol);
        Assert.Equal(50, campus.RiskPercent);

        Assert.Equal(2, overview.TopNonCompliantModels.Count);
        Assert.Contains(overview.TopNonCompliantModels, slice => slice.Label == "Axis P3225-LV Mk II" && slice.Count == 1);
        Assert.Equal("Camera", Assert.Single(overview.TopNonCompliantTypes).Label);
        Assert.Equal([2021, 2025, 2029, 2031], overview.EosByYear.Select(item => item.Year).ToArray());
        Assert.Equal("Axis P3245-LVE", Assert.Single(overview.TopEolModels).Label);
    }

    [Fact]
    public void Empty_inventory_is_all_zeros()
    {
        var overview = LifecycleInventory.FromCameras([]);

        Assert.Equal(0, overview.TotalDevices);
        Assert.Equal(0, overview.OverallCompliancePercent);
        Assert.Equal(0, overview.TotalSites);
        Assert.Empty(overview.TopAlertedSites);
        Assert.Empty(overview.EosByYear);
    }

    [Fact]
    public void Classifies_non_camera_device_types_from_name()
    {
        Assert.Equal("Access Control Panel", LifecycleInventory.DeviceType(new CameraInfo
        {
            Id = "d1",
            Name = "Lobby Door Controller",
            Model = "Aperio AH30"
        }));
        Assert.Equal("Encoder", LifecycleInventory.DeviceType(new CameraInfo
        {
            Id = "d2",
            Name = "Rack 1",
            Model = "Axis Q7414 Encoder"
        }));
        Assert.Equal("Camera", LifecycleInventory.DeviceType(new CameraInfo
        {
            Id = "d3",
            Name = "Lobby Main",
            Model = "AXIS P3245-LVE"
        }));
    }

    private static CameraInfo Camera(
        string id,
        string name,
        string site,
        string? lifecycle,
        DateTimeOffset? eos,
        string vendor,
        string model) =>
        new()
        {
            Id = id,
            Name = name,
            Site = site,
            Vendor = vendor,
            Model = model,
            Intelligence = new DeviceIntelligence
            {
                LifecycleStatus = lifecycle,
                EosDate = eos
            }
        };
}
