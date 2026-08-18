using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Milestone.Dashboard.Data;
using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public sealed class SnapshotCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SnapshotCache> _logger;
    public bool IsEnabled { get; }

    public SnapshotCache(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<SnapshotCache> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        IsEnabled = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Dashboard"));
    }

    public async Task SaveAsync(DashboardSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
            db.Snapshots.RemoveRange(await db.Snapshots.ToListAsync(cancellationToken));
            db.Snapshots.Add(new CachedSnapshotEntity
            {
                GeneratedAt = snapshot.GeneratedAt,
                Source = snapshot.Source,
                Json = JsonSerializer.Serialize(snapshot, JsonOptions)
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist the dashboard snapshot to SQL Server.");
        }
    }

    public async Task<DashboardSnapshot?> TryLoadAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return null;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
            var latest = await db.Snapshots.OrderByDescending(item => item.GeneratedAt).FirstOrDefaultAsync(cancellationToken);
            return latest is null ? null : JsonSerializer.Deserialize<DashboardSnapshot>(latest.Json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load a cached dashboard snapshot from SQL Server.");
            return null;
        }
    }
}
