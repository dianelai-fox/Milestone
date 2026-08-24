using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Milestone.Dashboard.Tests;

public class DashboardApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DashboardApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_endpoint_reports_demo_source()
    {
        var response = await _client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("demo", document.RootElement.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Dashboard_endpoint_returns_cameras_and_storage()
    {
        var response = await _client.GetAsync("/api/dashboard");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("cameras").GetArrayLength() > 0);
        var camera = document.RootElement.GetProperty("cameras").EnumerateArray().First();
        Assert.False(string.IsNullOrWhiteSpace(camera.GetProperty("firmware").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(camera.GetProperty("vendor").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(camera.GetProperty("ipAddress").GetString()));
        Assert.Equal("Demo", camera.GetProperty("deviceSource").GetString());
        Assert.Equal("Compliant", camera.GetProperty("intelligence").GetProperty("ndaaStatus").GetString());
        Assert.Equal("Recording", camera.GetProperty("intelligence").GetProperty("recordingStatus").GetString());
        Assert.Equal("Disconnected", camera.GetProperty("intelligence").GetProperty("sdStatus").GetString());
        Assert.False(string.IsNullOrWhiteSpace(camera.GetProperty("intelligence").GetProperty("suggestedFirmware").GetString()));
        Assert.True(camera.GetProperty("labels").GetArrayLength() > 0);
        Assert.True(camera.GetProperty("customProperties").GetProperty("Owner").GetString()?.Length > 0);
        Assert.True(document.RootElement.GetProperty("storages").GetArrayLength() > 0);
        Assert.True(document.RootElement.GetProperty("summary").GetProperty("cameraCount").GetInt32() > 0);
        Assert.True(document.RootElement.GetProperty("sites").GetArrayLength() > 0);
        var site = document.RootElement.GetProperty("sites").EnumerateArray().First();
        Assert.False(string.IsNullOrWhiteSpace(site.GetProperty("name").GetString()));
        Assert.True(site.GetProperty("managedCount").GetInt32() > 0);
        Assert.True(document.RootElement.GetProperty("mapCenter").TryGetProperty("latitude", out _));
    }

    [Fact]
    public async Task Location_override_rejects_invalid_coordinates()
    {
        var response = await _client.PostAsJsonAsync("/api/locations", new
        {
            cameraId = "c01",
            latitude = 200,
            longitude = -118.25
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Location_override_is_applied_to_dashboard()
    {
        var save = await _client.PostAsJsonAsync("/api/locations", new
        {
            cameraId = "c10",
            latitude = 34.1111,
            longitude = -118.2222,
            site = "Override Site"
        });
        save.EnsureSuccessStatusCode();

        using var dashboard = JsonDocument.Parse(await _client.GetStringAsync("/api/dashboard"));
        var camera = dashboard.RootElement.GetProperty("cameras").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "c10");

        Assert.True(camera.GetProperty("locationIsOverride").GetBoolean());
        Assert.Equal(34.1111, camera.GetProperty("location").GetProperty("latitude").GetDouble(), 4);
        Assert.Equal("Override Site", camera.GetProperty("site").GetString());
    }

    [Fact]
    public async Task Home_page_is_served()
    {
        var response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("XProtect Operations", html);
        Assert.Contains("Select camera to place", html);
        Assert.Contains("Sites view", html);
        Assert.Contains("Firmware", html);
        Assert.Contains("Device Name", html);
        Assert.Contains("Last Seen", html);
        Assert.Contains("NDAA Status", html);
        Assert.Contains("Lifecycle Status", html);
        Assert.Contains("Recording Status", html);
        Assert.Contains("Storage Server", html);
        Assert.Contains("SD Status", html);
        Assert.Contains("id=\"view-dashboard\"", html);
        Assert.Contains("id=\"view-devices\"", html);
        Assert.Contains("id=\"view-manage\"", html);
        Assert.Contains("id=\"view-storage\"", html);
        Assert.Contains("id=\"storage-pies\"", html);
        Assert.Contains("storage-pie-grid", html);
        Assert.Contains("class=\"nav-label\"", html);
        Assert.Contains("Manage sites", html);
        Assert.Contains("Sites View", html);
        Assert.Contains("Firmware Vulnerabilities", html);
        Assert.Contains("id=\"inventory-panel\"", html);
        Assert.Contains("server-body", html);
        Assert.Contains("Recording server", html);
        Assert.Contains("Device Management", html);
        Assert.Contains("All Devices", html);
        Assert.Contains("selection-summary", html);
        Assert.Contains("page-nav", html);
        Assert.Contains("100", html);
        Assert.Contains("Group", html);
        Assert.Contains("/lib/leaflet/leaflet.js", html);
    }

    [Fact]
    public async Task Sidebar_label_stylesheet_uses_a_readable_font()
    {
        var response = await _client.GetAsync("/css/site.css");
        response.EnsureSuccessStatusCode();
        var css = await response.Content.ReadAsStringAsync();
        Assert.Contains(".nav-label", css);
        Assert.Contains("Calibri", css);
        Assert.Contains("color: #fff", css);
        Assert.Contains(".site-status", css);
        Assert.Contains("#22c55e", css);
    }

    [Fact]
    public async Task Location_import_matches_cameras_by_name()
    {
        var response = await _client.PostAsJsonAsync("/api/locations/import", new[]
        {
            new { name = "Archive Vault", latitude = 34.1234, longitude = -118.4321, site = "Imported" }
        });
        response.EnsureSuccessStatusCode();

        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, result.RootElement.GetProperty("saved").GetInt32());

        using var dashboard = JsonDocument.Parse(await _client.GetStringAsync("/api/dashboard"));
        var camera = dashboard.RootElement.GetProperty("cameras").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "c20");
        Assert.True(camera.GetProperty("locationIsOverride").GetBoolean());
        Assert.Equal("Imported", camera.GetProperty("site").GetString());
    }

    [Fact]
    public async Task Location_csv_import_saves_rows_with_camera_ids()
    {
        using var content = new MultipartFormDataContent();
        const string csv = """
            cameraId,name,latitude,longitude,site,address,Site_Name
            c10,Server Room,34.054244,-118.414072,FOXUSWDMSAP663,"10201 W Pico Blvd, Los Angeles, CA 90064, USA",Fox Studio Lot
            unknown,Missing Camera,,,
            stl,SPORTS-STL-C1201,38.627827,-90189505,FOXUSWDMSAP663,"1 Cardinal Way, St. Louis, MO",Sports STL
            """;
        content.Add(new StringContent(csv), "file", "camera-locations.csv");

        var response = await _client.PostAsync("/api/locations/import-csv", content);
        response.EnsureSuccessStatusCode();

        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, result.RootElement.GetProperty("saved").GetInt32());

        using var dashboard = JsonDocument.Parse(await _client.GetStringAsync("/api/dashboard"));
        var camera = dashboard.RootElement.GetProperty("cameras").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "c10");
        Assert.Equal(34.054244, camera.GetProperty("location").GetProperty("latitude").GetDouble(), 5);
        Assert.Equal("Fox Studio Lot", camera.GetProperty("site").GetString());
        Assert.Equal("10201 W Pico Blvd, Los Angeles, CA 90064, USA", camera.GetProperty("address").GetString());
        var site = dashboard.RootElement.GetProperty("sites").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Fox Studio Lot");
        Assert.Contains("Pico", site.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Location_template_includes_address_and_site_name_columns()
    {
        var response = await _client.GetAsync("/api/locations/template");
        response.EnsureSuccessStatusCode();
        var csv = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("cameraId,name,latitude,longitude,site,address,Site_Name", csv);
    }
}
