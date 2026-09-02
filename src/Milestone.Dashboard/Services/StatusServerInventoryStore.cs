using System.Text.Json;

namespace Milestone.Dashboard.Services;

public sealed class StatusServerInventoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string? _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<StatusServerInventoryStore> _logger;

    public StatusServerInventoryStore(IWebHostEnvironment environment, ILogger<StatusServerInventoryStore> logger)
    {
        _logger = logger;
        _path = AppDataPaths.CombineFile(environment, "status-servers.json", logger);
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
            var byKey = StatusServerCatalog.IndexByIdentity(current);
            foreach (var row in imported)
            {
                var key = StatusServerCatalog.IdentityKey(row);
                if (key.Length == 0)
                {
                    continue;
                }

                byKey[key] = byKey.TryGetValue(key, out var existing)
                    ? StatusServerCatalog.Merge(existing, row)
                    : row;
            }

            if (replaceDecks)
            {
                var importedKeys = imported
                    .Select(StatusServerCatalog.IdentityKey)
                    .Where(key => key.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var importedDecks = imported
                    .Select(StatusServerCatalog.ResolveDeck)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                next = byKey.Values
                    .Where(server =>
                        importedKeys.Contains(StatusServerCatalog.IdentityKey(server))
                        || !importedDecks.Contains(StatusServerCatalog.ResolveDeck(server)))
                    .ToList();
            }
            else
            {
                next = byKey.Values.ToList();
            }

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
            if (_path is not null && File.Exists(_path))
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
        if (_path is null || !File.Exists(_path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            var items = await JsonSerializer.DeserializeAsync<List<StatusServerCatalog.Spec>>(
                            stream, JsonOptions, cancellationToken)
                        ?? [];
            return StatusServerCatalog.IndexByIdentity(items.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
                .Values
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read server status inventory from {Path}", _path);
            return [];
        }
    }

    private async Task WriteUnlockedAsync(
        IReadOnlyList<StatusServerCatalog.Spec> servers,
        CancellationToken cancellationToken)
    {
        if (_path is null)
        {
            throw new InvalidOperationException(AppDataPaths.GrantHint);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, servers, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
