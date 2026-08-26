using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Milestone.Dashboard.Data;
using Milestone.Dashboard.Models;
using Milestone.Dashboard.Options;
using Milestone.Dashboard.Services;

if (args.Length > 0 && string.Equals(args[0], "encrypt-password", StringComparison.OrdinalIgnoreCase))
{
    EncryptPasswordCli(args);
    return;
}

var builder = WebApplication.CreateBuilder(args);

var milestoneOptions = builder.Configuration.GetSection(MilestoneOptions.SectionName).Get<MilestoneOptions>()
                       ?? new MilestoneOptions();
milestoneOptions.GatewayBaseUrl = XprotectAuth.NormalizeGatewayBaseUrl(milestoneOptions.GatewayBaseUrl);
milestoneOptions.Username = milestoneOptions.Username.Trim();
milestoneOptions.ClientId = string.IsNullOrWhiteSpace(milestoneOptions.ClientId)
    ? "GrantValidatorClient"
    : milestoneOptions.ClientId.Trim();
if (!string.IsNullOrWhiteSpace(milestoneOptions.TokenUrl))
{
    milestoneOptions.TokenUrl = milestoneOptions.TokenUrl.Trim();
}
builder.Services.AddSingleton(milestoneOptions);

var keysDirectory = AppSecretProtector.KeysDirectory(builder.Environment.ContentRootPath);
try
{
    Directory.CreateDirectory(keysDirectory);
    builder.Services.AddDataProtection()
        .SetApplicationName("Milestone.Dashboard")
        .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));
}
catch (Exception)
{
    builder.Services.AddDataProtection()
        .SetApplicationName("Milestone.Dashboard");
}

builder.Services.AddSingleton<LocationOverrideStore>();
builder.Services.AddSingleton<SnapshotCache>();
builder.Services.AddSingleton<MonitoredServerCatalog>();
builder.Services.AddSingleton<MonitoredServerMonitor>();
builder.Services.AddSingleton<AppSettingsPasswordWriter>();
builder.Services.AddSingleton<XprotectConnectionTester>();
builder.Services.AddSingleton<DemoVmsClient>();
builder.Services.AddScoped<DashboardService>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50_000_000;
    options.ValueLengthLimit = 50_000_000;
});
builder.Services.AddHttpClient("geocode", client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Milestone.Dashboard/1.0");
    client.Timeout = TimeSpan.FromSeconds(15);
});

var connectionString = builder.Configuration.GetConnectionString("Dashboard");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<DashboardDbContext>(options =>
    {
        if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            && !connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlite(connectionString);
        }
        else
        {
            options.UseSqlServer(connectionString);
        }
    });
}

if (milestoneOptions.UseDemoData)
{
    builder.Services.AddSingleton<IVmsClient>(sp => sp.GetRequiredService<DemoVmsClient>());
}
else
{
    builder.Services.AddHttpClient<IVmsClient, MilestoneApiClient>((sp, client) =>
        {
            client.Timeout = TimeSpan.FromMinutes(3);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            if (milestoneOptions.BypassSslValidation)
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return handler;
        });
}

var app = builder.Build();
UnprotectMilestonePassword(app, milestoneOptions);

if (!string.IsNullOrWhiteSpace(connectionString))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var error = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = UserFacingError(error)
        });
    });
});
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var path = context.File.Name;
        if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }
    }
});

app.MapGet("/api/server-status", async (
    MonitoredServerCatalog catalog,
    MonitoredServerMonitor monitor,
    CancellationToken cancellationToken) =>
{
    var overview = await monitor.ProbeAsync(catalog.List(), cancellationToken);
    return Results.Ok(overview);
});

