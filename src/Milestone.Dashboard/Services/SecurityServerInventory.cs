using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public static class SecurityServerInventory
{
    public static SecurityServersOverview From(
        IEnumerable<RecordingServerInfo> servers,
        IEnumerable<StorageVolume> storages,
        IEnumerable<RecordingServerInfo>? managedServers = null)
    {
        var volumeLookup = storages
            .GroupBy(storage => storage.RecordingServerId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var server in servers)
        {
            var volumes = volumeLookup.GetValueOrDefault(server.Id) ?? [];
            server.VolumeCount = volumes.Count;
            server.WorstVolumeUsagePercent = volumes.Count == 0
                ? StorageMetrics.UsagePercent(server.UsedSpaceMb, server.MaxSizeMb)
                : volumes.Max(volume => volume.UsagePercent);
            if (string.IsNullOrWhiteSpace(server.Role))
            {
                server.Role = "Recording server";
            }

            server.Kind = "recording";
            server.Source = string.IsNullOrWhiteSpace(server.Source) ? "XProtect" : server.Source;
            server.StorageReported = true;
        }

        foreach (var server in managedServers ?? [])
        {
            if (string.IsNullOrWhiteSpace(server.Role))
            {
                server.Role = server.Application ?? "Application server";
            }

            server.Kind = string.IsNullOrWhiteSpace(server.Kind) ? "application" : server.Kind;
            server.Source = string.IsNullOrWhiteSpace(server.Source) ? "Managed" : server.Source;
            if (string.IsNullOrWhiteSpace(server.DomainName))
            {
                server.DomainName = server.HostName;
            }
        }

        var list = servers.Concat(managedServers ?? [])
            .OrderBy(server => server.Enabled ? 1 : 0)
            .ThenByDescending(server => server.StorageReported ? server.EffectiveStorageUsagePercent : -1)
            .ThenBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var measured = list.Where(server => server.StorageReported).ToList();
        var used = measured.Sum(server => server.UsedSpaceMb);
        var max = measured.Sum(server => server.MaxSizeMb);
        return new SecurityServersOverview
        {
            TotalServers = list.Count,
            OnlineCount = list.Count(server => server.Enabled),
            OfflineCount = list.Count(server => !server.Enabled),
            StorageHealthyCount = list.Count(server => server.StorageHealth == "Healthy"),
            StorageWarningCount = list.Count(server => server.StorageHealth == "Warning"),
            StorageCriticalCount = list.Count(server => server.StorageHealth == "Critical"),
            StorageUnknownCount = list.Count(server => server.StorageHealth == "Not reported"),
            RecordingServerCount = list.Count(server => server.IsRecordingServer),
            ApplicationServerCount = list.Count(server => !server.IsRecordingServer),
            AttentionCount = list.Count(server => server.NeedsAttention),
            UsedSpaceMb = used,
            MaxSizeMb = max,
            StorageUsagePercent = StorageMetrics.UsagePercent(used, max),
            Servers = list,
            AttentionServers = list.Where(server => server.NeedsAttention).ToList()
        };
    }
}
