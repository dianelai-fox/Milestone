using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class StatusServerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string MasterMindTemplate = """"
        Server Name,IP Address,Server Description,Server Function,Domain,Environment,OS,SQL
        FOXUSWDMSDB303,10.180.80.154,MasterMind,App/DB,corp.fox,Prod,Windows Server 2022,Microsoft SQL Server 2022
        FOXUSWDMSDB304,10.180.80.155,MasterMind,App/DB,corp.fox,Prod,Windows Server 2022,Microsoft SQL Server 2022
        FOXUSWDMSDB305,10.180.80.156,MasterMind,App/DB,corp.fox,QA,Windows Server 2022,Microsoft SQL Server 2022
        AZTEC-FOX-1.corp.fox,10.180.118.10,MasterMind,Aztec Receivers,corp.fox,Prod,Linux,
        AZTEC-FOX-2.corp.fox,10.180.118.11,MasterMind,Aztec Receivers,corp.fox,Prod,Linux,
        AZTEC-FOX-3.corp.fox,10.180.118.12,MasterMind,Aztec Receivers,corp.fox,Prod,Linux,
        FOX2204376,10.138.201.11,MasterMind,MAS Signal Processor,corp.fox,Prod,Windows 10,
        FOX2205442,10.138.201.43,MasterMind,"MAS Signal Processor ""Backup""",corp.fox,Prod,Windows 11,
        """";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public StatusServerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
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
        Assert.Equal("App/DB", catalog.Single(server => server.Name == "FOXUSWDMSDB303").Role);
        Assert.Equal("Aztec Receivers", catalog.Single(server => server.Name == "AZTEC-FOX-1.corp.fox").Role);
        Assert.Equal("MAS Signal Processor", catalog.Single(server => server.Name == "FOX2204376").Role);
        Assert.Equal("MAS Signal Processor \"Backup\"", catalog.Single(server => server.Name == "FOX2205442").Role);
        Assert.Equal("Database", catalog.Single(server => server.Name == "FOXUSWDMSDB298").Role);
        Assert.Equal("Application", catalog.Single(server => server.Name == "FOXUSWDMSAP654").Role);
        Assert.Equal("Linux", catalog.Single(server => server.Name == "AZTEC-FOX-1.corp.fox").CatalogOs);
        Assert.Equal("QA", catalog.Single(server => server.Name == "FOXUSWDMSDB305").Environment);
    }

    [Fact]
    public void Parses_the_mastermind_server_template()
    {
        var rows = StatusServerCsvParser.Parse(MasterMindTemplate);
        Assert.Equal(8, rows.Count);
        Assert.All(rows, row => Assert.Equal("MasterMind", row.Deck));
        var backup = rows.Single(row => row.Name == "FOX2205442");
        Assert.Equal("10.138.201.43", backup.IpAddress);
        Assert.Equal("MAS Signal Processor \"Backup\"", backup.Role);
        Assert.Equal("Windows 11", backup.CatalogOs);
        Assert.Null(backup.Sql);
        var db = rows.Single(row => row.Name == "FOXUSWDMSDB303");
        Assert.Equal("App/DB", db.Role);
        Assert.Equal("Microsoft SQL Server 2022", db.Sql);
        Assert.Equal("corp.fox", db.Domain);
        var linux = rows.Single(row => row.Name == "AZTEC-FOX-1.corp.fox");
        Assert.Equal("Aztec Receivers", linux.Role);
        Assert.Equal("Linux", linux.CatalogOs);
        Assert.Equal("MasterMind", db.Description);
    }

    [Fact]
    public void Default_services_follow_server_function()
    {
        var db = StatusServerServices.DefaultFor(new StatusServerCatalog.Spec
        {
            Name = "FOXUSWDMSDB303",
            Role = "App/DB",
            Sql = "Microsoft SQL Server 2022",
            CatalogOs = "Windows Server 2022"
        });
        Assert.Contains("MSSQLSERVER", db);
        Assert.Contains("SQLSERVERAGENT", db);
        Assert.Contains("W3SVC", db);

        var app = StatusServerServices.DefaultFor(new StatusServerCatalog.Spec
        {
            Name = "FOXUSWDMSAP654",
            Role = "Application",
            CatalogOs = "Windows Server 2022"
        });
        Assert.Contains("W3SVC", app);
        Assert.DoesNotContain("MSSQLSERVER", app);

        var linux = StatusServerServices.DefaultFor(new StatusServerCatalog.Spec
        {
            Name = "AZTEC-FOX-1.corp.fox",
            Role = "Aztec Receivers",
            CatalogOs = "Linux"
        });
        Assert.Empty(linux);
    }

    [Fact]
    public void Parses_services_from_csv_and_json()
    {
        var csv = """
            Server Name,IP Address,Server Description,Server Function,Domain,Environment,OS,SQL,Services
            FOXUSWDMSIA304,10.180.80.200,Lenel,Application,corp.fox,Prod,Windows Server 2022,,"MSSQLSERVER; W3SVC"
            """;
        var row = Assert.Single(StatusServerCsvParser.Parse(csv));
        Assert.Equal(["MSSQLSERVER", "W3SVC"], row.Services);

        var services = StatusServerMonitor.ParseServicesJson(
            ["MSSQLSERVER", "W3SVC"],
            """
            [
              { "Name": "MSSQLSERVER", "State": "Running", "DisplayName": "SQL Server (MSSQLSERVER)" },
              { "Name": "W3SVC", "State": "Stopped", "DisplayName": "World Wide Web Publishing Service" }
            ]
            """);
        Assert.NotNull(services);
        Assert.True(services.Single(item => item.Name == "MSSQLSERVER").Running);
        Assert.True(services.Single(item => item.Name == "W3SVC").NeedsAttention);
        var named = StatusServerMonitor.ParseServicesJson(
            ["MSSQLSERVER"],
            """
            [ { "Name": "MSSQL$MASTERMIND", "State": "Running", "DisplayName": "SQL Server (MASTERMIND)" } ]
            """);
        Assert.Contains(named!, item => item.Name == "MSSQL$MASTERMIND" && item.Running);
        Assert.DoesNotContain(named!, item => item.Name == "MSSQLSERVER" && item.Status == "Not found");
        var onlineHost = new Milestone.Dashboard.Models.StatusServerInfo
        {
            Id = "status:lab",
            Name = "lab",
            IpAddress = "192.0.2.10",
            Online = true,
            Services = services
        };
        Assert.True(onlineHost.NeedsAttention);
        var offline = StatusServerServices.Unreachable(["MSSQLSERVER"], "Host offline", "Host is offline.");
        Assert.Equal("Host offline", Assert.Single(offline).Status);
        Assert.False(Assert.Single(offline).NeedsAttention);
    }

    [Fact]
    public void Server_description_becomes_its_own_application_group()
    {
        var csv = MasterMindTemplate.Replace(
            "FOXUSWDMSDB303,10.180.80.154,MasterMind,App/DB",
            "FOXUSWDMSDB303,10.180.80.154,Lenel,App/DB");
        csv = csv.Replace(
            "FOX2205442,10.138.201.43,MasterMind,",
            "FOX2205442,10.138.201.43,Aztec,");
        var rows = StatusServerCsvParser.Parse(csv);
        var db = rows.Single(row => row.Name == "FOXUSWDMSDB303");
        Assert.Equal("Lenel", db.Deck);
        Assert.Equal("Lenel", db.Description);
        var backup = rows.Single(row => row.Name == "FOX2205442");
        Assert.Equal("Aztec", backup.Deck);
        Assert.Equal("Aztec", backup.Description);
        Assert.Equal(6, rows.Count(row => row.Deck == "MasterMind"));
        Assert.Contains(rows, row => row.Deck == "Lenel");
        Assert.Contains(rows, row => row.Deck == "Aztec");
    }

    [Fact]
    public void Inventory_json_keeps_updated_server_description()
    {
        var original = new StatusServerCatalog.Spec
        {
            Name = "FOXUSWDMSDB303",
            IpAddress = "10.180.80.154",
            Role = "App/DB",
            Deck = "MasterMind",
            Description = "MasterMind"
        };
        var updated = StatusServerCatalog.Merge(original, new StatusServerCatalog.Spec
        {
            Name = "FOXUSWDMSDB303",
            IpAddress = "10.180.80.154",
            Role = "App/DB",
            Deck = "MasterMind",
            Description = "Lenel"
        });
        Assert.Equal("Lenel", updated.Deck);
        Assert.Equal("Lenel", updated.Description);

        var json = JsonSerializer.Serialize(new[] { updated }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var roundTrip = JsonSerializer.Deserialize<List<StatusServerCatalog.Spec>>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });
        var saved = Assert.Single(roundTrip!);
        Assert.Equal("Lenel", saved.Description);
        Assert.Equal("Lenel", saved.Deck);
    }

    [Fact]
    public void Replacing_mastermind_from_csv_keeps_perspective()
    {
        var imported = StatusServerCsvParser.Parse(MasterMindTemplate);
        var combined = new StatusServerCatalog().Combine(imported);
        Assert.Equal(12, combined.Count);
        Assert.Equal(8, combined.Count(server => server.Deck == "MasterMind"));
        Assert.Equal(4, combined.Count(server => server.Deck == "Perspective"));
        Assert.Equal("App/DB", combined.Single(server => server.Name == "FOXUSWDMSDB303").Role);
        Assert.Contains(combined, server => server.Name == "FOXUSWDMSAP655" && server.Deck == "Perspective");
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
            new StatusServerCatalog.Spec { Name = "online-lab", IpAddress = "127.0.0.1", Role = "Lab" },
            new StatusServerCatalog.Spec { Name = "bad-ip", IpAddress = "not-an-ip", Role = "Lab" }
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
    public async Task Saved_server_description_is_shown_as_its_own_application()
    {
        var monitor = new StatusServerMonitor();
        var overview = await monitor.ProbeAsync(
        [
            new StatusServerCatalog.Spec
            {
                Name = "FOXUSWDMSDB303",
                IpAddress = "192.0.2.10",
                Role = "App/DB",
                Deck = "MasterMind",
                Description = "Lenel"
            },
            new StatusServerCatalog.Spec
            {
                Name = "FOXUSWDMSAP654",
                IpAddress = "192.0.2.11",
                Role = "Application",
                Deck = "Perspective",
                Description = "Perspective"
            }
        ], CancellationToken.None);

        Assert.Equal(2, overview.Decks.Count);
        Assert.Contains(overview.Decks, deck => deck.Name == "Lenel" && deck.TotalServers == 1);
        Assert.Contains(overview.Decks, deck => deck.Name == "Perspective" && deck.TotalServers == 1);
        Assert.Equal("Lenel", overview.Servers.Single(server => server.Name == "FOXUSWDMSDB303").Deck);
    }

    [Fact]
    public async Task Documentation_ip_is_offline()
    {
        var monitor = new StatusServerMonitor();
        var overview = await monitor.ProbeAsync(
        [
            new StatusServerCatalog.Spec { Name = "doc-net", IpAddress = "192.0.2.1", Role = "Lab" }
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
            Role = "App/DB",
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
            8,
            master.GetProperty("onlineCount").GetInt32() + master.GetProperty("offlineCount").GetInt32());
        Assert.Equal(master.GetProperty("offlineCount").GetInt32(), master.GetProperty("attentionCount").GetInt32());
        Assert.Equal(
            4,
            perspective.GetProperty("onlineCount").GetInt32() + perspective.GetProperty("offlineCount").GetInt32());
        Assert.Equal(perspective.GetProperty("offlineCount").GetInt32(), perspective.GetProperty("attentionCount").GetInt32());
        Assert.Equal(12, document.RootElement.GetProperty("servers").GetArrayLength());
        Assert.Contains(
            document.RootElement.GetProperty("servers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "FOXUSWDMSDB303"
                      && server.GetProperty("ipAddress").GetString() == "10.180.80.154"
                      && server.GetProperty("role").GetString() == "App/DB"
                      && server.GetProperty("sql").GetString() == "Microsoft SQL Server 2022"
                      && server.GetProperty("detail").GetString()?.Length > 0
                      && server.GetProperty("services").EnumerateArray()
                          .Any(item => item.GetProperty("name").GetString() == "MSSQLSERVER"));
        Assert.Contains(
            document.RootElement.GetProperty("servers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "FOXUSWDMSAP655"
                      && server.GetProperty("ipAddress").GetString() == "10.180.96.24"
                      && server.GetProperty("deck").GetString() == "Perspective");
        Assert.Contains(
            document.RootElement.GetProperty("servers").EnumerateArray(),
            server => server.GetProperty("name").GetString() == "AZTEC-FOX-1.corp.fox"
                      && server.GetProperty("operatingSystem").GetString() == "Linux"
                      && server.GetProperty("environment").GetString() == "Prod");
    }

    [Fact]
    public async Task Server_status_template_matches_the_import_headers()
    {
        var response = await _client.GetAsync("/api/server-status/template");
        response.EnsureSuccessStatusCode();
        var csv = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("Server Name,IP Address,Server Description,Server Function,Domain,Environment,OS,SQL,Services", csv);
        Assert.Contains("FOXUSWDMSDB303", csv);
        Assert.Contains("FOXUSWDMSAP655", csv);
        Assert.Contains("MAS Signal Processor", csv);
        Assert.Contains("MSSQLSERVER", csv);
        Assert.Contains("W3SVC", csv);
    }

    [Fact]
    public async Task Server_status_csv_import_updates_mastermind_and_keeps_perspective()
    {
        var store = _factory.Services.GetRequiredService<StatusServerInventoryStore>();
        try
        {
            using var content = new MultipartFormDataContent();
            var file = new StringContent(MasterMindTemplate, Encoding.UTF8, "text/csv");
            file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(file, "file", "servers.csv");
            var response = await _client.PostAsync("/api/server-status/import-csv?replace=true", content);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(8, document.RootElement.GetProperty("imported").GetInt32());
            var overview = document.RootElement.GetProperty("overview");
            Assert.Equal(12, overview.GetProperty("servers").GetArrayLength());
            Assert.Contains(
                overview.GetProperty("servers").EnumerateArray(),
                server => server.GetProperty("name").GetString() == "FOX2205442"
                          && server.GetProperty("role").GetString() == "MAS Signal Processor \"Backup\""
                          && server.GetProperty("operatingSystem").GetString() == "Windows 11");
            Assert.Contains(
                overview.GetProperty("servers").EnumerateArray(),
                server => server.GetProperty("name").GetString() == "FOXUSWDMSAP654"
                          && server.GetProperty("deck").GetString() == "Perspective");
        }
        finally
        {
            await store.ClearAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Server_status_csv_import_updates_server_description_on_existing_hosts()
    {
        var store = _factory.Services.GetRequiredService<StatusServerInventoryStore>();
        try
        {
            var csv = MasterMindTemplate
                .Replace(
                    "FOXUSWDMSDB303,10.180.80.154,MasterMind,App/DB",
                    "FOXUSWDMSDB303,10.180.80.154,Primary MasterMind database,App/DB")
                .Replace(
                    "FOX2205442,10.138.201.43,MasterMind,",
                    "FOX2205442,10.138.201.43,MAS backup processor,");
            using var content = new MultipartFormDataContent();
            var file = new StringContent(csv, Encoding.UTF8, "text/csv");
            file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(file, "file", "servers.csv");
            var import = await _client.PostAsync("/api/server-status/import-csv?replace=true", content);
            import.EnsureSuccessStatusCode();
            using var imported = JsonDocument.Parse(await import.Content.ReadAsStringAsync());
            Assert.Contains(
                imported.RootElement.GetProperty("overview").GetProperty("servers").EnumerateArray(),
                server => server.GetProperty("name").GetString() == "FOXUSWDMSDB303"
                          && server.GetProperty("description").GetString() == "Primary MasterMind database"
                          && server.GetProperty("deck").GetString() == "Primary MasterMind database");

            using var document = JsonDocument.Parse(await _client.GetStringAsync("/api/server-status"));
            var servers = document.RootElement.GetProperty("servers").EnumerateArray().ToList();
            var decks = document.RootElement.GetProperty("decks").EnumerateArray()
                .Select(deck => deck.GetProperty("name").GetString())
                .ToList();
            Assert.Equal(12, servers.Count);
            Assert.Contains("Primary MasterMind database", decks);
            Assert.Contains("MAS backup processor", decks);
            Assert.Contains("MasterMind", decks);
            Assert.Contains("Perspective", decks);
            Assert.Contains(
                servers,
                server => server.GetProperty("name").GetString() == "FOXUSWDMSDB303"
                          && server.GetProperty("description").GetString() == "Primary MasterMind database"
                          && server.GetProperty("deck").GetString() == "Primary MasterMind database");
            Assert.Contains(
                servers,
                server => server.GetProperty("name").GetString() == "FOX2205442"
                          && server.GetProperty("description").GetString() == "MAS backup processor"
                          && server.GetProperty("deck").GetString() == "MAS backup processor");
            Assert.Equal(6, servers.Count(server => server.GetProperty("deck").GetString() == "MasterMind"));
            Assert.Equal(4, servers.Count(server => server.GetProperty("deck").GetString() == "Perspective"));
        }
        finally
        {
            await store.ClearAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void Duplicate_server_names_in_csv_do_not_throw()
    {
        const string csv = """
            Server Name,IP Address,Server Description,Server Function,Domain,Environment,OS,SQL
            FOXUSWDMSIA304,10.180.80.200,Lenel,Application,corp.fox,Prod,Windows Server 2022,
            FOXUSWDMSIA304,10.180.80.200,Lenel,Application,corp.fox,Prod,Windows Server 2022,
            FOXUSWDMSIA304,10.180.80.201,Aztec,Application,corp.fox,Prod,Windows Server 2022,
            FOXUSWDMSDB303,10.180.80.154,MasterMind,App/DB,corp.fox,Prod,Windows Server 2022,Microsoft SQL Server 2022
            """;
        var imported = StatusServerCsvParser.Parse(csv);
        Assert.Equal(4, imported.Count);
        var combined = new StatusServerCatalog().Combine(imported);
        Assert.Contains(combined, server => server.Name == "FOXUSWDMSIA304" && server.Deck == "Lenel");
        Assert.Contains(combined, server => server.Name == "FOXUSWDMSIA304" && server.Deck == "Aztec");
        Assert.Equal(1, combined.Count(server => server.Name == "FOXUSWDMSIA304" && server.Deck == "Lenel"));
        Assert.Equal(2, combined.Count(server => server.Name == "FOXUSWDMSIA304"));
    }

    [Fact]
    public async Task Server_status_csv_import_accepts_duplicate_names_and_new_applications()
    {
        var store = _factory.Services.GetRequiredService<StatusServerInventoryStore>();
        try
        {
            const string first = """
                Server Name,IP Address,Server Description,Server Function,Domain,Environment,OS,SQL
                FOXUSWDMSIA304,10.180.80.200,Lenel,Application,corp.fox,Prod,Windows Server 2022,
                FOXUSWDMSIA304,10.180.80.200,Lenel,Application,corp.fox,QA,Windows Server 2022,
                """;
            using (var seed = new MultipartFormDataContent())
            {
                var file = new StringContent(first, Encoding.UTF8, "text/csv");
                file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
                seed.Add(file, "file", "servers.csv");
                (await _client.PostAsync("/api/server-status/import-csv?replace=true", seed)).EnsureSuccessStatusCode();
            }

            const string csv = """
                Server Name,IP Address,Server Description,Server Function,Domain,Environment,OS,SQL
                FOXUSWDMSIA304,10.180.80.200,Lenel,Application,corp.fox,Prod,Windows Server 2022,
                FOXUSWDMSIA304,10.180.80.201,Aztec,Receiver,corp.fox,Prod,Windows Server 2022,
                FOXUSWDMSDB303,10.180.80.154,MasterMind,App/DB,corp.fox,Prod,Windows Server 2022,Microsoft SQL Server 2022
                NEWAPP-01,10.180.90.10,C-Cure,Application,corp.fox,Prod,Windows Server 2022,
                """;
            using var content = new MultipartFormDataContent();
            var next = new StringContent(csv, Encoding.UTF8, "text/csv");
            next.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(next, "file", "servers.csv");
            var response = await _client.PostAsync("/api/server-status/import-csv?replace=true", content);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(4, document.RootElement.GetProperty("imported").GetInt32());
            Assert.Contains(
                document.RootElement.GetProperty("duplicateNames").EnumerateArray().Select(item => item.GetString()),
                name => name == "FOXUSWDMSIA304");
            var servers = document.RootElement.GetProperty("overview").GetProperty("servers").EnumerateArray().ToList();
            Assert.Contains(servers, server => server.GetProperty("name").GetString() == "FOXUSWDMSIA304"
                                              && server.GetProperty("deck").GetString() == "Lenel"
                                              && server.GetProperty("ipAddress").GetString() == "10.180.80.200");
            Assert.Contains(servers, server => server.GetProperty("name").GetString() == "FOXUSWDMSIA304"
                                              && server.GetProperty("deck").GetString() == "Aztec"
                                              && server.GetProperty("ipAddress").GetString() == "10.180.80.201");
            Assert.Contains(servers, server => server.GetProperty("name").GetString() == "NEWAPP-01"
                                              && server.GetProperty("deck").GetString() == "C-Cure");
            Assert.Contains(servers, server => server.GetProperty("name").GetString() == "FOXUSWDMSAP655"
                                              && server.GetProperty("deck").GetString() == "Perspective");
        }
        finally
        {
            await store.ClearAsync(CancellationToken.None);
        }
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
