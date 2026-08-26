using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class StatusServerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public StatusServerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public void Catalog_has_mastermind_and_perspective_servers()
    {
        var catalog = new StatusServerCatalog().List();
        Assert.Equal(12, catalog.Count);
        Assert.Equal(8, catalog.Count(server => server.Deck == "MasterMind"));
        Assert.Equal(4, catalog.Count(server => server.Deck == "Perspective"));
        Assert.Contains(catalog, server => server.Name == "FOXUSWDMSDB303" && server.IpAddress == "10.180.80.154");
        Assert.Contains(catalog, server => server.Name == "FOXUSWDMSDB304" && server.IpAddress == "10.180.80.155");
        Assert.Contains(catalog, server => server.Name == "FOXUSWDMSDB305" && server.IpAddress == "10.180.80.156");
        Assert.Contains(catalog, server => server.Name == "AZTEC-FOX-1.corp.fox" && server.IpAddress == "10.180.118.10");
        Assert.Contains(catalog, server => server.Name == "AZTEC-FOX-2.corp.fox" && server.IpAddress == "10.180.118.11");
        Assert.Contains(catalog, server => server.Name == "AZTEC-FOX-3.corp.fox" && server.IpAddress == "10.180.118.12");
        Assert.Contains(catalog, server => server.Name == "FOX2204376" && server.IpAddress == "10.138.201.11");
        Assert.Contains(catalog, server => server.Name == "FOX2205442" && server.IpAddress == "10.138.201.43");
        Assert.Contains(catalog, server => server.Name == "FOXUSWDMSDB298" && server.IpAddress == "10.180.80.36" && server.Deck == "Perspective");
        Assert.Contains(catalog, server => server.Name == "FOXUSWDMSAP654" && server.IpAddress == "10.180.80.37" && server.Deck == "Perspective");
        Assert.Contains(catalog, server => server.Name == "FOXUSWDMSDB299" && server.IpAddress == "10.180.96.23" && server.Deck == "Perspective");
        Assert.Contains(catalog, server => server.Name == "FOXUSWDMSAP655" && server.IpAddress == "10.180.96.24" && server.Deck == "Perspective");
        Assert.Equal("Database", catalog.Single(server => server.Name == "FOXUSWDMSDB303").Role);
        Assert.Equal("Application", catalog.Single(server => server.Name == "AZTEC-FOX-1.corp.fox").Role);
        Assert.Equal("Endpoint", catalog.Single(server => server.Name == "FOX2204376").Role);
        Assert.Equal("Database", catalog.Single(server => server.Name == "FOXUSWDMSDB298").Role);
        Assert.Equal("Application", catalog.Single(server => server.Name == "FOXUSWDMSAP654").Role);
    }

    [Theory]
    [InlineData("10.180.80.154", true)]
    [InlineData("10.138.201.43", true)]
    [InlineData("not-an-ip", false)]
    [InlineData("10.180.80", false)]
    public void Accepts_only_ipv4_addresses(string ip, bool expected)
    {
        Assert.Equal(expected, StatusServerMonitor.IsValidIPv4(ip));
    }

    [Fact]
    public async Task Overview_counts_offline_servers_as_need_attention()
    {
        var monitor = new StatusServerMonitor();
        var overview = await monitor.ProbeAsync(
        [
            new StatusServerCatalog.Spec("online-lab", "127.0.0.1", "Lab"),
            new StatusServerCatalog.Spec("bad-ip", "not-an-ip", "Lab")
        ], CancellationToken.None);

        var deck = Assert.Single(overview.Decks);
        Assert.Equal("MasterMind", deck.Name);
        Assert.Equal(2, deck.TotalServers);
        Assert.Equal(deck.OfflineCount, deck.AttentionCount);
        Assert.Equal(deck.OnlineCount + deck.OfflineCount, deck.TotalServers);
        var invalid = overview.Servers.Single(server => server.Name == "bad-ip");
        Assert.False(invalid.Online);
        Assert.True(invalid.NeedsAttention);
        Assert.Equal("Offline", invalid.Status);
    }

    [Fact]
    public async Task Documentation_ip_is_offline()
    {
        var monitor = new StatusServerMonitor();
        var overview = await monitor.ProbeAsync(
        [
            new StatusServerCatalog.Spec("doc-net", "192.0.2.1", "Lab")
        ], CancellationToken.None);

        var server = Assert.Single(overview.Servers);
        Assert.False(server.Online);
        Assert.Equal("Offline", server.Status);
        Assert.Equal("Lab", server.Role);
        Assert.Contains("No response", server.Detail);
        Assert.False(string.IsNullOrWhiteSpace(server.CheckedAt.ToString()));
        Assert.Equal(1, overview.Decks[0].AttentionCount);
    }

    [Fact]
    public void Parses_windows_inventory_json()
    {
        const string json = """
            {
              "os": {
                "Caption": "Microsoft Windows Server 2022 Standard",
                "LastBootUpTime": "2026-08-20T08:00:00Z",
                "TotalVisibleMemorySize": 33554432,
                "FreePhysicalMemory": 16777216
              },
              "disks": [
                { "DeviceID": "C:", "Size": 536870912000, "FreeSpace": 107374182400 }
              ]
            }
            """;

        var reading = StatusServerMonitor.ParseInventoryJson(json);
        Assert.NotNull(reading);
        Assert.Equal("Microsoft Windows Server 2022 Standard", reading.OperatingSystem);
        Assert.Equal(50, reading.MemoryUsedPercent);
        Assert.True(reading.StorageUsedPercent is >= 79 and <= 81);
        Assert.False(string.IsNullOrWhiteSpace(reading.Uptime));
    }

    [Fact]
    public void High_storage_on_an_online_server_needs_attention()
    {
        var server = new Milestone.Dashboard.Models.StatusServerInfo
        {
            Id = "status:db",
            Name = "FOXUSWDMSDB303",
            IpAddress = "10.180.80.154",
            Role = "Database",
            Online = true,
            StorageReported = true,
            StorageUsedPercent = 92
        };

        Assert.Equal("Critical", server.StorageHealth);
        Assert.True(server.NeedsAttention);
    }

    [Fact]
    public async Task Server_status_api_returns_mastermind_and_perspective_decks()
    {
        var response = await _client.GetAsync("/api/server-status");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var decks = document.RootElement.GetProperty("decks").EnumerateArray().ToList();
        Assert.Equal(2, decks.Count);
        Assert.Equal("MasterMind", decks[0].GetProperty("name").GetString());
        Assert.Equal("Perspective", decks[1].GetProperty("name").GetString());
        var master = decks[0];
        var perspective = decks[1];
        Assert.Equal(8, master.GetProperty("totalServers").GetInt32());
        Assert.Equal(4, perspective.GetProperty("totalServers").GetInt32());
        Assert.Equal(
            master.GetProperty("onlineCount").GetInt32() + master.GetProperty("offlineCount").GetInt32(),
            8);
        Assert.Equal(master.GetProperty("offlineCount").GetInt32(), master.GetProperty("attentionCount").GetInt32());
        Assert.Equal(
            perspective.GetProperty("onlineCount").GetInt32() + perspective.GetProperty("offlineCount").GetInt32(),
            4);
        Assert.Equal(perspective.GetProperty("offlineCount").GetInt32(), perspective.GetProperty("attentionCount").GetInt32());
        Assert.Equal(12, document.RootElement.GetProperty("servers").GetArrayLength());
        Assert.Contains(
            document.RootElement.GetProperty("servers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "FOXUSWDMSDB303"
                      && server.GetProperty("ipAddress").GetString() == "10.180.80.154"
                      && server.GetProperty("role").GetString() == "Database"
                      && server.GetProperty("detail").GetString()?.Length > 0);
        Assert.Contains(
            document.RootElement.GetProperty("servers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "FOXUSWDMSAP655"
                      && server.GetProperty("ipAddress").GetString() == "10.180.96.24"
                      && server.GetProperty("deck").GetString() == "Perspective");
    }

    [Fact]
    public async Task Dashboard_security_servers_do_not_include_mastermind_hosts()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/api/dashboard"));
        Assert.DoesNotContain(
            document.RootElement.GetProperty("securityServers").GetProperty("servers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "FOXUSWDMSDB303");
        Assert.DoesNotContain(
            document.RootElement.GetProperty("securityServers").GetProperty("servers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "FOXUSWDMSDB298");
        Assert.DoesNotContain(
            document.RootElement.GetProperty("recordingServers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "AZTEC-FOX-1.corp.fox");
        Assert.DoesNotContain(
            document.RootElement.GetProperty("recordingServers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "FOXUSWDMSAP655");
    }
}
