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
    public void Catalog_has_the_eight_mastermind_servers()
    {
        var catalog = new StatusServerCatalog().List();
        Assert.Equal(8, catalog.Count);
        Assert.All(catalog, server => Assert.Equal("MasterMind", server.Deck));
        Assert.Contains(catalog, server => server.Name == "FOXUSWDMSDB303" && server.IpAddress == "10.180.80.154");
        Assert.Contains(catalog, server => server.Name == "FOXUSWDMSDB304" && server.IpAddress == "10.180.80.155");
        Assert.Contains(catalog, server => server.Name == "FOXUSWDMSDB305" && server.IpAddress == "10.180.80.156");
        Assert.Contains(catalog, server => server.Name == "AZTEC-FOX-1.corp.fox" && server.IpAddress == "10.180.118.10");
        Assert.Contains(catalog, server => server.Name == "AZTEC-FOX-2.corp.fox" && server.IpAddress == "10.180.118.11");
        Assert.Contains(catalog, server => server.Name == "AZTEC-FOX-3.corp.fox" && server.IpAddress == "10.180.118.12");
        Assert.Contains(catalog, server => server.Name == "FOX2204376" && server.IpAddress == "10.138.201.11");
        Assert.Contains(catalog, server => server.Name == "FOX2205442" && server.IpAddress == "10.138.201.43");
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
            new StatusServerCatalog.Spec("online-lab", "127.0.0.1"),
            new StatusServerCatalog.Spec("bad-ip", "not-an-ip")
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
            new StatusServerCatalog.Spec("doc-net", "192.0.2.1")
        ], CancellationToken.None);

        var server = Assert.Single(overview.Servers);
        Assert.False(server.Online);
        Assert.Equal("Offline", server.Status);
        Assert.Equal(1, overview.Decks[0].AttentionCount);
    }

    [Fact]
    public async Task Server_status_api_returns_the_mastermind_deck()
    {
        var response = await _client.GetAsync("/api/server-status");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var deck = document.RootElement.GetProperty("decks").EnumerateArray().Single();
        Assert.Equal("MasterMind", deck.GetProperty("name").GetString());
        Assert.Equal(8, deck.GetProperty("totalServers").GetInt32());
        Assert.Equal(
            8,
            deck.GetProperty("onlineCount").GetInt32() + deck.GetProperty("offlineCount").GetInt32());
        Assert.Equal(deck.GetProperty("offlineCount").GetInt32(), deck.GetProperty("attentionCount").GetInt32());
        Assert.Equal(8, document.RootElement.GetProperty("servers").GetArrayLength());
        Assert.Contains(
            document.RootElement.GetProperty("servers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "FOXUSWDMSDB303"
                      && server.GetProperty("ipAddress").GetString() == "10.180.80.154");
    }

    [Fact]
    public async Task Dashboard_security_servers_do_not_include_mastermind_hosts()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/api/dashboard"));
        Assert.DoesNotContain(
            document.RootElement.GetProperty("securityServers").GetProperty("servers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "FOXUSWDMSDB303");
        Assert.DoesNotContain(
            document.RootElement.GetProperty("recordingServers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "AZTEC-FOX-1.corp.fox");
    }
}
