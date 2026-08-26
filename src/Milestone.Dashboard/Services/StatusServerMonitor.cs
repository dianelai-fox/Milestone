using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public sealed class StatusServerMonitor
{
    private static readonly int[] ProbePorts = [445, 3389];

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

    private static async Task<StatusServerInfo> ProbeOneAsync(
        StatusServerCatalog.Spec spec,
        CancellationToken cancellationToken)
    {
        return new StatusServerInfo
        {
            Id = $"status:{spec.Name}",
            Name = spec.Name,
            IpAddress = spec.IpAddress,
            Deck = spec.Deck,
            Online = await IsReachableAsync(spec.IpAddress, cancellationToken)
        };
    }

    internal static async Task<bool> IsReachableAsync(string ipAddress, CancellationToken cancellationToken)
    {
        if (!IsValidIPv4(ipAddress))
        {
            return false;
        }

        var checks = ProbePorts.Select(port => TryConnectAsync(ipAddress, port, cancellationToken));
        var results = await Task.WhenAll(checks);
        if (results.Any(connected => connected))
        {
            return true;
        }

        return OperatingSystem.IsWindows() && await TryPingAsync(ipAddress);
    }

    private static async Task<bool> TryConnectAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        try
        {
            var address = IPAddress.Parse(ipAddress);
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(1000));
            await socket.ConnectAsync(new IPEndPoint(address, port), timeout.Token);
            return socket.Connected
                   && socket.RemoteEndPoint is IPEndPoint remote
                   && remote.Address.Equals(address);
        }
        catch
        {
            return false;
        }
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
}
