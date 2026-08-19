using System.Text.Json;
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
}
