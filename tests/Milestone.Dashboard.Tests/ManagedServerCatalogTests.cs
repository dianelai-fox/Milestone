using Microsoft.Extensions.Logging.Abstractions;
using Milestone.Dashboard.Options;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class ManagedServerCatalogTests
{
    [Fact]
    public void Always_includes_the_fox_lenel_application_server()
    {
        var catalog = new ManagedServerCatalog(
            new MilestoneOptions(),
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json"),
            NullLogger<ManagedServerCatalog>.Instance);

        var lenel = Assert.Single(catalog.List());
        Assert.Equal("FOXUSWDMSIA297", lenel.Name);
        Assert.Equal("LENELNEWAPP.INT.APPS.FOX", lenel.HostName);
        Assert.Equal("Lenel application", lenel.Role);
        Assert.Equal("Lenel", lenel.Application);
    }
}
