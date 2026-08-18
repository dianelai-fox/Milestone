using System.Net.Http.Headers;
using System.Text.Json;
using Milestone.Dashboard.Models;
using Milestone.Dashboard.Options;

namespace Milestone.Dashboard.Services;

public sealed class MilestoneApiClient : IVmsClient
{
    private readonly HttpClient _httpClient;
    private readonly MilestoneOptions _options;
    private readonly ILogger<MilestoneApiClient> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public MilestoneApiClient(HttpClient httpClient, MilestoneOptions options, ILogger<MilestoneApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public string SourceName => "milestone-api";

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var cameras = await GetPagedAsync("cameras", cancellationToken);
        var hardware = (await GetPagedAsync("hardware", cancellationToken))
            .ToDictionary(item => ReadString(item, "id"), item => item, StringComparer.OrdinalIgnoreCase);
        var recordingServers = await GetPagedAsync("recordingServers", cancellationToken);
        var sites = await GetPagedAsync("sites", cancellationToken);
        var siteName = sites.Count > 0 ? ReadString(sites[0], "displayName") : null;

        var hardwareLookup = hardware.ToDictionary(
            pair => pair.Key,
            pair => new
            {
                Name = ReadString(pair.Value, "displayName") ?? ReadString(pair.Value, "name"),
                Address = ReadString(pair.Value, "address"),
                RecordingServerId = ReadRelationId(pair.Value, "parent")
            },
            StringComparer.OrdinalIgnoreCase);

        var serverLookup = recordingServers.ToDictionary(
            item => ReadString(item, "id"),
            item => ReadString(item, "displayName") ?? ReadString(item, "name") ?? "Recording server",
            StringComparer.OrdinalIgnoreCase);

        var cameraModels = cameras.Select(item =>
        {
            var id = ReadString(item, "id");
            var hardwareId = ReadRelationId(item, "parent");
            hardwareLookup.TryGetValue(hardwareId ?? string.Empty, out var hw);
            var recordingServerId = hw?.RecordingServerId;
            return new CameraInfo
            {
                Id = id,
                Name = ReadString(item, "displayName") ?? ReadString(item, "name") ?? id,
                Description = ReadString(item, "description"),
                Enabled = ReadBool(item, "enabled"),
                HardwareId = hardwareId,
                HardwareName = hw?.Name,
                HardwareAddress = hw?.Address,
                RecordingServerId = recordingServerId,
                RecordingServerName = recordingServerId is not null && serverLookup.TryGetValue(recordingServerId, out var recName)
                    ? recName
                    : null,
                Site = siteName,
                Location = GisPointParser.Parse(ReadString(item, "gisPoint"))
            };
        }).ToList();

        var storages = new List<StorageVolume>();
        foreach (var server in recordingServers)
        {
            var serverId = ReadString(server, "id");
            var serverName = serverLookup.GetValueOrDefault(serverId, serverId);
            var serverStorages = await GetChildArrayAsync($"recordingServers/{serverId}/storages", cancellationToken);
            foreach (var storage in serverStorages)
            {
                storages.Add(await MapStorageAsync(storage, serverId, serverName, "Recording", cancellationToken));
                var storageId = ReadString(storage, "id");
                var archives = await GetChildArrayAsync($"storages/{storageId}/archiveStorages", cancellationToken);
                foreach (var archive in archives)
                {
                    storages.Add(await MapStorageAsync(archive, serverId, serverName, "Archive", cancellationToken));
                }
            }
        }

        var servers = recordingServers.Select(item =>
        {
            var id = ReadString(item, "id");
            return new RecordingServerInfo
            {
                Id = id,
                Name = serverLookup.GetValueOrDefault(id, id),
                HostName = ReadString(item, "hostName") ?? ReadString(item, "webServerUri"),
                Enabled = ReadBool(item, "enabled", true),
                CameraCount = cameraModels.Count(c => c.RecordingServerId == id),
                UsedSpaceMb = storages.Where(s => s.RecordingServerId == id).Sum(s => s.UsedSpaceMb),
                MaxSizeMb = storages.Where(s => s.RecordingServerId == id).Sum(s => s.MaxSizeMb)
            };
        }).ToList();

        return new DashboardSnapshot
        {
            Source = SourceName,
            SiteName = siteName,
            Cameras = cameraModels,
            Storages = storages,
            RecordingServers = servers
        };
    }

