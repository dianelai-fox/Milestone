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
        mapCenter = new
        {
            latitude = snapshot.ResolveMapCenter()?.Latitude ?? milestoneOptions.DefaultLatitude,
            longitude = snapshot.ResolveMapCenter()?.Longitude ?? milestoneOptions.DefaultLongitude,
            zoom = milestoneOptions.DefaultZoom
        }
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

app.MapPost("/api/locations/import", async (List<LocationImportItem> items, DashboardService dashboard, CancellationToken cancellationToken) =>
{
    return await ImportLocationsAsync(items, dashboard, cancellationToken);
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
        return await ImportLocationsAsync(items, dashboard, cancellationToken);
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = $"CSV import failed: {ex.Message}" }, statusCode: 500);
    }
});

static async Task<IResult> ImportLocationsAsync(
    List<LocationImportItem> items,
    DashboardService dashboard,
    CancellationToken cancellationToken)
{
    if (items.Count == 0)
    {
        return Results.BadRequest(new { error = "No location rows with latitude and longitude were found. Leave those two columns filled; empty rows are skipped." });
    }

    var result = await dashboard.ImportOverridesAsync(items, cancellationToken);
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

app.MapFallbackToFile("index.html");

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

public partial class Program;
