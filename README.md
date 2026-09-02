# XProtect Camera and Storage Dashboard

A web dashboard for Milestone XProtect that shows camera locations on a map and recording/archive storage usage. Host it on an existing IIS web server. It can read live data from the XProtect API Gateway, or run with built-in demo data until that connection is ready.

## What you get

- Map of cameras that have GIS coordinates in XProtect (`gisPoint`)
- Local location overrides for cameras that are not mapped yet
- Storage usage for recording and archive volumes
- Camera inventory in a device table: status, labels, vendor, model, IP, firmware, lifecycle/EOS, NDAA, password age, and notes
- Optional SQL Server cache so the last snapshot still shows if the management server is briefly unavailable
- Server Status page for non-XProtect hosts, starting with the MasterMind deck (Online / Offline / Need attention)

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

## Develop on FOX2208553, host on FOXAWSMSAP076

Keep the source on your PC. Publish the site to the new web server.

| Role | Computer | Folder |
| --- | --- | --- |
| Edit / build | **FOX2208553** | `C:\Users\dianela\Milestone` |
| IIS site | **FOXAWSMSAP076** | `C:\inetpub\xprotect-dashboard` |

WinRM and `\\FOXAWSMSAP076\C$` are blocked from FOX2208553 (`Access is denied` / `network name cannot be found`). That is expected. Copy a zip through RDP instead.

**On FOX2208553:**

Administrator PowerShell starts in `C:\Windows\system32`. Use the full path (do not use `.\scripts\...` from system32):

```powershell
cd C:\Users\dianela\Milestone
powershell -ExecutionPolicy Bypass -File C:\Users\dianela\Milestone\scripts\pack-for-iis.ps1 -IncludeSite -IncludeLiveSettings
```

Or double-click `C:\Users\dianela\Milestone\scripts\pack-for-iis.cmd`.

That writes `Desktop\xprotect-iis-package.zip`. Copy the zip onto FOXAWSMSAP076 through the RDP window (copy on the PC, paste on the server desktop).

**On FOXAWSMSAP076**, open **Windows PowerShell as Administrator** (not PowerShell 7). IIS + the [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0) must already be installed:

```powershell
Expand-Archive -Path C:\Users\sa-dlai\Desktop\xprotect-iis-package.zip -DestinationPath C:\Users\sa-dlai\Desktop\xprotect-iis-package -Force
C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -ExecutionPolicy Bypass -File C:\Users\sa-dlai\Desktop\xprotect-iis-package\scripts\expand-iis-package.ps1 -ZipPath C:\Users\sa-dlai\Desktop\xprotect-iis-package.zip
C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -ExecutionPolicy Bypass -File C:\inetpub\xprotect-dashboard\scripts\setup-iis-server.ps1
```

That creates `C:\inetpub\xprotect-dashboard`, app pool **XProtectDashboard** (no space, **No Managed Code**), the site on port 8080, and Modify on `App_Data`. Live `appsettings.json` is not overwritten after the first copy.

**Later publishes:** run `pack-for-iis.ps1 -IncludeSite` on FOX2208553, copy the new zip through RDP, then run `expand-iis-package.ps1` on FOXAWSMSAP076.

Then open `http://FOXAWSMSAP076:8080` and press Ctrl+F5.

If `Password` is `ENC:...`, `App_Data\keys` must exist on FOXAWSMSAP076 or login fails. If login fails, use **Connect to XProtect** on the new server. Do not paste the password into chat.

Server Status service checks now use **`CORP\FOXAWSMSAP076$`**, not `FOX2208553$`. Run `-ShowIisIdentity` on FOXAWSMSAP076, then grant that account on each monitored Windows host.

To target a different web server: `-RemoteComputer OTHERWEB01`.

## Host on IIS

On a **new web server**, HTTP 500.19 error `0x8007000d` on `C:\inetpub\xprotect-dashboard\web.config` means IIS cannot read the ASP.NET Core section. Install the Hosting Bundle first; the site files are not wrong.

