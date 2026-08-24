using Milestone.Dashboard.Models;

namespace Milestone.Dashboard.Services;

public sealed class DemoVmsClient : IVmsClient
{
    public string SourceName => "demo";

    public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var rec1 = "a18c2e60-1c3b-4d0a-9f11-0f5f1d8c1001";
        var rec2 = "a18c2e60-1c3b-4d0a-9f11-0f5f1d8c1002";

        var cameras = new List<CameraInfo>
        {
            Cam("c01", "Lobby Main", "Building A", rec1, "REC-01 Downtown", -118.2569, 34.0522, true, "10.10.1.11", "AXIS P3245-LVE", "11.11.65", ["Building A / Lobby"], "AXIS P32 Series"),
            Cam("c02", "Lobby West", "Building A", rec1, "REC-01 Downtown", -118.2574, 34.0525, true, "10.10.1.12", "AXIS P3245-LVE", "11.11.65", ["Building A / Lobby"], "AXIS P32 Series"),
            Cam("c03", "Reception Desk", "Building A", rec1, "REC-01 Downtown", -118.2566, 34.0519, true, "10.10.1.13", "AXIS M3086-V", "11.8.61", ["Building A / Reception"], "AXIS M30 Series"),
            Cam("c04", "Elevator Bank A", "Building A", rec1, "REC-01 Downtown", -118.2563, 34.0526, true, "10.10.1.14", "AXIS P3225-LVE Mk II", "8.40.1", ["Building A / Elevators"], "AXIS P32 Series"),
            Cam("c05", "Parking P1 Entry", "Parking", rec1, "REC-01 Downtown", -118.2588, 34.0514, true, "10.10.1.21", "AXIS Q1700-LE", "10.12.104", ["Parking / P1"], "AXIS Q17 Series"),
            Cam("c06", "Parking P1 Exit", "Parking", rec1, "REC-01 Downtown", -118.2591, 34.0511, true, "10.10.1.22", "AXIS Q1700-LE", "10.12.104", ["Parking / P1"], "AXIS Q17 Series"),
            Cam("c07", "Loading Dock", "Building A", rec1, "REC-01 Downtown", -118.2558, 34.0510, true, "10.10.1.31", "AXIS P3225-LV", "6.50.5", ["Building A / Dock"], "AXIS P32 Series"),
            Cam("c08", "Perimeter North", "Campus", rec1, "REC-01 Downtown", -118.2550, 34.0538, true, "10.10.1.41", "AXIS Q6135-LE", "11.11.73", ["Campus / Perimeter"], "AXIS Q61 Series", true),
            Cam("c09", "Perimeter South", "Campus", rec1, "REC-01 Downtown", -118.2582, 34.0502, true, "10.10.1.42", "AXIS Q6135-LE", "11.11.73", ["Campus / Perimeter"], "AXIS Q61 Series", true),
            Cam("c10", "Server Room", "Building A", rec1, "REC-01 Downtown", null, null, true, "10.10.1.51", "AXIS M2036-LE", "11.9.52", ["Building A / Restricted"], "AXIS M20 Series"),
            Cam("c11", "Studio Floor 2", "Building B", rec2, "REC-02 Studio", -118.2508, 34.0548, true, "10.20.1.11", "AXIS P3265-LVE", "11.11.65", ["Building B / Studio"], "AXIS P32 Series"),
            Cam("c12", "Studio Control", "Building B", rec2, "REC-02 Studio", -118.2504, 34.0551, true, "10.20.1.12", "AXIS P3265-V", "11.11.65", ["Building B / Control"], "AXIS P32 Series"),
            Cam("c13", "Sound Stage A", "Building B", rec2, "REC-02 Studio", -118.2498, 34.0544, true, "10.20.1.13", "Hanwha XNV-8083R", "2.41.03", ["Building B / Stages"], "Hanwha Wisenet"),
            Cam("c14", "Sound Stage B", "Building B", rec2, "REC-02 Studio", -118.2492, 34.0540, true, "10.20.1.14", "Hanwha XNV-8083R", "2.41.03", ["Building B / Stages"], "Hanwha Wisenet"),
            Cam("c15", "Gatehouse", "Campus", rec2, "REC-02 Studio", -118.2526, 34.0562, true, "10.20.1.21", "AXIS P1465-LE", "11.11.61", ["Campus / Gate"], "AXIS P14 Series"),
            Cam("c16", "Employee Lot", "Parking", rec2, "REC-02 Studio", -118.2534, 34.0536, true, "10.20.1.22", "AXIS P1465-LE", "11.11.61", ["Parking / Employee"], "AXIS P14 Series"),
            Cam("c17", "Warehouse Aisle 3", "Warehouse", rec2, "REC-02 Studio", -118.2484, 34.0528, true, "10.20.1.31", "Hikvision DS-2CD2686G2", "V5.7.15", ["Warehouse / Aisles"], "Hikvision"),
            Cam("c18", "Warehouse Dock 2", "Warehouse", rec2, "REC-02 Studio", -118.2478, 34.0522, false, "10.20.1.32", "Hikvision DS-2CD2686G2", "V5.7.11", ["Warehouse / Dock"], "Hikvision"),
            Cam("c19", "Roof PTZ", "Building B", rec2, "REC-02 Studio", -118.2501, 34.0556, true, "10.20.1.41", "AXIS Q6318-LE", "11.11.73", ["Building B / Roof"], "AXIS Q63 Series", true),
            Cam("c20", "Archive Vault", "Building B", rec2, "REC-02 Studio", null, null, true, "10.20.1.51", "AXIS M3086-V", "11.8.61", ["Building B / Restricted"], "AXIS M30 Series")
        };

