# XProtect Camera and Storage Dashboard

A web dashboard for Milestone XProtect that shows camera locations on a map and recording/archive storage usage. Host it on an existing IIS web server. It can read live data from the XProtect API Gateway, or run with built-in demo data until that connection is ready.

## What you get

- Map of cameras that have GIS coordinates in XProtect (`gisPoint`)
- Local location overrides for cameras that are not mapped yet
- Storage usage for recording and archive volumes
- Camera inventory with firmware, labels/groups, model, serial, custom properties, recording server, and hardware details
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

1. Confirm the API Gateway answers:

   `GET https://<management-or-gateway-host>/api/.well-known/uris`

2. Create an XProtect **Basic user** with permission to read cameras, recording servers, and storage.

3. On the web server, set these values (IIS Configuration Editor, environment variables, or `appsettings.Production.json` that is **not** committed):

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
2. Publish the site:

   ```bash
   dotnet publish src/Milestone.Dashboard -c Release -o C:\inetpub\xprotect-dashboard
   ```

3. In IIS, create a site or application pointed at that folder. The included `web.config` uses the ASP.NET Core Module.
4. Grant the app-pool identity read access to the publish folder and write access to `App_Data` and `logs`.
5. If you set `ConnectionStrings:Dashboard`, the app creates a small `DashboardSnapshots` table and stores the last successful API pull there. Use a dedicated database, not the XProtect `Surveillance` database.

Windows authentication to XProtect is not used. Use an XProtect Basic user, and prefer HTTPS between the web server and the API Gateway.

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