1. Install IIS, then install the [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0) (ASP.NET Core Runtime 8.0 → **Hosting Bundle**). Run `iisreset`. If the bundle was installed before IIS, repair the Hosting Bundle.
2. In IIS, the app pool must be named **XProtectDashboard** (no space) and **No Managed Code** (not .NET CLR 4). If you also have **XProtect Dashboard** (with a space), run `scripts/fix-iis-app-pool.ps1` as Administrator on the **web server** to move the site and remove the extra pool.
3. From FOX2208553, run `scripts/publish-iis.ps1` so the live `appsettings.json` on FOXAWSMSAP076 is kept. A plain `dotnet publish` to the web server must not replace that file.

   If you publish with `dotnet publish` yourself, do not replace `C:\inetpub\xprotect-dashboard\appsettings.json` on FOXAWSMSAP076. That file is what switches **Demo data** to your live recording servers and cameras (`Milestone:UseDemoData` = `false`).

4. In IIS on FOXAWSMSAP076, create a site or application pointed at that folder if `setup-iis-server.ps1` has not already done so.
5. Grant the app-pool identity read access to the publish folder and write access to `App_Data` and `logs`. If the site shows `Access to the path 'C:\Windows\TEMP\MilestoneDashboard' is denied`, the app pool cannot write `App_Data`. On FOXAWSMSAP076 run `scripts/grant-app-data-access.ps1` as Administrator, then recycle **XProtectDashboard** and press Ctrl+F5.
6. If you set `ConnectionStrings:Dashboard`, the app creates a small `DashboardSnapshots` table and stores the last successful API pull there. Use a dedicated database, not the XProtect `Surveillance` database.

Windows authentication to XProtect is not used. Use an XProtect Basic user, and prefer HTTPS between the web server and the API Gateway.

## Grant IIS permission to read Windows services

Server Status can list SQL/IIS service state only when the **XProtectDashboard** app pool is allowed to query `Win32_Service` on that host. **No access** means the host answered SMB/RDP, but CIM/WMI was denied.

Do this once.

1. On **FOXAWSMSAP076**, open PowerShell as Administrator and print the account to grant:

   ```powershell
   C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -ExecutionPolicy Bypass -File C:\inetpub\xprotect-dashboard\scripts\grant-remote-service-access.ps1 -ShowIisIdentity
   ```

   If the app pool is still **ApplicationPoolIdentity**, the account is `CORP\FOXAWSMSAP076$`.

2. On **each monitored Windows server**, open PowerShell as Administrator and grant that account (use the value from step 1):

   ```powershell
   C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -ExecutionPolicy Bypass -File \\FOXAWSMSAP076\C$\inetpub\xprotect-dashboard\scripts\grant-remote-service-access.ps1 -Account "CORP\FOXAWSMSAP076$"
   ```

   That adds the account to **Distributed COM Users** and **Remote Management Users**, turns on the WMI/WinRM firewall rules, and grants **Remote Enable** on `root\cimv2`.

3. Recycle the **XProtectDashboard** app pool and press Ctrl+F5. SQL/IIS pills should change from **No access** to **Running** or **Stopped**.

If FOXUSWDMSDB305 still shows **No access**, you probably granted the old PC account (`FOX2208553$`). Run `-ShowIisIdentity` on **FOXAWSMSAP076**, then grant **that** account (`CORP\FOXAWSMSAP076$`) on FOXUSWDMSDB305. From FOXAWSMSAP076 (not FOX2208553), test with:

```powershell
C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -ExecutionPolicy Bypass -File C:\inetpub\xprotect-dashboard\scripts\test-remote-service-access.ps1 -ComputerName 10.180.80.156
```

The dashboard queries by IP over **DCOM**. WinRM to an IP fails with TrustedHosts (0x803381bb) and is not used.

Linux hosts (Aztec receivers) stay **None**. Do not run the grant script on those.

If your security team will not allow the IIS computer account, set the app pool to a domain service account and pass that account in `-Account`. Do not put the XProtect password in this script.

## Encrypt the password in appsettings.json

The dashboard can store `Milestone:Password` as an encrypted value (`ENC:...`) instead of plain text. Decrypt happens only when the site starts.

Do this **on the IIS web server** after you publish. The encrypted value only works on that same server.

The easiest way is the **Encrypt password** page in the sidebar. Type the XProtect Basic user password, keep **Save into appsettings.json** checked, then recycle the app pool and press Ctrl+F5.

The PowerShell script still works if you prefer it:

1. Publish the new site from FOX2208553 (`scripts/publish-iis.ps1`).
2. Open PowerShell **as Administrator** on **FOXAWSMSAP076**.
3. Run (copy the script there, or use the UNC path to your PC):

   ```powershell
   C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -ExecutionPolicy Bypass -File C:\inetpub\xprotect-dashboard\scripts\encrypt-password.ps1
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
