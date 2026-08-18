using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public interface IVmsClient
{
    string SourceName { get; }

    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}
