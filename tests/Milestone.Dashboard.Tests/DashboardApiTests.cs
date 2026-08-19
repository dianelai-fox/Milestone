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
        Assert.True(document.RootElement.GetProperty("storages").GetArrayLength() > 0);
        Assert.True(document.RootElement.GetProperty("summary").GetProperty("cameraCount").GetInt32() > 0);
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
}