    private async Task<StorageVolume> MapStorageAsync(
        JsonElement item,
        string recordingServerId,
        string recordingServerName,
        string kind,
        CancellationToken cancellationToken)
    {
        var id = ReadString(item, "id");
        var info = await TryGetStorageInformationAsync(id, cancellationToken);
        return new StorageVolume
        {
            Id = id,
            Name = ReadString(item, "displayName") ?? ReadString(item, "name") ?? id,
            RecordingServerId = recordingServerId,
            RecordingServerName = recordingServerName,
            DiskPath = ReadString(item, "diskPath"),
            Kind = kind,
            MaxSizeMb = ReadLong(item, "maxSize"),
            UsedSpaceMb = info is null ? 0 : ReadLong(info.Value, "usedSpace"),
            LockedUsedSpaceMb = info is null ? 0 : ReadLong(info.Value, "lockedUsedSpace"),
            RetainMinutes = (int)ReadLong(item, "retainMinutes"),
            IsDefault = ReadBool(item, "isDefault"),
            IsAvailable = info is null || ReadBool(info.Value, "isAvailable", true),
            IsMounted = info is null || ReadBool(info.Value, "isMounted", true),
            EncryptionMethod = ReadString(item, "encryptionMethod")
        };
    }

    private async Task<JsonElement?> TryGetStorageInformationAsync(string storageId, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await SendJsonAsync($"storageInformation/{storageId}", cancellationToken);
            if (document.RootElement.TryGetProperty("data", out var data))
            {
                return data.Clone();
            }

            return document.RootElement.Clone();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Storage information was not available for {StorageId}", storageId);
            return null;
        }
    }

    private async Task<List<JsonElement>> GetPagedAsync(string resource, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        var page = 0;
        while (true)
        {
            using var document = await SendJsonAsync($"{resource}?page={page}&size={_options.PageSize}", cancellationToken);
            var pageItems = ReadArray(document.RootElement);
            if (pageItems.Count == 0)
            {
                break;
            }

            items.AddRange(pageItems);
            if (pageItems.Count < _options.PageSize)
            {
                break;
            }

            page++;
        }

        return items;
    }

    private async Task<List<JsonElement>> GetChildArrayAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await SendJsonAsync(path, cancellationToken);
            return ReadArray(document.RootElement);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Optional resource {Path} was not available", path);
            return [];
        }
    }

    private async Task<JsonDocument> SendJsonAsync(string relativePath, CancellationToken cancellationToken)
    {
        await EnsureTokenAsync(cancellationToken);
        var url = $"{_options.ResolvedApiBaseUrl()}/{relativePath.TrimStart('/')}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private async Task EnsureTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return;
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = _options.Username,
                ["password"] = _options.Password,
                ["client_id"] = _options.ClientId
            });
            using var response = await _httpClient.PostAsync(_options.ResolvedTokenUrl(), content, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            _accessToken = document.RootElement.GetProperty("access_token").GetString();
            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expires)
                ? expires.GetInt32()
                : 3600;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static List<JsonElement> ReadArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().Select(item => item.Clone()).ToList();
        }

        if (root.TryGetProperty("array", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            return array.EnumerateArray().Select(item => item.Clone()).ToList();
        }

        return [];
    }

    private static string ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool ReadBool(JsonElement element, string name, bool fallback = false)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => fallback
        };
    }

    private static long ReadLong(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }

    private static string? ReadRelationId(JsonElement element, string relationName)
    {
        if (!element.TryGetProperty("relations", out var relations))
        {
            return null;
        }

        if (!relations.TryGetProperty(relationName, out var relation))
        {
            return null;
        }

        return relation.TryGetProperty("id", out var id) ? id.GetString() : null;
    }
}
