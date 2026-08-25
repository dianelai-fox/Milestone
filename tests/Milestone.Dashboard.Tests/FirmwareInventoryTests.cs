using Milestone.Dashboard.Models;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class FirmwareInventoryTests
{
    [Fact]
    public void Summarizes_outdated_vulnerable_and_upgrade_counts()
    {
        var overview = FirmwareInventory.FromCameras(
        [
            Camera("c01", "Lobby", "Building A", "11.11.65", "11.11", null, null),
            Camera("c02", "Dock", "Building A", "8.40.1", "8.40", "EOS", "High"),
            Camera("c03", "Gate", "Campus", "11.8.61", "11.11", "EOL", "Medium"),
            Camera("c04", "Vault", "Campus", null, null, null, null),
            Camera("c05", "Lot", "Parking", "V5.7.15", "5.7", null, null)
        ]);

        Assert.Equal(5, overview.TotalDevices);
        Assert.Equal(1, overview.CompliantCount);
        Assert.Equal(3, overview.NonCompliantCount);
        Assert.Equal(1, overview.NaCount);
        Assert.Equal(25, overview.OverallCompliancePercent);
        Assert.Equal(1, overview.CompliantVersionCount);
        Assert.Equal(2, overview.VulnerableCount);
        Assert.Equal(2, overview.AvailableUpgradeCount);
        Assert.Equal(0, overview.CompliantSites);
        Assert.Equal(3, overview.NonCompliantSites);

        var building = overview.TopAlertedSites.Single(site => site.Site == "Building A");
        Assert.Equal(1, building.Alerted);
        Assert.Equal(50, building.RiskPercent);
        Assert.Contains(overview.TopNonCompliantModels, slice => slice.Label == "AXIS P3245-LVE" && slice.Count == 2);
        Assert.Contains(overview.TopNonCompliantModels, slice => slice.Label == "AXIS P3225-LV Mk II" && slice.Count == 1);
        Assert.Equal("Camera", Assert.Single(overview.TopNonCompliantTypes).Label);
        Assert.True(overview.Details.Count >= 3);
    }

    [Fact]
    public void Eos_is_outdated_even_when_firmware_matches_suggested()
    {
        var camera = Camera("c01", "Dock", "Building A", "8.40.1", "8.40", "EOS", "High");

        Assert.True(FirmwareInventory.IsOutdated(camera));
        Assert.False(FirmwareInventory.HasUpgrade(camera));
        Assert.True(FirmwareInventory.IsVulnerable(camera));
    }

    [Fact]
    public void Empty_inventory_is_all_zeros()
    {
        var overview = FirmwareInventory.FromCameras([]);

        Assert.Equal(0, overview.TotalDevices);
        Assert.Equal(0, overview.OverallCompliancePercent);
        Assert.Empty(overview.TopAlertedSites);
        Assert.Empty(overview.Details);
    }

    private static CameraInfo Camera(
        string id,
        string name,
        string site,
        string? firmware,
        string? suggested,
        string? lifecycle,
        string? vulnerability) =>
        new()
        {
            Id = id,
            Name = name,
            Site = site,
            Vendor = "Axis",
            Model = name == "Dock" ? "AXIS P3225-LV Mk II" : "AXIS P3245-LVE",
            Firmware = firmware,
            Intelligence = new DeviceIntelligence
            {
                SuggestedFirmware = suggested,
                LifecycleStatus = lifecycle,
                VulnerabilitySeverity = vulnerability
            }
        };
}
