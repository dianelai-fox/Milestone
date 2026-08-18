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
app.UseStaticFiles();

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
        recordingServers = snapshot.RecordingServers
    });
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
