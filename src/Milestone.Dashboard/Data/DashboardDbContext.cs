using Microsoft.EntityFrameworkCore;

namespace Milestone.Dashboard.Data;

public sealed class DashboardDbContext : DbContext
{
    public DashboardDbContext(DbContextOptions<DashboardDbContext> options) : base(options)
    {
    }

    public DbSet<CachedSnapshotEntity> Snapshots => Set<CachedSnapshotEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CachedSnapshotEntity>(entity =>
        {
            entity.ToTable("DashboardSnapshots");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Source).HasMaxLength(64);
            entity.Property(item => item.Json).IsRequired();
        });
    }
}

public sealed class CachedSnapshotEntity
{
    public int Id { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Json { get; set; } = string.Empty;
}