app.MapPost("/api/server-status", async (
    MonitoredServerRequest request,
    MonitoredServerCatalog catalog,
    MonitoredServerMonitor monitor,
    CancellationToken cancellationToken) =>
{
    try
    {
        await catalog.SaveAsync(ToMonitoredSpec(request), cancellationToken);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    var overview = await monitor.ProbeAsync(catalog.List(), cancellationToken);
    return Results.Ok(overview);
});

app.MapDelete("/api/server-status/{name}", async (
    string name,
    MonitoredServerCatalog catalog,
    MonitoredServerMonitor monitor,
    CancellationToken cancellationToken) =>
{
    var removed = await catalog.RemoveAsync(Uri.UnescapeDataString(name), cancellationToken);
    if (!removed)
    {
        return Results.NotFound(new { error = "That saved server was not found. Configured servers stay until you remove them from appsettings.json." });
    }

    var overview = await monitor.ProbeAsync(catalog.List(), cancellationToken);
    return Results.Ok(overview);
});

app.MapGet("/api/health", async (DashboardService dashboard, CancellationToken cancellationToken) =>
{
    DashboardSnapshot snapshot;
    try
    {
        snapshot = await dashboard.GetSnapshotAsync(cancellationToken);
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "error", error = UserFacingError(ex) }, statusCode: StatusCodes.Status500InternalServerError);
    }

    return Results.Ok(new HealthStatus
    {
        Status = "ok",
        Source = snapshot.Source,
        GeneratedAt = snapshot.GeneratedAt,
        SqlCacheEnabled = dashboard.SqlCacheEnabled
    });
});

app.MapGet("/api/dashboard", async (DashboardService dashboard, CancellationToken cancellationToken) =>
{
    DashboardSnapshot snapshot;
    try
    {
        snapshot = await dashboard.GetSnapshotAsync(cancellationToken);
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = UserFacingError(ex) }, statusCode: StatusCodes.Status500InternalServerError);
    }

    return Results.Ok(new
    {
        snapshot.GeneratedAt,
        snapshot.Source,
        snapshot.SiteName,
        summary = snapshot.Summary,
        cameras = snapshot.Cameras.Select(camera => new
        {
            camera.Id,
            camera.Name,
            camera.ShortName,
            camera.Description,
            camera.Enabled,
            camera.Channel,
            camera.HardwareId,
            camera.HardwareName,
            camera.HardwareAddress,
            camera.HardwareUserName,
            camera.HardwareEnabled,
            camera.HardwareDriver,
            camera.Vendor,
            camera.Model,
            camera.IpAddress,
            camera.DeviceSource,
            camera.Firmware,
            camera.SerialNumber,
            camera.MacAddress,
            camera.RecordingServerId,
            camera.RecordingServerName,
            camera.RecordingStorageId,
            camera.RecordingStorageName,
            camera.FailoverSetting,
            camera.RecordingEnabled,
            camera.EdgeStorageEnabled,
            camera.EdgeStoragePlaybackEnabled,
            camera.PrebufferEnabled,
            camera.PrebufferSeconds,
            camera.PtzEnabled,
            camera.CreatedDate,
            camera.LastModified,
            camera.PasswordLastModified,
            camera.Intelligence,
            camera.Labels,
            camera.CustomProperties,
            camera.Site,
            camera.Address,
            camera.Location,
            camera.LocationIsOverride
        }),
        storages = snapshot.Storages.Select(storage => new
        {
            storage.Id,
            storage.Name,
            storage.RecordingServerId,
            storage.RecordingServerName,
            storage.DiskPath,
            storage.Kind,
            storage.MaxSizeMb,
            storage.UsedSpaceMb,
            storage.LockedUsedSpaceMb,
            storage.RetainMinutes,
            storage.IsDefault,
            storage.IsAvailable,
            storage.IsMounted,
            storage.EncryptionMethod,
            usagePercent = storage.UsagePercent,
            usedLabel = StorageMetrics.FormatSize(storage.UsedSpaceMb),
            maxLabel = StorageMetrics.FormatSize(storage.MaxSizeMb),
            retentionLabel = StorageMetrics.FormatRetention(storage.RetainMinutes)
        }),
        sites = snapshot.Sites,
        recordingServers = snapshot.RecordingServers,
        lifecycle = snapshot.Lifecycle,
        passwordRotation = snapshot.PasswordRotation,
        firmware = snapshot.Firmware,
        securityServers = snapshot.SecurityServers,
        mapCenter = new
        {
            latitude = snapshot.ResolveMapCenter()?.Latitude ?? milestoneOptions.DefaultLatitude,
            longitude = snapshot.ResolveMapCenter()?.Longitude ?? milestoneOptions.DefaultLongitude,
            zoom = milestoneOptions.DefaultZoom
        }
    });
});

