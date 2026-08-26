using System.Text.Json;

namespace Milestone.Dashboard.Services;

public sealed class StatusServerInventoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public StatusServerInventoryStore(IWebHostEnvironment environment, ILogger<StatusServerInventoryStore> logger)
    {
        _path = ResolvePath(environment, logger);
    }

    private static string ResolvePath(IWebHostEnvironment environment, ILogger logger)
    {
        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "App_Data"),
            Path.Combine(environment.WebRootPath ?? environment.ContentRootPath, "..", "App_Data"),
            Path.Combine(Path.GetTempPath(), "MilestoneDashboard")
        };

        foreach (var folder in candidates)
        {
            try
            {
                var fullFolder = Path.GetFullPath(folder);
                Directory.CreateDirectory(fullFolder);
                var probe = Path.Combine(fullFolder, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return Path.Combine(fullFolder, "status-servers.json");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not use server status folder {Folder}", folder);
            }
        }

        logger.LogError(
            "IIS cannot write server status. Grant Modify on C:\\inetpub\\xprotect-dashboard\\App_Data to IIS AppPool\\XProtectDashboard.");
        return Path.Combine(Path.GetTempPath(), "MilestoneDashboard", "status-servers.json");
    }

    public async Task<IReadOnlyList<StatusServerCatalog.Spec>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await ReadUnlockedAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<StatusServerCatalog.Spec>> ResolveAsync(
        StatusServerCatalog catalog,
        CancellationToken cancellationToken)
    {
        var saved = await GetAllAsync(cancellationToken);
        return saved.Count > 0 ? saved : catalog.List();
    }

    public async Task<IReadOnlyList<StatusServerCatalog.Spec>> ImportAsync(
        IReadOnlyList<StatusServerCatalog.Spec> imported,
        bool replaceDecks,
        StatusServerCatalog catalog,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadUnlockedAsync(cancellationToken);
            if (current.Count == 0)
            {
                current = catalog.List();
            }

            IReadOnlyList<StatusServerCatalog.Spec> next;
            if (replaceDecks)
            {
                var decks = imported
                    .Select(server => server.Deck)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                next = [.. imported, .. current.Where(server => !decks.Contains(server.Deck))];
            }
            else
            {
                var byName = current.ToDictionary(server => server.Name, StringComparer.OrdinalIgnoreCase);
                foreach (var row in imported)
                {
                    byName[row.Name] = row;
                }

                next = byName.Values.ToList();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await WriteUnlockedAsync(next, cancellationToken);
            return next;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<StatusServerCatalog.Spec>> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            var items = await JsonSerializer.DeserializeAsync<List<StatusServerCatalog.Spec>>(
                            stream, JsonOptions, cancellationToken)
                        ?? [];
            return items.Where(item => !string.IsNullOrWhiteSpace(item.Name)).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private async Task WriteUnlockedAsync(
        IReadOnlyList<StatusServerCatalog.Spec> servers,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, servers, JsonOptions, cancellationToken);
    }
}
