using Milestone.Dashboard.Models;
using Milestone.Dashboard.Options;

namespace Milestone.Dashboard.Services;

public sealed class ServerStatusService
{
    private readonly MonitoredServerCatalog _catalog;
    private readonly MonitoredServerProbe _probe;
    private readonly MilestoneOptions _options;

    public ServerStatusService(
        MonitoredServerCatalog catalog,
        MonitoredServerProbe probe,
        MilestoneOptions options)
    {
        _catalog = catalog;
        _probe = probe;
        _options = options;
    }

    public Task<ServerStatusOverview> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var configured = _catalog.List();
        if (configured.Count == 0)
        {
            if (_options.UseDemoData)
            {
                return _probe.ProbeAsync(MonitoredServerCatalog.DemoServers(), demoOnly: true, cancellationToken);
            }

            return _probe.ProbeAsync([], demoOnly: false, cancellationToken);
        }

        return _probe.ProbeAsync(configured, demoOnly: false, cancellationToken);
    }
}