app.MapGet("/api/settings/password", (AppSettingsPasswordWriter writer, MilestoneOptions options) =>
    Results.Ok(new
    {
        encrypted = writer.PasswordIsEncrypted,
        canWrite = writer.CanWrite,
        useDemoData = options.UseDemoData
    }));

app.MapPost("/api/settings/password", (
    EncryptPasswordRequest request,
    AppSettingsPasswordWriter writer,
    MilestoneOptions options,
    IDataProtectionProvider dataProtection) =>
{
    if (string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Type the current XProtect Basic user password." });
    }

    var protector = dataProtection.CreateProtector(AppSecretProtector.Purpose);
    var encrypted = AppSecretProtector.Protect(protector, request.Password);
    var saved = false;
    string? saveError = null;
    if (request.Save)
    {
        try
        {
            writer.SaveEncrypted(encrypted);
            options.Password = request.Password;
            saved = true;
        }
        catch (Exception ex)
        {
            saveError = "Encrypted, but appsettings.json could not be updated. Paste the ENC: value into Milestone:Password. " + ex.Message;
        }
    }

    return Results.Ok(new { encrypted, saved, saveError });
});

app.MapGet("/api/settings/connection", (AppSettingsPasswordWriter writer, MilestoneOptions options) =>
    Results.Ok(new
    {
        gatewayBaseUrl = options.GatewayBaseUrl,
        username = options.Username,
        passwordSet = !string.IsNullOrWhiteSpace(options.Password),
        useDemoData = options.UseDemoData,
        bypassSslValidation = options.BypassSslValidation,
        canWrite = writer.CanWrite,
        settingsPath = writer.FilePath
    }));

app.MapPost("/api/settings/connection/test", async (
    XprotectConnectionRequest request,
    MilestoneOptions options,
    XprotectConnectionTester tester,
    CancellationToken cancellationToken) =>
{
    var gateway = string.IsNullOrWhiteSpace(request.GatewayBaseUrl) ? options.GatewayBaseUrl : request.GatewayBaseUrl;
    var username = string.IsNullOrWhiteSpace(request.Username) ? options.Username : request.Username;
    var password = string.IsNullOrWhiteSpace(request.Password) ? options.Password : request.Password;
    if (request.UseDemoData)
    {
        return Results.Ok(new { ok = true, message = "Demo data is on. Turn it off to test a live XProtect login." });
    }

    var error = await tester.TestAsync(
        gateway,
        username,
        password,
        options.ClientId,
        request.BypassSslValidation,
        cancellationToken);
    return error is null
        ? Results.Ok(new { ok = true, message = "XProtect login succeeded." })
        : Results.Ok(new { ok = false, error });
});

