using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class AppDataPathsTests
{
    [Fact]
    public void Uses_site_App_Data_and_never_Windows_TEMP()
    {
        using var root = new TempSite();
        var env = new StubWebHostEnvironment
        {
            ContentRootPath = root.Path,
            WebRootPath = Path.Combine(root.Path, "wwwroot")
        };

        var folder = AppDataPaths.TryGetWritableDirectory(env);

        Assert.Equal(Path.Combine(root.Path, "App_Data"), folder);
        var forbidden = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MilestoneDashboard"));
        Assert.All(AppDataPaths.CandidateFolders(env), candidate =>
        {
            var full = Path.GetFullPath(candidate);
            Assert.False(
                full.Equals(forbidden, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(forbidden + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public async Task Returns_null_instead_of_system_temp_when_App_Data_is_not_writable()
    {
        using var root = new TempSite();
        var appData = Path.Combine(root.Path, "App_Data");
        Directory.CreateDirectory(appData);
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(appData, UnixFileMode.None);
        try
        {
            var env = new StubWebHostEnvironment
            {
                ContentRootPath = root.Path,
                WebRootPath = Path.Combine(root.Path, "wwwroot")
            };

            Assert.Null(AppDataPaths.TryGetWritableDirectory(env));
            var store = new StatusServerInventoryStore(env, NullLogger<StatusServerInventoryStore>.Instance);
            Assert.Empty(await store.GetAllAsync(CancellationToken.None));
        }
        finally
        {
            File.SetUnixFileMode(appData, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public async Task Import_explains_missing_App_Data_permission_instead_of_using_TEMP()
    {
        using var root = new TempSite();
        var appData = Path.Combine(root.Path, "App_Data");
        Directory.CreateDirectory(appData);
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(appData, UnixFileMode.None);
        try
        {
            var env = new StubWebHostEnvironment
            {
                ContentRootPath = root.Path,
                WebRootPath = Path.Combine(root.Path, "wwwroot")
            };
            var store = new StatusServerInventoryStore(env, NullLogger<StatusServerInventoryStore>.Instance);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.ImportAsync(
                    [new StatusServerCatalog.Spec { Name = "FOX1", IpAddress = "10.0.0.1", Deck = "MasterMind" }],
                    replaceDecks: false,
                    new StatusServerCatalog(),
                    CancellationToken.None));
            Assert.Contains("App_Data", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(@"C:\Windows\TEMP", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.SetUnixFileMode(appData, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private sealed class TempSite : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "MilestoneDashboardTests",
            Guid.NewGuid().ToString("N"));

        public TempSite()
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "wwwroot"));
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Milestone.Dashboard.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
