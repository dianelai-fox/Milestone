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

    public async Task<ServerStatusOverview> ProbeAsync(CancellationToken cancellationToken)
    {
        return await ProbeAsync(new StatusServerCatalog().List(), cancellationToken);
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
            .OrderBy(deck => deck.Name, StringComparer.OrdinalIgnoreCase)
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

        return new StatusServerInfo
        {
            Id = $"status:{spec.Name}",
            Name = spec.Name,
            IpAddress = spec.IpAddress,
            Role = spec.Role,
            Deck = spec.Deck,
            Online = reach.Online,
            CheckedAt = checkedAt,
            LatencyMs = reach.LatencyMs,
            ProbePort = reach.Port,
            ProbeMethod = reach.Method,
            Detail = reach.Detail,
            OperatingSystem = inventory?.OperatingSystem,
            LastBoot = inventory?.LastBoot,
            Uptime = inventory?.Uptime,
            MemoryUsedPercent = inventory?.MemoryUsedPercent,
            StorageUsedPercent = inventory?.StorageUsedPercent,
            StorageReported = inventory?.StorageUsedPercent is not null
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
