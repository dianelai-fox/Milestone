using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Milestone.Dashboard.Data;
using Milestone.Dashboard.Models;
using Milestone.Dashboard.Options;
using Milestone.Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);

var milestoneOptions = builder.Configuration.GetSection(MilestoneOptions.SectionName).Get<MilestoneOptions>()
                       ?? new MilestoneOptions();
builder.Services.AddSingleton(milestoneOptions);

builder.Services.AddSingleton<LocationOverrideStore>();
builder.Services.AddSingleton<SnapshotCache>();
builder.Services.AddSingleton<DemoVmsClient>();
builder.Services.AddScoped<DashboardService>();
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
            client.Timeout = TimeSpan.FromSeconds(60);
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

if (!string.IsNullOrWhiteSpace(connectionString))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
    await db.Database.EnsureCreatedAsync();
}

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
    var snapshot = await dashboard.GetSnapshotAsync(cancellationToken);
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
    var snapshot = await dashboard.GetSnapshotAsync(cancellationToken);
    return Results.Ok(new
    {
        snapshot.GeneratedAt,
        snapshot.Source,
        snapshot.SiteName,
        summary = snapshot.Summary,
        cameras = snapshot.Cameras,
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
    var lines = new List<string> { "cameraId,name,latitude,longitude,site" };
    lines.AddRange(snapshot.Cameras.Select(camera =>
        $"{camera.Id},\"{camera.Name.Replace("\"", "\"\"")}\",{camera.Location?.Latitude},{camera.Location?.Longitude},{camera.Site}"));
    return Results.Text(string.Join('\n', lines) + "\n", "text/csv");
});

app.MapPost("/api/locations/import", async (List<LocationImportItem> items, DashboardService dashboard, CancellationToken cancellationToken) =>
{
    if (items.Count == 0)
    {
        return Results.BadRequest(new { error = "No location rows were provided." });
    }

    var invalid = items.Where(item => item.Latitude is < -90 or > 90 || item.Longitude is < -180 or > 180).ToList();
    if (invalid.Count > 0)
    {
        return Results.BadRequest(new { error = "One or more rows have coordinates that are out of range." });
    }

    var result = await dashboard.ImportOverridesAsync(items, cancellationToken);
    return Results.Ok(result);
});

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

public partial class Program;
