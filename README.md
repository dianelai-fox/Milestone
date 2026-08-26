# XProtect Camera and Storage Dashboard

A web dashboard for Milestone XProtect that shows camera locations on a map and recording/archive storage usage. Host it on an existing IIS web server. It can read live data from the XProtect API Gateway, or run with built-in demo data until that connection is ready.

## What you get

- Map of cameras that have GIS coordinates in XProtect (`gisPoint`)
- Local location overrides for cameras that are not mapped yet
- Storage usage for recording and archive volumes
- Camera inventory in a device table: status, labels, vendor, model, IP, firmware, lifecycle/EOS, NDAA, password age, and notes
- Security Servers page for XProtect recording-server online status and storage
- Server Status page for any hosts you configure (name, hostname, IP) with live online/offline checks
- Optional SQL Server cache so the last snapshot still shows if the management server is briefly unavailable

The dashboard does **not** query the XProtect `Surveillance` database. Milestone stores configuration there, but that schema is unsupported for integrations. Live data comes from the [MIP VMS RESTful Configuration API](https://doc.developer.milestonesys.com/mipvmsapi/api/config-rest/v1/) through the API Gateway.

## Run it locally

```bash
dotnet run --project src/Milestone.Dashboard
```

Open http://localhost:5080. Demo mode is on by default, so the page loads without an XProtect connection.

```bash
dotnet test
```

## Connect to XProtect

If the badge says **Unavailable** and the page shows `XProtect login failed (HTTP 400)`, the site is already in live mode but the gateway rejected the login. Open **Connect to XProtect** in the sidebar (the page also opens itself after a login error). Use an XProtect **Basic user**, not a Windows/`DOMAIN\user` account. Set the gateway URL to the management server or API Gateway root, with no `/API` suffix. Click **Test login**, then **Save connection**. Recycle the app pool only if you changed **Use demo data**.

1. Confirm the API Gateway answers:

   `GET https://<management-or-gateway-host>/api/.well-known/uris`

2. Create an XProtect **Basic user** with permission to read cameras, recording servers, and storage.

3. On the web server, set these values from **Connect to XProtect**, IIS Configuration Editor, environment variables, or `appsettings.Production.json` that is **not** committed:

| Setting | Example | Purpose |
| --- | --- | --- |
| `Milestone__UseDemoData` | `false` | Switch from sample data to the live API |
| `Milestone__GatewayBaseUrl` | `https://xprotect.company.local` | API Gateway / management server |
| `Milestone__Username` | `dashboard.reader` | Basic user |
| `Milestone__Password` | *(secret)* | Basic user password |
| `Milestone__ClientId` | `GrantValidatorClient` | Built-in IDP client |
| `Milestone__BypassSslValidation` | `false` | Only `true` for lab certs |
| `ConnectionStrings__Dashboard` | `Server=sql01;Database=XProtectDashboard;Trusted_Connection=True;TrustServerCertificate=True` | Optional snapshot cache |

The app authenticates the same way Milestone documents for the API Gateway:

`POST /API/IDP/connect/token` with `grant_type=password` and `client_id=GrantValidatorClient`

Then it reads `/api/rest/v1/cameras` (including custom properties), `/hardware`, `/hardwareDriverSettings` (firmware, serial, MAC when the driver reports them), `/cameraGroups` (labels), `/hardwareDrivers`, `/recordingServers`, `/storages`, and `/storageInformation`.

If a camera has no `gisPoint` in Management Client, you can still place it:

```http
POST /api/locations
Content-Type: application/json

{
  "cameraId": "<camera-guid>",
  "latitude": 34.0522,
  "longitude": -118.2437,
  "site": "Studio lot"
}
```

Overrides are stored in `App_Data/location-overrides.json` on the web server.

## Host on IIS

1. Install the [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0) on the web server.
2. Publish the site. Prefer `scripts/publish-iis.ps1` so the live `appsettings.json` is kept. A plain `dotnet publish` no longer overwrites that file.

   ```bash
   scripts/publish-iis.ps1
   ```

   If you publish with `dotnet publish` yourself, do not replace `C:\inetpub\xprotect-dashboard\appsettings.json`. That file is what switches **Demo data** to your live recording servers and cameras (`Milestone:UseDemoData` = `false`).

3. In IIS, create a site or application pointed at that folder. The included `web.config` uses the ASP.NET Core Module.
4. Grant the app-pool identity read access to the publish folder and write access to `App_Data` and `logs`.
5. If you set `ConnectionStrings:Dashboard`, the app creates a small `DashboardSnapshots` table and stores the last successful API pull there. Use a dedicated database, not the XProtect `Surveillance` database.

Windows authentication to XProtect is not used. Use an XProtect Basic user, and prefer HTTPS between the web server and the API Gateway.

## Encrypt the password in appsettings.json

The dashboard can store `Milestone:Password` as an encrypted value (`ENC:...`) instead of plain text. Decrypt happens only when the site starts.

Do this **on the IIS web server** after you publish. The encrypted value only works on that same server.

The easiest way is the **Encrypt password** page in the sidebar. Type the XProtect Basic user password, keep **Save into appsettings.json** checked, then recycle the app pool and press Ctrl+F5.

The PowerShell script still works if you prefer it:

1. Publish the new site (stop IIS first, then `scripts/publish-iis.ps1` or your usual publish).
2. Open PowerShell **as Administrator** on the web server.
3. Run:

   ```powershell
   cd C:\Users\dianela\Milestone
   powershell -ExecutionPolicy Bypass -File .\scripts\encrypt-password.ps1
   ```

   If the site folder is not `C:\inetpub\xprotect-dashboard`, pass it:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\encrypt-password.ps1 -SitePath "C:\inetpub\xprotect-dashboard"
   ```

4. When asked, type the **current** XProtect password (the same one already in `appsettings.json`).
5. The script replaces `Password` with a value that starts with `ENC:` and saves a backup as `appsettings.json.bak`.
6. Recycle the `XProtectDashboard` app pool (or `iisreset`).
7. Open the dashboard and press **Ctrl+F5**.

Plain text passwords still work until you run the script. After encryption, `appsettings.json` looks like:

```json
"Password": "ENC:CfDJ8...."
```

That `ENC:` string is not the real password. Only this website, on this server, can turn it back into the password at startup.

## Project layout

```
src/Milestone.Dashboard/     ASP.NET Core 8 site (IIS-ready)
tests/Milestone.Dashboard.Tests/
```

## Put cameras on the map

Most XProtect systems do not store GIS coordinates on cameras, so the dashboard shows **Not mapped**. You can place them in the dashboard without changing XProtect:

1. Search an address or site name above the map, then click **Find**.
2. Choose a camera in **Select camera to place**, or click **Place on map** in the table.
3. Click the map. The pin is saved in `App_Data/location-overrides.json` on the web server.

For many cameras, click **Download CSV**, fill `latitude` and `longitude`, then **Import CSV**.

If you later set a camera GIS point in Management Client (`POINT (LONGITUDE LATITUDE)`), the dashboard uses that value unless a local override exists.

To change the starting map view when nothing is mapped yet, set `Milestone:DefaultLatitude`, `DefaultLongitude`, and `DefaultZoom` in `appsettings.json`.

## Monitor servers (online / offline)

Open **Server Status** in the sidebar. That page is separate from **Security Servers** (XProtect recording servers). Add the hosts you already track by name, hostname, and IP:

```json
"MonitoredServers": [
  {
    "Name": "FOXUSWDMSIA297",
    "HostName": "LENELNEWAPP.INT.APPS.FOX",
    "IpAddress": "10.0.0.10",
    "Role": "Lenel application"
  }
]
```

Put that under `Milestone` in `appsettings.json` on the IIS server (or copy `monitored-servers.example.json` to `App_Data/monitored-servers.json`). The site probes IP/hostname from the web server (common ports, then ping) and shows Online or Offline. Demo mode shows sample rows until you add real hosts.

