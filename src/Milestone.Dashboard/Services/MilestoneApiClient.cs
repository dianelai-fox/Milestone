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
        var cameras = await GetPagedAsync("cameras", cancellationToken, "includeChildren=customProperties");
        var hardwareItems = await GetPagedAsync("hardware", cancellationToken);
        var recordingServers = await GetPagedAsync("recordingServers", cancellationToken);
        var sites = await GetPagedAsync("sites", cancellationToken);
        var mapLocations = await GetChildArrayAsync("gisMapLocations", cancellationToken);
        var cameraGroups = await GetOptionalPagedAsync("cameraGroups", cancellationToken, "includeChildren=cameras");
        var hardwareDrivers = await GetOptionalPagedAsync("hardwareDrivers", cancellationToken);
        var driverSettings = await GetOptionalPagedAsync("hardwareDriverSettings", cancellationToken);
        var siteName = sites.Count > 0
            ? JsonElementReader.ReadOptionalString(sites[0], "displayName")
            : null;
        var groupLabels = CameraGroupIndex.Build(cameraGroups);

        var driverNames = hardwareDrivers.ToDictionary(
            item => JsonElementReader.ReadString(item, "id"),
            item => JsonElementReader.ReadOptionalString(item, "displayName")
                    ?? JsonElementReader.ReadOptionalString(item, "name")
                    ?? "Driver",
            StringComparer.OrdinalIgnoreCase);

        var settingsByHardware = new Dictionary<string, HardwareDeviceDetails>(StringComparer.OrdinalIgnoreCase);
        foreach (var settings in driverSettings)
        {
            var hardwareId = HardwareSettingsReader.ReadParentHardwareId(settings)
                             ?? JsonElementReader.ReadOptionalString(settings, "id");
            if (string.IsNullOrWhiteSpace(hardwareId))
            {
                continue;
            }

            var details = HardwareSettingsReader.Read(settings);
            settingsByHardware[hardwareId] = settingsByHardware.TryGetValue(hardwareId, out var existing)
                ? HardwareSettingsReader.Merge(existing, details)
                : details;
        }

        var hardwareLookup = hardwareItems.ToDictionary(
            item => JsonElementReader.ReadString(item, "id"),
            item =>
            {
                var id = JsonElementReader.ReadString(item, "id");
                settingsByHardware.TryGetValue(id, out var fromSettings);
                var fromHardware = HardwareSettingsReader.Read(item);
                var details = HardwareSettingsReader.Merge(fromSettings ?? new HardwareDeviceDetails(null, null, null, null), fromHardware);
                var driverId = JsonElementReader.ReadPathId(item, "hardwareDriverPath");
                return new
                {
                    Name = JsonElementReader.ReadOptionalString(item, "displayName")
                           ?? JsonElementReader.ReadOptionalString(item, "name"),
                    Address = JsonElementReader.ReadOptionalString(item, "address"),
                    UserName = JsonElementReader.ReadOptionalString(item, "userName"),
                    Enabled = JsonElementReader.ReadOptionalBool(item, "enabled"),
                    Model = details.Model
                            ?? JsonElementReader.ReadOptionalString(item, "model"),
                    Firmware = details.Firmware,
                    SerialNumber = details.SerialNumber,
                    MacAddress = details.MacAddress,
                    Driver = driverId is not null && driverNames.TryGetValue(driverId, out var driverName)
                        ? driverName
                        : null,
                    RecordingServerId = JsonElementReader.ReadRelationId(item, "parent"),
                    Location = GisPointParser.FromElement(item),
                    PasswordLastModified = JsonElementReader.ReadDate(item, "passwordLastModified"),
                    LastModified = JsonElementReader.ReadDate(item, "lastModified"),
                    CustomProperties = CustomPropertyReader.Read(item)
                };
            },
            StringComparer.OrdinalIgnoreCase);

        var serverLookup = recordingServers.ToDictionary(
            item => JsonElementReader.ReadString(item, "id"),
            item => JsonElementReader.ReadOptionalString(item, "displayName")
                    ?? JsonElementReader.ReadOptionalString(item, "name")
                    ?? "Recording server",
            StringComparer.OrdinalIgnoreCase);

        var cameraModels = cameras.Select(item =>
        {
            var id = JsonElementReader.ReadString(item, "id");
            var hardwareId = JsonElementReader.ReadRelationId(item, "parent");
            hardwareLookup.TryGetValue(hardwareId ?? string.Empty, out var hw);
            var recordingServerId = hw?.RecordingServerId;
            var cameraProperties = CustomPropertyReader.Read(item);
            var mergedProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (hw?.CustomProperties is not null)
            {
                foreach (var pair in hw.CustomProperties)
                {
                    mergedProperties[pair.Key] = pair.Value;
                }
            }

            foreach (var pair in cameraProperties)
            {
                mergedProperties[pair.Key] = pair.Value;
            }

            groupLabels.TryGetValue(id, out var labels);
            return new CameraInfo
            {
                Id = id,
                Name = JsonElementReader.ReadOptionalString(item, "displayName")
                       ?? JsonElementReader.ReadOptionalString(item, "name")
                       ?? id,
                ShortName = JsonElementReader.ReadOptionalString(item, "shortName"),
                Description = JsonElementReader.ReadOptionalString(item, "description"),
                Enabled = JsonElementReader.ReadBool(item, "enabled"),
                Channel = JsonElementReader.ReadOptionalInt(item, "channel"),
                HardwareId = hardwareId,
                HardwareName = hw?.Name,
                HardwareAddress = hw?.Address,
                HardwareUserName = hw?.UserName,
                HardwareEnabled = hw?.Enabled,
                HardwareDriver = hw?.Driver,
                Model = hw?.Model,
                Firmware = hw?.Firmware,
                SerialNumber = hw?.SerialNumber,
                MacAddress = hw?.MacAddress,
                RecordingServerId = recordingServerId,
                RecordingServerName = recordingServerId is not null && serverLookup.TryGetValue(recordingServerId, out var recName)
                    ? recName
                    : null,
                RecordingStorageId = JsonElementReader.ReadPathId(item, "recordingStorage"),
                FailoverSetting = JsonElementReader.ReadOptionalString(item, "failoverSetting"),
                RecordingEnabled = JsonElementReader.ReadOptionalBool(item, "recordingEnabled"),
                EdgeStorageEnabled = JsonElementReader.ReadOptionalBool(item, "edgeStorageEnabled"),
                EdgeStoragePlaybackEnabled = JsonElementReader.ReadOptionalBool(item, "edgeStoragePlaybackEnabled"),
                PrebufferEnabled = JsonElementReader.ReadOptionalBool(item, "prebufferEnabled"),
                PrebufferSeconds = JsonElementReader.ReadOptionalInt(item, "prebufferSeconds"),
                PtzEnabled = JsonElementReader.ReadOptionalBool(item, "ptzEnabled"),
                CreatedDate = JsonElementReader.ReadDate(item, "createdDate"),
                LastModified = JsonElementReader.ReadDate(item, "lastModified") ?? hw?.LastModified,
                PasswordLastModified = hw?.PasswordLastModified,
                Labels = labels ?? [],
                CustomProperties = mergedProperties,
                Site = siteName,
                Location = GisPointParser.FromElement(item) ?? hw?.Location
            };
        }).ToList();

        var storages = new List<StorageVolume>();
        foreach (var server in recordingServers)
        {
            var serverId = JsonElementReader.ReadString(server, "id");
            var serverName = serverLookup.GetValueOrDefault(serverId, serverId);
            var serverStorages = await GetChildArrayAsync($"recordingServers/{serverId}/storages", cancellationToken);
            foreach (var storage in serverStorages)
            {
                storages.Add(await MapStorageAsync(storage, serverId, serverName, "Recording", cancellationToken));
                var storageId = JsonElementReader.ReadString(storage, "id");
                var archives = await GetChildArrayAsync($"storages/{storageId}/archiveStorages", cancellationToken);
                foreach (var archive in archives)
                {
                    storages.Add(await MapStorageAsync(archive, serverId, serverName, "Archive", cancellationToken));
                }
            }
        }

        var storageNames = storages.ToDictionary(item => item.Id, item => item.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var camera in cameraModels)
        {
            if (!string.IsNullOrWhiteSpace(camera.RecordingStorageId)
                && storageNames.TryGetValue(camera.RecordingStorageId, out var storageName))
            {
                camera.RecordingStorageName = storageName;
            }
        }

        var servers = recordingServers.Select(item =>
        {
            var id = JsonElementReader.ReadString(item, "id");
            return new RecordingServerInfo
            {
                Id = id,
                Name = serverLookup.GetValueOrDefault(id, id),
                HostName = JsonElementReader.ReadOptionalString(item, "hostName")
                           ?? JsonElementReader.ReadOptionalString(item, "webServerUri"),
                Enabled = JsonElementReader.ReadBool(item, "enabled", true),
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
            RecordingServers = servers,
            SuggestedMapCenter = mapLocations
                .Select(GisPointParser.FromElement)
                .FirstOrDefault(location => location is not null)
        };
    }

    private async Task<StorageVolume> MapStorageAsync(
        JsonElement item,
        string recordingServerId,
        string recordingServerName,
        string kind,
        CancellationToken cancellationToken)
    {
        var id = JsonElementReader.ReadString(item, "id");
        var info = await TryGetStorageInformationAsync(id, cancellationToken);
        return new StorageVolume
        {
            Id = id,
            Name = JsonElementReader.ReadOptionalString(item, "displayName")
                   ?? JsonElementReader.ReadOptionalString(item, "name")
                   ?? id,
            RecordingServerId = recordingServerId,
            RecordingServerName = recordingServerName,
            DiskPath = JsonElementReader.ReadOptionalString(item, "diskPath"),
            Kind = kind,
            MaxSizeMb = JsonElementReader.ReadLong(item, "maxSize"),
            UsedSpaceMb = info is null ? 0 : JsonElementReader.ReadLong(info.Value, "usedSpace"),
            LockedUsedSpaceMb = info is null ? 0 : JsonElementReader.ReadLong(info.Value, "lockedUsedSpace"),
            RetainMinutes = (int)JsonElementReader.ReadLong(item, "retainMinutes"),
            IsDefault = JsonElementReader.ReadBool(item, "isDefault"),
            IsAvailable = info is null || JsonElementReader.ReadBool(info.Value, "isAvailable", true),
            IsMounted = info is null || JsonElementReader.ReadBool(info.Value, "isMounted", true),
            EncryptionMethod = JsonElementReader.ReadOptionalString(item, "encryptionMethod")
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

    private async Task<List<JsonElement>> GetOptionalPagedAsync(
        string resource,
        CancellationToken cancellationToken,
        string? extraQuery = null)
    {
        try
        {
            return await GetPagedAsync(resource, cancellationToken, extraQuery);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Optional collection {Resource} was not available", resource);
            return [];
        }
    }

    private async Task<List<JsonElement>> GetPagedAsync(
        string resource,
        CancellationToken cancellationToken,
        string? extraQuery = null)
    {
        try
        {
            return await GetPagedCoreAsync(resource, cancellationToken, extraQuery);
        }
        catch (HttpRequestException ex) when (!string.IsNullOrWhiteSpace(extraQuery))
        {
            _logger.LogDebug(ex, "Optional children for {Resource} were not available. Retrying without includeChildren.", resource);
            return await GetPagedCoreAsync(resource, cancellationToken, null);
        }
    }

    private async Task<List<JsonElement>> GetPagedCoreAsync(
        string resource,
        CancellationToken cancellationToken,
        string? extraQuery)
    {
        var items = new List<JsonElement>();
        var page = 0;
        while (true)
        {
            var path = $"{resource}?page={page}&size={_options.PageSize}";
            if (!string.IsNullOrWhiteSpace(extraQuery))
            {
                path += $"&{extraQuery.TrimStart('&')}";
            }

            using var document = await SendJsonAsync(path, cancellationToken);
            var pageItems = JsonElementReader.ReadArray(document.RootElement);
            if (pageItems.Count == 0)
            {
                break;
            }

            items.AddRange(pageItems.Select(GisPointParser.Unwrap));
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
            return JsonElementReader.ReadArray(document.RootElement).Select(GisPointParser.Unwrap).ToList();
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
}
