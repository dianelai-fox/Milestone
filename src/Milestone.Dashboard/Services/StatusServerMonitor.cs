using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public sealed class StatusServerMonitor
{
    private static readonly (int Port, string Method)[] ProbePorts =
    [
        (445, "SMB"),
        (3389, "RDP")
    ];

    private readonly StatusServerCatalog _catalog;
    private readonly StatusServerInventoryStore? _store;

    public StatusServerMonitor()
        : this(new StatusServerCatalog())
    {
    }

    public StatusServerMonitor(StatusServerCatalog catalog, StatusServerInventoryStore? store = null)
    {
        _catalog = catalog;
        _store = store;
    }

    public async Task<ServerStatusOverview> ProbeAsync(CancellationToken cancellationToken)
    {
        var specs = _store is null
            ? _catalog.List()
            : await _store.ResolveAsync(_catalog, cancellationToken);
        return await ProbeAsync(specs, cancellationToken);
    }

    public async Task<ServerStatusOverview> ProbeAsync(
        IEnumerable<StatusServerCatalog.Spec> specs,
        CancellationToken cancellationToken)
    {
        var servers = await Task.WhenAll(specs.Select(spec => ProbeOneAsync(spec, cancellationToken)));
        var decks = servers
            .GroupBy(server => server.Deck, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var list = group
                    .OrderBy(server => server.Online ? 1 : 0)
                    .ThenBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return new StatusServerDeck
                {
                    Name = group.Key,
                    TotalServers = list.Count,
                    OnlineCount = list.Count(server => server.Online),
                    OfflineCount = list.Count(server => !server.Online),
                    AttentionCount = list.Count(server => server.NeedsAttention),
                    Servers = list
                };
            })
            .OrderBy(deck => deck.Name switch
            {
                "MasterMind" => 0,
                "Perspective" => 1,
                _ => 2
            })
            .ThenBy(deck => deck.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ServerStatusOverview
        {
            Decks = decks,
            Servers = decks.SelectMany(deck => deck.Servers).ToList()
        };
    }

    internal static bool IsValidIPv4(string? ip) =>
        !string.IsNullOrWhiteSpace(ip)
        && ip.Count(character => character == '.') == 3
        && IPAddress.TryParse(ip, out var address)
        && address.AddressFamily == AddressFamily.InterNetwork;

    private async Task<StatusServerInfo> ProbeOneAsync(
        StatusServerCatalog.Spec spec,
        CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var reach = await ProbeReachabilityAsync(spec.IpAddress, cancellationToken);
        InventoryReading? inventory = null;
        if (reach.Online && OperatingSystem.IsWindows())
        {
            inventory = await TryReadInventoryAsync(spec.IpAddress, cancellationToken);
        }

        var application = StatusServerCsvParser.ResolveApplication(spec.Description, spec.Deck);
        var watched = StatusServerServices.Watched(spec);
        IReadOnlyList<StatusServiceInfo> services = [];
        if (watched.Count > 0)
        {
            if (!OperatingSystem.IsWindows())
            {
                services = StatusServerServices.Unreachable(
                    watched,
                    "IIS only",
                    "Windows service checks run from the IIS host, not from this machine.");
            }
            else
            {
                var probe = await TryReadServicesAsync(spec.IpAddress, watched, cancellationToken);
                services = probe.Services ?? StatusServerServices.Unreachable(
                    watched,
                    reach.Online ? "No access" : "Host offline",
                    reach.Online
                        ? ShortError(
                            probe.Error,
                            "The host answered, but IIS could not read Win32_Service. Run scripts/test-remote-service-access.ps1 on the IIS server. Grant the account from -ShowIisIdentity on that IIS server, not your PC.")
                        : "The host did not answer SMB (445), RDP (3389), or a Windows service query.");
            }
        }

        return new StatusServerInfo
        {
            Id = $"status:{spec.Name}:{application}",
            Name = spec.Name,
            IpAddress = spec.IpAddress,
            Role = spec.Role,
            Deck = application,
            Description = spec.Description ?? application,
            Domain = spec.Domain,
            Environment = spec.Environment,
            Sql = spec.Sql,
            Online = reach.Online,
            CheckedAt = checkedAt,
            LatencyMs = reach.LatencyMs,
            ProbePort = reach.Port,
            ProbeMethod = reach.Method,
            Detail = reach.Detail,
            OperatingSystem = inventory?.OperatingSystem ?? spec.CatalogOs,
            LastBoot = inventory?.LastBoot,
            Uptime = inventory?.Uptime,
            MemoryUsedPercent = inventory?.MemoryUsedPercent,
            StorageUsedPercent = inventory?.StorageUsedPercent,
            StorageReported = inventory?.StorageUsedPercent is not null,
            Services = services
        };
    }

    internal static async Task<Reachability> ProbeReachabilityAsync(string ipAddress, CancellationToken cancellationToken)
    {
        if (!IsValidIPv4(ipAddress))
        {
            return new Reachability(false, null, null, null, "IP address is not valid.");
        }

        foreach (var (port, method) in ProbePorts)
        {
            var (connected, latencyMs) = await TryConnectAsync(ipAddress, port, cancellationToken);
            if (connected)
            {
                return new Reachability(true, latencyMs, port, method, $"Responded on {method} ({port}).");
            }
        }

        if (OperatingSystem.IsWindows() && await TryPingAsync(ipAddress))
        {
            return new Reachability(true, null, null, "Ping", "Reached by ICMP ping. SMB and RDP did not answer.");
        }

        return new Reachability(false, null, null, null, "No response on SMB (445) or RDP (3389).");
    }

    private static async Task<(bool Connected, int? LatencyMs)> TryConnectAsync(
        string ipAddress,
        int port,
        CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            var address = IPAddress.Parse(ipAddress);
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(1000));
            await socket.ConnectAsync(new IPEndPoint(address, port), timeout.Token);
            if (socket.Connected
                && socket.RemoteEndPoint is IPEndPoint remote
                && remote.Address.Equals(address))
            {
                return (true, (int)clock.ElapsedMilliseconds);
            }
        }
        catch
        {
            // Offline or filtered.
        }

        return (false, null);
    }

    private static async Task<bool> TryPingAsync(string ipAddress)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, 1000);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<InventoryReading?> TryReadInventoryAsync(string ipAddress, CancellationToken cancellationToken)
    {
        if (!IsValidIPv4(ipAddress))
        {
            return null;
        }

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    "-NoProfile -NonInteractive -Command \"$h='" + ipAddress
                    + "'; $os=Get-CimInstance -ComputerName $h -ClassName Win32_OperatingSystem | Select-Object Caption,LastBootUpTime,TotalVisibleMemorySize,FreePhysicalMemory; $disks=@(Get-CimInstance -ComputerName $h -ClassName Win32_LogicalDisk -Filter 'DriveType=3' | Select-Object DeviceID,Size,FreeSpace); [pscustomobject]@{ os=$os; disks=$disks } | ConvertTo-Json -Depth 4\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode == 0 ? ParseInventoryJson(output) : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed record ServiceProbe(IReadOnlyList<StatusServiceInfo>? Services, string? Error);

    private static string ServiceFilter(IReadOnlyList<string> names)
    {
        var clauses = new List<string>();
        foreach (var name in names)
        {
            clauses.Add($"Name='{name}'");
            if (name.Equals("MSSQLSERVER", StringComparison.OrdinalIgnoreCase))
            {
                clauses.Add("Name LIKE 'MSSQL$%'");
            }

            if (name.Equals("SQLSERVERAGENT", StringComparison.OrdinalIgnoreCase))
            {
                clauses.Add("Name LIKE 'SQLAgent$%'");
            }
        }

        return string.Join(" OR ", clauses);
    }

    private static string ShortError(string? error, string fallback)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return fallback;
        }

        var text = error.ReplaceLineEndings(" ").Trim();
        return text.Length <= 220 ? text : text[..220];
    }

    private static async Task<ServiceProbe> TryReadServicesAsync(
        string ipAddress,
        IReadOnlyList<string> names,
        CancellationToken cancellationToken)
    {
        if (!IsValidIPv4(ipAddress) || names.Count == 0)
        {
            return new ServiceProbe(null, null);
        }

        var filter = ServiceFilter(names).Replace("$", "`$", StringComparison.Ordinal);
        var script =
            "$h='" + ipAddress + "'\n" +
            "$filter=\"" + filter + "\"\n" +
            "$err=$null\n" +
            "$connected=$false\n" +
            "function Read-Services([string]$Protocol) {\n" +
            "  $opt = New-CimSessionOption -Protocol $Protocol\n" +
            "  $session = New-CimSession -ComputerName $h -SessionOption $opt -OperationTimeoutSec 4\n" +
            "  try {\n" +
            "    @(Get-CimInstance -CimSession $session -ClassName Win32_Service -Filter $filter | Select-Object Name,State,DisplayName)\n" +
            "  } finally { Remove-CimSession $session }\n" +
            "}\n" +
            "$result = @()\n" +
            "foreach ($protocol in @('Dcom')) {\n" +
            "  try {\n" +
            "    $result = @(Read-Services $protocol)\n" +
            "    $connected = $true\n" +
            "    break\n" +
            "  } catch { $err = $_.Exception.Message }\n" +
            "}\n" +
            "if (-not $connected) { [Console]::Error.WriteLine($err); exit 2 }\n" +
            "if ($result.Count -eq 0) { '[]' } else { $result | ConvertTo-Json -Depth 3 }\n";
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -EncodedCommand "
                    + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script)),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(start);
            if (process is null)
            {
                return new ServiceProbe(null, "Could not start PowerShell.");
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0)
            {
                return new ServiceProbe(null, string.IsNullOrWhiteSpace(error) ? output : error);
            }

            return new ServiceProbe(ParseServicesJson(names, output) ?? [], error);
        }
        catch (Exception ex)
        {
            return new ServiceProbe(null, ex.Message);
        }
    }

    internal static IReadOnlyList<StatusServiceInfo>? ParseServicesJson(IReadOnlyList<string> watched, string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json[0] is not '{' and not '[')
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var nodes = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToList()
            : [document.RootElement];
        var found = new Dictionary<string, StatusServiceInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var name = node.TryGetProperty("Name", out var nameNode) ? nameNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var state = node.TryGetProperty("State", out var stateNode) ? stateNode.GetString() : "Unknown";
            var display = node.TryGetProperty("DisplayName", out var displayNode) ? displayNode.GetString() : null;
            found[name] = new StatusServiceInfo
            {
                Name = name,
                DisplayName = string.IsNullOrWhiteSpace(display)
                    ? StatusServerServices.DisplayName(name)
                    : display,
                Status = string.IsNullOrWhiteSpace(state) ? "Unknown" : state,
                Detail = state
            };
        }

        return StatusServerServices.FromReadings(watched, found);
    }

    internal static InventoryReading? ParseInventoryJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json[0] is not '{' and not '[')
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        string? os = null;
        DateTimeOffset? lastBoot = null;
        string? uptime = null;
        double? memory = null;
        if (root.TryGetProperty("os", out var osNode) && osNode.ValueKind == JsonValueKind.Object)
        {
            os = osNode.TryGetProperty("Caption", out var caption) ? caption.GetString() : null;
            if (osNode.TryGetProperty("LastBootUpTime", out var boot) && DateTimeOffset.TryParse(boot.GetString(), out var parsedBoot))
            {
                lastBoot = parsedBoot;
                var span = DateTimeOffset.UtcNow - parsedBoot.ToUniversalTime();
                uptime = span.TotalDays >= 1
                    ? $"{span.TotalDays:0.0} days"
                    : $"{span.TotalHours:0.0} hours";
            }

            var total = ReadNumber(osNode, "TotalVisibleMemorySize");
            var free = ReadNumber(osNode, "FreePhysicalMemory");
            if (total > 0)
            {
                memory = Math.Round((total - free) * 100d / total, 1);
            }
        }

        double? storage = null;
        if (root.TryGetProperty("disks", out var disksNode))
        {
            var disks = disksNode.ValueKind == JsonValueKind.Array
                ? disksNode.EnumerateArray().ToList()
                : [disksNode];
            var usages = disks
                .Select(disk =>
                {
                    var size = ReadNumber(disk, "Size");
                    var free = ReadNumber(disk, "FreeSpace");
                    return size > 0 ? (size - free) * 100d / size : (double?)null;
                })
                .Where(value => value is not null)
                .Select(value => value!.Value)
                .ToList();
            if (usages.Count > 0)
            {
                storage = Math.Round(usages.Max(), 1);
            }
        }

        if (os is null && storage is null && memory is null)
        {
            return null;
        }

        return new InventoryReading(os, lastBoot, uptime, memory, storage);
    }

    private static double ReadNumber(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }

    internal sealed record Reachability(bool Online, int? LatencyMs, int? Port, string? Method, string Detail);

    internal sealed record InventoryReading(
        string? OperatingSystem,
        DateTimeOffset? LastBoot,
        string? Uptime,
        double? MemoryUsedPercent,
        double? StorageUsedPercent);
}
