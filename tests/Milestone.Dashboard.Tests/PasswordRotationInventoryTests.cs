using Milestone.Dashboard.Models;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class PasswordRotationInventoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Summarizes_password_rotation_like_securithings()
    {
        var overview = PasswordRotationInventory.FromCameras(
        [
            Camera("c01", "Lobby", "Building A", "Up To Date", Now.AddDays(140), "root"),
            Camera("c02", "Dock", "Building A", "Overdue", Now.AddDays(-10), "root"),
            Camera("c03", "Gate", "Campus", "Never Rotated", null, "viewer"),
            Camera("c04", "Vault", "Campus", null, null, null),
            Camera("c05", "Lot", "Parking", "Due Soon", Now.AddDays(12), "operator")
        ], Now);

        Assert.Equal(5, overview.TotalDevices);
        Assert.Equal(2, overview.CompliantCount);
        Assert.Equal(2, overview.NonCompliantCount);
        Assert.Equal(1, overview.NaCount);
        Assert.Equal(50, overview.OverallCompliancePercent);
        Assert.Equal(1, overview.UpToDateCount);
        Assert.Equal(1, overview.NeverRotatedCount);
        Assert.Equal(1, overview.ExpiredCount);
        Assert.Equal(1, overview.SoonCount);
        Assert.Equal(1, overview.CompliantSites);
        Assert.Equal(2, overview.NonCompliantSites);

        var building = overview.TopAlertedSites.Single(site => site.Site == "Building A");
        Assert.Equal(1, building.Alerted);
        Assert.Equal(50, building.RiskPercent);

        Assert.Equal("viewer", overview.NonCompliantByUserType.Single(slice => slice.Label == "viewer").Label);
        Assert.Contains(overview.NonCompliantByUserType, slice => slice.Label == "admin" && slice.Count == 1);
        Assert.Equal("Camera", Assert.Single(overview.NonCompliantByDeviceType).Label);
        Assert.Equal(1, overview.ExpirationBreakdown.Single(item => item.Label == "180 days").Count);
        Assert.Equal(1, overview.ExpirationBreakdown.Single(item => item.Label == "90 days").Count);
        Assert.Equal(1, overview.ExpirationBreakdown.Single(item => item.Label == "Expired").Count);
    }

    [Fact]
    public void Empty_inventory_is_all_zeros()
    {
        var overview = PasswordRotationInventory.FromCameras([]);

        Assert.Equal(0, overview.TotalDevices);
        Assert.Equal(0, overview.OverallCompliancePercent);
        Assert.Empty(overview.TopAlertedSites);
    }

    [Theory]
    [InlineData("root", "admin")]
    [InlineData("administrator", "admin")]
    [InlineData("operator", "operator")]
    [InlineData("viewer", "viewer")]
    [InlineData("", "unknown")]
    public void Classifies_hardware_user_types(string user, string expected)
    {
        Assert.Equal(expected, PasswordRotationInventory.UserType(new CameraInfo
        {
            Id = "c1",
            Name = "Cam",
            HardwareUserName = string.IsNullOrEmpty(user) ? null : user
        }));
    }

    private static CameraInfo Camera(
        string id,
        string name,
        string site,
        string? status,
        DateTimeOffset? expiry,
        string? user) =>
        new()
        {
            Id = id,
            Name = name,
            Site = site,
            HardwareUserName = user,
            Intelligence = new DeviceIntelligence
            {
                PasswordExpiryStatus = status,
                PasswordExpiryDate = expiry
            }
        };
}
