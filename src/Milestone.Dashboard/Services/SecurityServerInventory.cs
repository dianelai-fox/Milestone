using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public static class SecurityServerInventory
{
    public static SecurityServersOverview From(
        IEnumerable<RecordingServerInfo> servers,
        IEnumerable<StorageVolume> storages)
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
        }

        var list = servers
            .OrderBy(server => server.Enabled ? 1 : 0)
            .ThenByDescending(server => server.EffectiveStorageUsagePercent)
            .ThenBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var used = list.Sum(server => server.UsedSpaceMb);
        var max = list.Sum(server => server.MaxSizeMb);
        return new SecurityServersOverview
        {
            TotalServers = list.Count,
            OnlineCount = list.Count(server => server.Enabled),
            OfflineCount = list.Count(server => !server.Enabled),
            StorageHealthyCount = list.Count(server => server.StorageHealth == "Healthy"),
            StorageWarningCount = list.Count(server => server.StorageHealth == "Warning"),
            StorageCriticalCount = list.Count(server => server.StorageHealth == "Critical"),
            CpuHealthyCount = list.Count(server => server.CpuHealth == "Healthy"),
            CpuAttentionCount = list.Count(server => server.CpuHealth is "Warning" or "Critical"),
            CpuUnreportedCount = list.Count(server => server.CpuHealth == "Not reported"),
            AttentionCount = list.Count(server => server.NeedsAttention),
            UsedSpaceMb = used,
            MaxSizeMb = max,
            StorageUsagePercent = StorageMetrics.UsagePercent(used, max),
            Servers = list,
            AttentionServers = list.Where(server => server.NeedsAttention).ToList()
        };
    }
}
