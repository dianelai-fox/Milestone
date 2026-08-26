using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Milestone.Dashboard.Models;
using Milestone.Dashboard.Options;

namespace Milestone.Dashboard.Services;

public sealed class MonitoredServerMonitor
{
    private static readonly Regex HostPattern = new(@"^[A-Za-z0-9._:-]+$", RegexOptions.Compiled);
    private readonly ILogger<MonitoredServerMonitor> _logger;

    public MonitoredServerMonitor(ILogger<MonitoredServerMonitor> logger)
    {
        _logger = logger;
    }

    public async Task<ServerStatusOverview> ProbeAsync(
        IEnumerable<MonitoredServerSpec> specs,
        CancellationToken cancellationToken)
    {
        var tasks = specs.Select(spec => ProbeOneAsync(spec, cancellationToken));
        var servers = await Task.WhenAll(tasks);
        return ServerStatusOverview.From(servers);
    }

    public async Task<MonitoredServerStatus> ProbeOneAsync(MonitoredServerSpec spec, CancellationToken cancellationToken)
    {
        var name = spec.DisplayName();
        var targets = spec.ProbeTargets().Where(IsValidHost).ToList();
        var online = false;
        string? reachableOn = null;
        string? ipAddress = string.IsNullOrWhiteSpace(spec.IpAddress) ? null : spec.IpAddress.Trim();

        foreach (var target in targets)
        {
            if (IPAddress.TryParse(target, out var parsed))
            {
                ipAddress ??= parsed.ToString();
            }
            else
            {
                ipAddress ??= await TryResolveIpAsync(target, cancellationToken);
            }

            if (await IsReachableAsync(target, spec.ResolvedPorts(), cancellationToken))
            {
                online = true;
                reachableOn = target;
                break;
            }
        }

        _logger.LogDebug(
            "Server {Name} is {Status}. Targets: {Targets}. Reachable on {ReachableOn}.",
            name,
            online ? "online" : "offline",
            string.Join(", ", targets),
            reachableOn ?? "none");

        return new MonitoredServerStatus
        {
            Id = $"monitored:{name}",
            Name = name,
            HostName = string.IsNullOrWhiteSpace(spec.HostName) ? null : spec.HostName.Trim(),
            IpAddress = ipAddress,
            Role = string.IsNullOrWhiteSpace(spec.Role) ? "Server" : spec.Role.Trim(),
            Source = spec.Source,
            Online = online,
            CanRemove = string.Equals(spec.Source, "saved", StringComparison.OrdinalIgnoreCase),
            ReachableOn = reachableOn,
            CheckedAt = DateTimeOffset.UtcNow
        };
    }

    internal static bool IsValidHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out _))
        {
            return true;
        }

        return HostPattern.IsMatch(host) && host.Length <= 253;
    }

    internal async Task<bool> IsReachableAsync(string host, IEnumerable<int> ports, CancellationToken cancellationToken)
    {
        if (!IsValidHost(host))
        {
            return false;
        }

        if (!IPAddress.TryParse(host, out _) && !await HostExistsAsync(host, cancellationToken))
        {
            return false;
        }

        var candidates = ports.Where(port => port is > 0 and < 65536).Distinct().ToArray();
        if (candidates.Length > 0)
        {
            var checks = candidates.Select(port => TryConnectAsync(host, port, cancellationToken)).ToList();
            while (checks.Count > 0)
            {
                var finished = await Task.WhenAny(checks);
                checks.Remove(finished);
                if (await finished)
                {
                    return true;
                }
            }
        }

        return await TryPingAsync(host, cancellationToken);
    }

    private static async Task<string?> TryResolveIpAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(1500));
            var addresses = await Dns.GetHostAddressesAsync(host, timeout.Token);
            return addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork)?.ToString()
                   ?? addresses.FirstOrDefault()?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> HostExistsAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(1500));
            var addresses = await Dns.GetHostAddressesAsync(host, timeout.Token);
            return addresses.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(900));
            await client.ConnectAsync(host, port, timeout.Token);
            return client.Connected;
        }
        catch (SocketException ex) when (
            ex.SocketErrorCode is SocketError.ConnectionRefused or SocketError.HostDown)
        {
            // The host answered. A closed port still means the server is on the network.
            return ex.SocketErrorCode == SocketError.ConnectionRefused;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or HttpRequestException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryPingAsync(string host, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 800);
            return reply.Status == IPStatus.Success;
        }
        catch (Exception ex) when (ex is PingException or SocketException or OperationCanceledException)
        {
            return false;
        }
    }
}
