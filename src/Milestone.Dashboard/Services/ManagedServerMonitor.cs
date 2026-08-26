using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using Milestone.Dashboard.Models;
using Milestone.Dashboard.Options;

namespace Milestone.Dashboard.Services;

public sealed class ManagedServerMonitor
{
    private static readonly Regex HostPattern = new(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled);
    private readonly ILogger<ManagedServerMonitor> _logger;

    public ManagedServerMonitor(ILogger<ManagedServerMonitor> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<RecordingServerInfo>> ProbeAsync(
        IEnumerable<ManagedServerSpec> specs,
        CancellationToken cancellationToken)
    {
        var tasks = specs.Select(spec => ProbeOneAsync(spec, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    public async Task<RecordingServerInfo> ProbeOneAsync(ManagedServerSpec spec, CancellationToken cancellationToken)
    {
        var host = spec.ResolvedHost();
        var name = string.IsNullOrWhiteSpace(spec.Name) ? host : spec.Name.Trim();
        var online = await IsReachableAsync(host, spec.ProbePorts, cancellationToken);
        var storage = online && spec.CheckStorage
            ? await TryReadStorageAsync(host, cancellationToken)
            : null;

        return new RecordingServerInfo
        {
            Id = $"managed:{host}",
            Name = name,
            HostName = host,
            Enabled = online,
            Role = string.IsNullOrWhiteSpace(spec.Role) ? spec.Application ?? "Application server" : spec.Role,
            Application = spec.Application ?? spec.Role,
            Kind = "application",
            Source = "Managed",
            VolumeCount = storage?.VolumeCount ?? 0,
            UsedSpaceMb = storage?.UsedSpaceMb ?? 0,
            MaxSizeMb = storage?.MaxSizeMb ?? 0,
            WorstVolumeUsagePercent = storage?.WorstVolumeUsagePercent ?? 0,
            StorageReported = storage is not null
        };
    }

    internal static bool IsValidHost(string host) =>
        !string.IsNullOrWhiteSpace(host) && HostPattern.IsMatch(host);

    internal async Task<bool> IsReachableAsync(string host, IEnumerable<int> ports, CancellationToken cancellationToken)
    {
        if (!IsValidHost(host))
        {
            return false;
        }

        foreach (var port in ports.Where(port => port is > 0 and < 65536).Distinct())
        {
            if (await TryConnectAsync(host, port, cancellationToken))
            {
                return true;
            }
        }

        return await TryPingAsync(host, cancellationToken);
    }

    private static async Task<bool> TryConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(1500));
            await client.ConnectAsync(host, port, timeout.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryPingAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 1500);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private async Task<StorageReading?> TryReadStorageAsync(string host, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || !IsValidHost(host))
        {
            return null;
        }

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    "-NoProfile -NonInteractive -Command \"Get-CimInstance -ComputerName '"
                    + host
                    + "' -ClassName Win32_LogicalDisk -Filter 'DriveType=3' | Select-Object DeviceID,Size,FreeSpace | ConvertTo-Json\"",
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
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            return ParseDiskJson(output);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read disk space on {Host}", host);
            return null;
        }
    }

    internal static StorageReading? ParseDiskJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var disks = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToList()
            : [document.RootElement];

        var volumes = disks
            .Select(disk =>
            {
                var size = ReadSize(disk, "Size");
                var free = ReadSize(disk, "FreeSpace");
                return (Size: size, Used: Math.Max(0, size - free));
            })
            .Where(disk => disk.Size > 0)
            .ToList();

        if (volumes.Count == 0)
        {
            return null;
        }

        var usedBytes = volumes.Sum(disk => disk.Used);
        var maxBytes = volumes.Sum(disk => disk.Size);
        return new StorageReading(
            ToMb(usedBytes),
            ToMb(maxBytes),
            volumes.Count,
            volumes.Max(disk => StorageMetrics.UsagePercent(ToMb(disk.Used), ToMb(disk.Size))));
    }

    private static long ReadSize(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var number)
            ? number
            : 0;

    private static long ToMb(long bytes) => Math.Max(0, bytes / (1024 * 1024));

    internal sealed record StorageReading(long UsedSpaceMb, long MaxSizeMb, int VolumeCount, double WorstVolumeUsagePercent);
}