app.MapPost("/api/settings/connection", async (
    XprotectConnectionRequest request,
    MilestoneOptions options,
    AppSettingsPasswordWriter writer,
    XprotectConnectionTester tester,
    IDataProtectionProvider dataProtection,
    CancellationToken cancellationToken) =>
{
    var gateway = (request.GatewayBaseUrl ?? string.Empty).Trim();
    var username = (request.Username ?? string.Empty).Trim();
    var password = string.IsNullOrWhiteSpace(request.Password) ? options.Password : request.Password.Trim();
    if (!request.UseDemoData
        && (string.IsNullOrWhiteSpace(gateway) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)))
    {
        return Results.BadRequest(new { error = "Gateway URL, username, and password are required when demo data is off." });
    }

    var protector = dataProtection.CreateProtector(AppSecretProtector.Purpose);
    string? encrypted = null;
    if (!string.IsNullOrWhiteSpace(request.Password))
    {
        encrypted = AppSecretProtector.Protect(protector, request.Password.Trim());
    }
    else if (writer.PasswordIsEncrypted)
    {
        encrypted = writer.ReadPassword();
    }
    else if (!string.IsNullOrWhiteSpace(options.Password))
    {
        encrypted = AppSecretProtector.Protect(protector, options.Password);
    }

    var recycleRequired = options.UseDemoData != request.UseDemoData
                          || options.BypassSslValidation != request.BypassSslValidation;
    try
    {
        writer.SaveConnection(gateway, username, encrypted, request.UseDemoData, request.BypassSslValidation);
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            saved = false,
            saveError = "Could not update appsettings.json. " + ex.Message,
            loginOk = false
        });
    }

    options.GatewayBaseUrl = XprotectAuth.NormalizeGatewayBaseUrl(gateway);
    options.Username = username;
    if (!string.IsNullOrWhiteSpace(request.Password))
    {
        options.Password = request.Password.Trim();
    }

    options.UseDemoData = request.UseDemoData;
    options.BypassSslValidation = request.BypassSslValidation;

    string? loginError = null;
    if (!request.UseDemoData)
    {
        loginError = await tester.TestAsync(
            options.GatewayBaseUrl,
            options.Username,
            options.Password,
            options.ClientId,
            options.BypassSslValidation,
            cancellationToken);
    }

    return Results.Ok(new
    {
        saved = true,
        saveError = (string?)null,
        loginOk = loginError is null,
        loginError,
        recycleRequired
    });
});

app.MapGet("/api/geocode", async (string q, IHttpClientFactory httpFactory, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(q))
    {
        return Results.BadRequest(new { error = "q is required." });
    }

    var client = httpFactory.CreateClient("geocode");
    using var response = await client.GetAsync($"search?format=json&limit=5&q={Uri.EscapeDataString(q)}", cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return Results.StatusCode((int)response.StatusCode);
    }

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    var results = document.RootElement.EnumerateArray().Select(item => new
    {
        label = item.GetProperty("display_name").GetString(),
        latitude = double.Parse(item.GetProperty("lat").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
        longitude = double.Parse(item.GetProperty("lon").GetString()!, System.Globalization.CultureInfo.InvariantCulture)
    }).ToList();

    return Results.Ok(results);
});

app.MapGet("/api/locations/template", async (DashboardService dashboard, CancellationToken cancellationToken) =>
{
    var snapshot = await dashboard.GetSnapshotAsync(cancellationToken);
    var lines = new List<string> { "cameraId,name,latitude,longitude,site,address,Site_Name" };
    lines.AddRange(snapshot.Cameras.Select(camera =>
    {
        var siteCode = camera.CustomProperties.TryGetValue("SiteCode", out var code) ? code : "";
        var address = camera.Address
                      ?? (camera.CustomProperties.TryGetValue("Address", out var value) ? value : "");
        return string.Join(',',
            camera.Id,
            Csv(camera.Name),
            camera.Location?.Latitude,
            camera.Location?.Longitude,
            Csv(siteCode),
            Csv(address),
            Csv(camera.Site));
    }));
    return Results.Text(string.Join('\n', lines) + "\n", "text/csv");
});

app.MapPost("/api/locations/import", async (List<LocationImportItem> items, bool? replace, DashboardService dashboard, CancellationToken cancellationToken) =>
{
    return await ImportLocationsAsync(items, dashboard, replace ?? true, cancellationToken);
});

app.MapPost("/api/locations/import-csv", async (HttpRequest request, DashboardService dashboard, CancellationToken cancellationToken) =>
{
    try
    {
        string text;
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "Choose a CSV file to import." });
            }

            using var reader = new StreamReader(file.OpenReadStream());
            text = await reader.ReadToEndAsync(cancellationToken);
        }
        else
        {
            using var reader = new StreamReader(request.Body);
            text = await reader.ReadToEndAsync(cancellationToken);
        }

        var items = CsvLocationParser.Parse(text);
        var replace = true;
        if (request.Query.TryGetValue("replace", out var replaceValue)
            && bool.TryParse(replaceValue, out var parsed))
        {
            replace = parsed;
        }

        return await ImportLocationsAsync(items, dashboard, replace, cancellationToken);
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = $"CSV import failed: {ex.Message}" }, statusCode: 500);
    }
});

