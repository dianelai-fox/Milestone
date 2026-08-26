using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Milestone.Dashboard.Models;
using Milestone.Dashboard.Options;

namespace Milestone.Dashboard.Services;

public sealed class MonitoredServerProbe
{
    private static readonly Regex HostPattern = new(@"^[A-Za-z0-9._:-]+$", RegexOptions.Compiled);

    public MonitoredServerProbe()
    {
    }

    public async Task<ServerStatusOverview> ProbeAsync(
        IEnumerable<MonitoredServerSpec> specs,
        bool demoOnly,
        CancellationToken cancellationToken)
    {
        var list = specs.ToList();
        var checkedAt = DateTimeOffset.UtcNow;
        if (demoOnly)
        {
            var demo = list.Select((spec, index) => ToInfo(spec, online: index != 1, reachableVia: index != 1 ? "demo" : null, checkedAt, isDemo: true)).ToList();
            return Overview(demo, "demo", checkedAt);
        }

        var results = await Task.WhenAll(list.Select(spec => ProbeOneAsync(spec, checkedAt, cancellationToken)));
        return Overview(results, "live", checkedAt);
    }

    public async Task<MonitoredServerInfo> ProbeOneAsync(
        MonitoredServerSpec spec,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        string? reachableVia = null;
        foreach (var target in spec.ProbeTargets())
        {
            if (await IsReachableAsync(target, spec.ProbePorts, cancellationToken))
            {
                reachableVia = target;
                break;
            }
        }

        return ToInfo(spec, online: reachableVia is not null, reachableVia, checkedAt, isDemo: false);
    }

    internal static bool IsValidHost(string host) =>
        !string.IsNullOrWhiteSpace(host) && HostPattern.IsMatch(host.Trim());

    internal async Task<bool> IsReachableAsync(string host, IEnumerable<int> ports, CancellationToken cancellationToken)
    {
        if (!IsValidHost(host))
        {
            return false;
        }

        host = host.Trim();
        if (!await HostExistsAsync(host, cancellationToken))
        {
            return false;
        }

        var candidates = ports.Where(port => port is > 0 and < 65536).Distinct().ToArray();
        if (candidates.Length > 0)
        {
            var checks = candidates.Select(port => TryConnectAsync(host, port, cancellationToken));
            var results = await Task.WhenAll(checks);
            if (results.Any(connected => connected))
            {
                return true;
            }
        }

        return await TryPingAsync(host, cancellationToken);
    }

    private static async Task<bool> HostExistsAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out _))
        {
            return true;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
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
            timeout.CancelAfter(TimeSpan.FromMilliseconds(1200));
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
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(1500));
            var reply = await ping.SendPingAsync(host, 1200);
            return reply.Status == IPStatus.Success;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static MonitoredServerInfo ToInfo(
        MonitoredServerSpec spec,
        bool online,
        string? reachableVia,
        DateTimeOffset checkedAt,
        bool isDemo)
    {
        var name = spec.DisplayName();
        return new MonitoredServerInfo
        {
            Id = $"monitored:{name}",
            Name = name,
            HostName = string.IsNullOrWhiteSpace(spec.HostName) ? null : spec.HostName.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(spec.IpAddress) ? null : spec.IpAddress.Trim(),
            Role = string.IsNullOrWhiteSpace(spec.Role) ? "Server" : spec.Role.Trim(),
            Online = online,
            ReachableVia = reachableVia,
            CheckedAt = checkedAt.ToString("u"),
            IsDemo = isDemo
        };
    }

    private static ServerStatusOverview Overview(
        IReadOnlyList<MonitoredServerInfo> servers,
        string source,
        DateTimeOffset checkedAt)
    {
        var ordered = servers
            .OrderBy(server => server.Online ? 1 : 0)
            .ThenBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ServerStatusOverview
        {
            TotalServers = ordered.Count,
            OnlineCount = ordered.Count(server => server.Online),
            OfflineCount = ordered.Count(server => !server.Online),
            Source = source,
            CheckedAt = checkedAt,
            Servers = ordered
        };
    }
}