        var storages = new List<StorageVolume>
        {
            new()
            {
                Id = "s01",
                Name = "Default Recording",
                RecordingServerId = rec1,
                RecordingServerName = "REC-01 Downtown",
                DiskPath = @"E:\MediaDatabase",
                Kind = "Recording",
                MaxSizeMb = 8_388_608,
                UsedSpaceMb = 5_242_880,
                LockedUsedSpaceMb = 81_920,
                RetainMinutes = 10_080,
                IsDefault = true,
                EncryptionMethod = "Light"
            },
            new()
            {
                Id = "s02",
                Name = "Archive NAS-01",
                RecordingServerId = rec1,
                RecordingServerName = "REC-01 Downtown",
                DiskPath = @"\\nas-01\xprotect\archive",
                Kind = "Archive",
                MaxSizeMb = 20_971_520,
                UsedSpaceMb = 12_582_912,
                LockedUsedSpaceMb = 204_800,
                RetainMinutes = 129_600,
                EncryptionMethod = "Strong"
            },
            new()
            {
                Id = "s03",
                Name = "Default Recording",
                RecordingServerId = rec2,
                RecordingServerName = "REC-02 Studio",
                DiskPath = @"D:\MediaDatabase",
                Kind = "Recording",
                MaxSizeMb = 4_194_304,
                UsedSpaceMb = 3_670_016,
                LockedUsedSpaceMb = 12_288,
                RetainMinutes = 7_200,
                IsDefault = true,
                EncryptionMethod = "None"
            },
            new()
            {
                Id = "s04",
                Name = "Archive NAS-02",
                RecordingServerId = rec2,
                RecordingServerName = "REC-02 Studio",
                DiskPath = @"\\nas-02\xprotect\archive",
                Kind = "Archive",
                MaxSizeMb = 16_777_216,
                UsedSpaceMb = 6_291_456,
                LockedUsedSpaceMb = 40_960,
                RetainMinutes = 86_400,
                EncryptionMethod = "Light"
            }
        };

        var servers = new List<RecordingServerInfo>
        {
            new()
            {
                Id = rec1,
                Name = "REC-01 Downtown",
                HostName = "rec01.campus.local",
                CameraCount = cameras.Count(c => c.RecordingServerId == rec1),
                UsedSpaceMb = storages.Where(s => s.RecordingServerId == rec1).Sum(s => s.UsedSpaceMb),
                MaxSizeMb = storages.Where(s => s.RecordingServerId == rec1).Sum(s => s.MaxSizeMb)
            },
            new()
            {
                Id = rec2,
                Name = "REC-02 Studio",
                HostName = "rec02.campus.local",
                CameraCount = cameras.Count(c => c.RecordingServerId == rec2),
                UsedSpaceMb = storages.Where(s => s.RecordingServerId == rec2).Sum(s => s.UsedSpaceMb),
                MaxSizeMb = storages.Where(s => s.RecordingServerId == rec2).Sum(s => s.MaxSizeMb)
            }
        };

        return Task.FromResult(new DashboardSnapshot
        {
            Source = SourceName,
            SiteName = "Demo Campus",
            Cameras = cameras,
            Storages = storages,
            RecordingServers = servers
        });
    }

    private static CameraInfo Cam(
        string id,
        string name,
        string site,
        string recId,
        string recName,
        double? lon,
        double? lat,
        bool enabled,
        string address,
        string model,
        string firmware,
        IReadOnlyList<string> labels,
        string driver,
        bool ptz = false)
    {
        var serial = $"ACC{id[1..].ToUpperInvariant()}{address.Replace(".", "")[^6..]}";
        return new CameraInfo
        {
            Id = id,
            Name = name,
            ShortName = id.ToUpperInvariant(),
            Description = $"{site} camera",
            Enabled = enabled,
            Channel = 0,
            HardwareId = $"h-{id}",
            HardwareName = name,
            HardwareAddress = $"http://{address}/",
            HardwareUserName = "root",
            HardwareEnabled = enabled,
            HardwareDriver = driver,
            Vendor = CameraIdentity.Vendor(model, driver),
            Model = model,
            IpAddress = address,
            DeviceSource = "Demo",
            Firmware = firmware,
            SerialNumber = serial,
            MacAddress = $"00:40:8C:1A:{id[1]}{id[2]}:{id[2]}{id[1]}",
            RecordingServerId = recId,
            RecordingServerName = recName,
            RecordingStorageName = "Default Recording",
            FailoverSetting = "FullSupport",
            RecordingEnabled = enabled,
            EdgeStorageEnabled = false,
            EdgeStoragePlaybackEnabled = false,
            PrebufferEnabled = true,
            PrebufferSeconds = 5,
            PtzEnabled = ptz,
            CreatedDate = new DateTimeOffset(2024, 3, 12, 16, 0, 0, TimeSpan.Zero),
            LastModified = new DateTimeOffset(2026, 8, 1, 18, 30, 0, TimeSpan.Zero),
            PasswordLastModified = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero),
            Labels = labels,
            CustomProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Owner"] = "Physical Security",
                ["Criticality"] = site is "Building B" or "Campus" ? "High" : "Standard"
            },
            Site = site,
            Location = lon is null || lat is null ? null : new CameraLocation(lon.Value, lat.Value)
        };
    }
}