static async Task<IResult> ImportLocationsAsync(
    List<LocationImportItem> items,
    DashboardService dashboard,
    bool replace,
    CancellationToken cancellationToken)
{
    if (items.Count == 0)
    {
        return Results.BadRequest(new { error = "No location rows with latitude and longitude were found. Leave those two columns filled; empty rows are skipped." });
    }

    var result = await dashboard.ImportOverridesAsync(items, cancellationToken, replace);
    return Results.Ok(result);
}

app.MapPost("/api/locations", async (LocationOverrideRequest request, DashboardService dashboard, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.CameraId))
    {
        return Results.BadRequest(new { error = "cameraId is required." });
    }

    if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
    {
        return Results.BadRequest(new { error = "Latitude or longitude is out of range." });
    }

    await dashboard.SaveOverrideAsync(request, cancellationToken);
    return Results.Ok(request);
});

app.MapFallback(async (HttpContext context) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Not found." });
        return;
    }

    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "index.html"));
});

app.Run();

static string UserFacingError(Exception? error)
{
    var current = error;
    while (current is not null)
    {
        if (current is HttpRequestException)
        {
            return "Could not reach XProtect. Check Milestone:GatewayBaseUrl, Username, Password, and whether UseDemoData should be true. " + current.Message;
        }

        if (current is InvalidOperationException or CryptographicException)
        {
            return current.Message;
        }

        current = current.InnerException;
    }

    return error?.Message
           ?? "The dashboard could not load. Check the IIS site log and appsettings.json.";
}

static void EncryptPasswordCli(string[] args)
{
    var root = Directory.GetCurrentDirectory();
    var keysDirectory = AppSecretProtector.KeysDirectory(root);
    Directory.CreateDirectory(keysDirectory);

    var services = new ServiceCollection();
    services.AddDataProtection()
        .SetApplicationName("Milestone.Dashboard")
        .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));
#pragma warning disable ASP0000
    using var provider = services.BuildServiceProvider();
#pragma warning restore ASP0000
    var protector = provider.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector(AppSecretProtector.Purpose);

    var plain = args.Length > 1
        ? args[1]
        : Console.In.ReadToEnd().TrimEnd('\r', '\n');
    if (string.IsNullOrWhiteSpace(plain))
    {
        Console.Error.WriteLine("Password was empty.");
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine(AppSecretProtector.Protect(protector, plain));
}

static void UnprotectMilestonePassword(WebApplication app, MilestoneOptions options)
{
    if (string.IsNullOrWhiteSpace(options.Password) || !AppSecretProtector.IsProtected(options.Password))
    {
        return;
    }

    try
    {
        var protector = app.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(AppSecretProtector.Purpose);
        options.Password = AppSecretProtector.Unprotect(protector, options.Password);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex,
            "Could not decrypt Milestone:Password. Encrypt the password on this same web server with scripts/encrypt-password.ps1.");
        throw new InvalidOperationException(
            "The encrypted password in appsettings.json could not be decrypted. Run scripts/encrypt-password.ps1 on this web server, then recycle the app pool.",
            ex);
    }
}

static string Csv(string? value)
{
    if (string.IsNullOrEmpty(value))
    {
        return "";
    }

    return $"\"{value.Replace("\"", "\"\"")}\"";
}

static MonitoredServerSpec ToMonitoredSpec(MonitoredServerRequest request) =>
    new()
    {
        Name = request.Name?.Trim() ?? string.Empty,
        HostName = string.IsNullOrWhiteSpace(request.HostName) ? null : request.HostName.Trim(),
        IpAddress = string.IsNullOrWhiteSpace(request.IpAddress) ? null : request.IpAddress.Trim(),
        Role = string.IsNullOrWhiteSpace(request.Role) ? "Server" : request.Role.Trim(),
        Source = "saved"
    };

public partial class Program;
