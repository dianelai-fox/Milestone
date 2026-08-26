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
        var lifecycle = document.RootElement.GetProperty("lifecycle");
        Assert.Equal(document.RootElement.GetProperty("cameras").GetArrayLength(), lifecycle.GetProperty("totalDevices").GetInt32());
        Assert.True(lifecycle.GetProperty("eosCount").GetInt32() >= 1);
        Assert.Equal(
            lifecycle.GetProperty("currentProductCount").GetInt32()
            + lifecycle.GetProperty("eolCount").GetInt32()
            + lifecycle.GetProperty("eosCount").GetInt32()
            + lifecycle.GetProperty("naCount").GetInt32(),
            lifecycle.GetProperty("totalDevices").GetInt32());
        Assert.Equal(
            lifecycle.GetProperty("compliantSites").GetInt32() + lifecycle.GetProperty("nonCompliantSites").GetInt32(),
            lifecycle.GetProperty("totalSites").GetInt32());
        Assert.Equal(
            lifecycle.GetProperty("ndaaCompliantCount").GetInt32()
            + lifecycle.GetProperty("ndaaRestrictedCount").GetInt32()
            + lifecycle.GetProperty("ndaaUnknownCount").GetInt32(),
            lifecycle.GetProperty("totalDevices").GetInt32());
        Assert.True(lifecycle.GetProperty("ndaaCompliantCount").GetInt32() >= 1);
        var passwords = document.RootElement.GetProperty("passwordRotation");
        Assert.Equal(
            passwords.GetProperty("upToDateCount").GetInt32()
            + passwords.GetProperty("neverRotatedCount").GetInt32()
            + passwords.GetProperty("expiredCount").GetInt32()
            + passwords.GetProperty("soonCount").GetInt32()
            + passwords.GetProperty("naCount").GetInt32(),
            passwords.GetProperty("totalDevices").GetInt32());
        Assert.True(passwords.GetProperty("neverRotatedCount").GetInt32() >= 1);
        Assert.True(passwords.GetProperty("expiredCount").GetInt32() >= 1);
        var firmware = document.RootElement.GetProperty("firmware");
        Assert.Equal(
            firmware.GetProperty("compliantCount").GetInt32()
            + firmware.GetProperty("nonCompliantCount").GetInt32()
            + firmware.GetProperty("naCount").GetInt32(),
            firmware.GetProperty("totalDevices").GetInt32());
        Assert.True(firmware.GetProperty("nonCompliantCount").GetInt32() >= 1);
        Assert.True(firmware.GetProperty("details").GetArrayLength() >= 1);
        var securityServers = document.RootElement.GetProperty("securityServers");
        Assert.True(securityServers.GetProperty("totalServers").GetInt32() >= 2);
        Assert.Equal(
            securityServers.GetProperty("onlineCount").GetInt32()
            + securityServers.GetProperty("offlineCount").GetInt32(),
            securityServers.GetProperty("totalServers").GetInt32());
        Assert.True(securityServers.GetProperty("servers").GetArrayLength() >= 2);
        var firstServer = securityServers.GetProperty("servers").EnumerateArray().First();
        Assert.False(string.IsNullOrWhiteSpace(firstServer.GetProperty("status").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(firstServer.GetProperty("storageHealth").GetString()));
        Assert.False(firstServer.TryGetProperty("cpuPercent", out _));
        Assert.False(firstServer.TryGetProperty("cpuHealth", out _));
        Assert.Contains(
            securityServers.GetProperty("servers").EnumerateArray(),
            server => server.GetProperty("enabled").GetBoolean() == false);
    }

    [Fact]
    public async Task Encrypt_password_page_returns_enc_value_without_saving()
    {
        var response = await _client.PostAsJsonAsync("/api/settings/password", new
        {
            password = "FoxSecret!",
            save = false
        });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.StartsWith("ENC:", document.RootElement.GetProperty("encrypted").GetString());
        Assert.False(document.RootElement.GetProperty("saved").GetBoolean());
        Assert.DoesNotContain("FoxSecret!", json, StringComparison.Ordinal);

        var status = await _client.GetAsync("/api/settings/password");
        status.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Connection_settings_do_not_return_the_password()
    {
        var response = await _client.GetAsync("/api/settings/connection");
        response.EnsureSuccessStatusCode();
        Assert.Contains("json", response.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("useDemoData").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("password", out _));
        Assert.True(document.RootElement.TryGetProperty("passwordSet", out _));
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("gatewayBaseUrl").GetString()));
        Assert.DoesNotContain("ENC:", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_api_path_returns_json_not_html()
    {
        var response = await _client.GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("json", response.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Not found.", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Connection_test_explains_demo_mode_and_rejects_the_example_host()
    {
        var demo = await _client.PostAsJsonAsync("/api/settings/connection/test", new
        {
            useDemoData = true
        });
        demo.EnsureSuccessStatusCode();
        using var demoDocument = JsonDocument.Parse(await demo.Content.ReadAsStringAsync());
        Assert.True(demoDocument.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("Demo data", demoDocument.RootElement.GetProperty("message").GetString());

        var live = await _client.PostAsJsonAsync("/api/settings/connection/test", new
        {
            gatewayBaseUrl = "https://xprotect.example.com",
            username = "reader",
            password = "secret",
            useDemoData = false
        });
        live.EnsureSuccessStatusCode();
        var liveJson = await live.Content.ReadAsStringAsync();
        using var liveDocument = JsonDocument.Parse(liveJson);
        Assert.False(liveDocument.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("example host", liveDocument.RootElement.GetProperty("error").GetString());
        Assert.DoesNotContain("secret", liveJson, StringComparison.Ordinal);
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
        Assert.Contains("id=\"view-lifecycle\"", html);
        Assert.Contains("Device Lifecycle", html);
        Assert.Contains("lifecycle-highlights", html);
        Assert.Contains("ndaa-highlights", html);
        Assert.Contains("NDAA Highlights", html);
        Assert.Contains("id=\"view-passwords\"", html);
        Assert.Contains("Password Rotation", html);
        Assert.Contains("password-highlights", html);
        Assert.Contains("id=\"view-firmware\"", html);
        Assert.Contains("Outdated Firmware", html);
        Assert.Contains("firmware-highlights", html);
        Assert.Contains("id=\"view-security-servers\"", html);
        Assert.Contains("id=\"nav-security-servers\"", html);
        Assert.Contains("id=\"view-server-status\"", html);
        Assert.Contains("id=\"nav-server-status\"", html);
        Assert.Contains("Server Status", html);
        Assert.Contains("MasterMind", html);
        Assert.Contains("Perspective", html);
        Assert.Contains("Last checked", html);
        Assert.Contains("id=\"status-grid\"", html);
        Assert.Contains("MasterMind and Perspective", html);
        Assert.Contains("memory, OS, uptime, and Last checked", html);
        Assert.Contains("id=\"demo-banner\"", html);
        Assert.Contains("UseDemoData", html);
        Assert.Contains("id=\"view-encrypt\"", html);
        Assert.Contains("Encrypt password", html);
        Assert.Contains("encrypt-form", html);
        Assert.Contains("id=\"view-connect\"", html);
        Assert.Contains("Connect to XProtect", html);
        Assert.Contains("connect-form", html);
        Assert.Contains("Security Servers", html);
        Assert.Contains("servers-grid", html);
        Assert.Contains("id=\"storage-pies\"", html);
        Assert.Contains("Replace previous locations", html);
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
        Assert.Contains(".life-card", css);
        Assert.Contains("--pink", css);
        Assert.Contains(".highlights-grid", css);
        Assert.Contains(".ndaa-highlights-card", css);
        Assert.Contains(".pw-gauge", css);
        Assert.Contains(".risk-segments", css);
        Assert.Contains(".server-grid", css);
        Assert.Contains(".server-card", css);
        Assert.Contains(".status-deck", css);
        Assert.Contains(".status-grid", css);
        Assert.Contains(".status-server-card", css);
        Assert.Contains(".status-deck-list", css);
        Assert.Contains(".status-deck-group", css);
        Assert.Contains(".demo-banner", css);
        Assert.Contains(".connect-actions", css);
    }

    [Fact]
    public void Publish_does_not_replace_live_appsettings()
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var csproj = File.ReadAllText(Path.Combine(repo, "src", "Milestone.Dashboard", "Milestone.Dashboard.csproj"));
        var publish = File.ReadAllText(Path.Combine(repo, "scripts", "publish-iis.ps1"));
        Assert.Contains("CopyToPublishDirectory>Never", csproj);
        Assert.Contains("Keeping live settings", publish);
        Assert.Contains("Restore-LiveSettings", publish);
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

    [Fact]
    public async Task Location_csv_import_replaces_previous_pins_and_keeps_xprotect_camera_count()
    {
        using var first = new MultipartFormDataContent();
        first.Add(new StringContent("""
            cameraId,name,latitude,longitude,site
            c10,Server Room,34.05,-118.41,Old Site
            c20,Archive Vault,34.12,-118.43,Old Site
            """), "file", "old.csv");
        (await _client.PostAsync("/api/locations/import-csv?replace=true", first)).EnsureSuccessStatusCode();

        using var second = new MultipartFormDataContent();
        second.Add(new StringContent("""
            cameraId,name,latitude,longitude,site,address,Site_Name
            c10,Server Room,34.054244,-118.414072,FOXUSWDMSAP663,"10201 W Pico Blvd, Los Angeles, CA 90064, USA",Fox Studio Lot
            """), "file", "new.csv");
        var response = await _client.PostAsync("/api/locations/import-csv?replace=true", second);
        response.EnsureSuccessStatusCode();

        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, result.RootElement.GetProperty("saved").GetInt32());
        Assert.Equal(1, result.RootElement.GetProperty("removed").GetInt32());
        Assert.True(result.RootElement.GetProperty("cameraCount").GetInt32() >= 20);

        using var dashboard = JsonDocument.Parse(await _client.GetStringAsync("/api/dashboard"));
        Assert.True(dashboard.RootElement.GetProperty("cameras").GetArrayLength() >= 20);
        var vault = dashboard.RootElement.GetProperty("cameras").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "c20");
        Assert.False(vault.GetProperty("locationIsOverride").GetBoolean());
    }
}
