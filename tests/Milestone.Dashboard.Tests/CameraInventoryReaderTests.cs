using System.Text.Json;
using Milestone.Dashboard.Models;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class CameraInventoryReaderTests
{
    [Fact]
    public void Hardware_settings_extract_firmware_serial_mac_and_model()
    {
        using var document = JsonDocument.Parse("""
            {
              "displayName": "Settings",
              "hardwareDriverSettings": {
                "detectedModelName": "AXIS Q1647 Network Camera",
                "firmwareVersion": "9.80.3.8",
                "serialNumber": "ACC123456",
                "macAddress": "00:40:8C:12:34:56",
                "brightness": "50"
              },
              "relations": {
                "parent": { "type": "hardware", "id": "hw-1" }
              }
            }
            """);

        var details = HardwareSettingsReader.Read(document.RootElement);

        Assert.Equal("hw-1", HardwareSettingsReader.ReadParentHardwareId(document.RootElement));
        Assert.Equal("AXIS Q1647 Network Camera", details.Model);
        Assert.Equal("9.80.3.8", details.Firmware);
        Assert.Equal("ACC123456", details.SerialNumber);
        Assert.Equal("00:40:8C:12:34:56", details.MacAddress);
    }

    [Fact]
    public void Camera_groups_map_nested_labels_to_camera_ids()
    {
        using var document = JsonDocument.Parse("""
            {
              "array": [
                {
                  "id": "g-root",
                  "displayName": "Building A",
                  "cameras": [{ "id": "c-lobby" }],
                  "cameraGroups": [
                    {
                      "id": "g-lobby",
                      "displayName": "Lobby",
                      "cameras": [{ "id": "c-lobby" }, { "id": "c-desk" }]
                    }
                  ]
                }
              ]
            }
            """);

        var labels = CameraGroupIndex.Build(document.RootElement.GetProperty("array").EnumerateArray());

        Assert.Contains("Building A", labels["c-lobby"]);
        Assert.Contains("Building A / Lobby", labels["c-lobby"]);
        Assert.Equal(["Building A / Lobby"], labels["c-desk"]);
    }

    [Fact]
    public void Custom_properties_read_name_value_and_property_maps()
    {
        using var document = JsonDocument.Parse("""
            {
              "customProperties": [
                {
                  "displayName": "Custom properties",
                  "properties": { "Owner": "Physical Security", "Zone": "Studio" }
                },
                { "name": "Badge", "value": "Restricted" }
              ]
            }
            """);

        var properties = CustomPropertyReader.Read(document.RootElement);

        Assert.Equal("Physical Security", properties["Owner"]);
        Assert.Equal("Studio", properties["Zone"]);
        Assert.Equal("Restricted", properties["Badge"]);
    }

    [Theory]
    [InlineData("AXIS P3245-LVE", "AXIS P32 Series", "Axis", "P3245-LVE")]
    [InlineData("Hanwha PNV-A6081R", "Hanwha Wisenet", "Hanwha", "PNV-A6081R")]
    [InlineData("Bosch FLEXIDOME 5100i", null, "Bosch", "FLEXIDOME 5100i")]
    public void Identity_reads_vendor_and_strips_it_from_the_model(string model, string? driver, string vendor, string display)
    {
        Assert.Equal(vendor, CameraIdentity.Vendor(model, driver));
        Assert.Equal(display, CameraIdentity.DisplayModel(model, vendor));
    }

    [Theory]
    [InlineData("http://10.208.5.119/", "10.208.5.119")]
    [InlineData("http://10.208.5.94:80/onvif", "10.208.5.94")]
    [InlineData("10.10.1.11", "10.10.1.11")]
    public void Identity_reads_the_device_ip_from_the_hardware_address(string address, string expected)
    {
        Assert.Equal(expected, CameraIdentity.Host(address));
    }

    [Fact]
    public void Intelligence_marks_unsupported_axis_models_eos()
    {
        var camera = new CameraInfo
        {
            Id = "c1",
            Name = "Lobby",
            Vendor = "Axis",
            Model = "AXIS P3225-LV Mk II",
            Firmware = "8.40.31",
            PasswordLastModified = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero)
        };

        var intel = DeviceIntelligenceCatalog.Evaluate(camera, new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("EOS", intel.LifecycleStatus);
        Assert.Equal("High", intel.VulnerabilitySeverity);
        Assert.Equal("Compliant", intel.NdaaStatus);
        Assert.Equal("8.40", intel.SuggestedFirmware);
        Assert.Contains("P3275", intel.ReplacementModel);
        Assert.Equal("Up To Date", intel.PasswordExpiryStatus);
    }

    [Fact]
    public void Intelligence_marks_hikvision_ndaa_restricted()
    {
        var camera = new CameraInfo
        {
            Id = "c2",
            Name = "Dock",
            Vendor = "Hikvision",
            Model = "Hikvision DS-2CD2686G2"
        };

        var intel = DeviceIntelligenceCatalog.Evaluate(camera, new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("Restricted", intel.NdaaStatus);
        Assert.Null(intel.SslExpiryStatus);
    }

    [Fact]
    public void Intelligence_maps_recording_storage_sd_and_http_ssl()
    {
        var camera = new CameraInfo
        {
            Id = "c3",
            Name = "Lot",
            Enabled = true,
            HardwareAddress = "http://10.208.5.119/",
            RecordingEnabled = true,
            RecordingServerName = "FOXUSWDMSAP681",
            EdgeStorageEnabled = false
        };

        var intel = DeviceIntelligenceCatalog.Evaluate(camera);

        Assert.Equal("Non Compliant", intel.SslCompliance);
        Assert.Equal("Recording", intel.RecordingStatus);
        Assert.Equal("FOXUSWDMSAP681", intel.StorageServer);
        Assert.Equal("Disconnected", intel.SdStatus);
        Assert.Equal("No Disk", intel.SdWearStatus);
        Assert.Null(intel.Dot1xStatus);
        Assert.Null(intel.LastHardened);
    }
}
