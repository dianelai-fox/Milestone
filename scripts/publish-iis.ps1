# Publish the dashboard to IIS.
# On FOX2208553 this copies to FOXAWSMSAP076.
# On FOXAWSMSAP076 this publishes into C:\inetpub\xprotect-dashboard.
# Live appsettings.json and App_Data are never overwritten.
param(
    [string]$RemoteComputer,
    [string]$SitePath,
    [string]$AppPoolName,
    [string]$SiteName,
    [switch]$Local
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "iis-common.ps1")
if (-not $RemoteComputer) { $RemoteComputer = $IisWebComputer }
if (-not $SitePath) { $SitePath = $IisSitePath }
if (-not $AppPoolName) { $AppPoolName = $IisAppPoolName }
if (-not $SiteName) { $SiteName = $IisSiteName }

if (-not $Local -and -not (Test-SameComputer $RemoteComputer)) {
    Write-Host "This PC is $env:COMPUTERNAME. Publishing to $RemoteComputer."
    & (Join-Path $PSScriptRoot "publish-remote-iis.ps1") `
        -RemoteComputer $RemoteComputer `
        -SitePath $SitePath `
        -AppPoolName $AppPoolName `
        -SiteName $SiteName
    return
}

if (-not (Test-IsAdministrator)) {
    throw "Publishing on this IIS server requires Administrator PowerShell."
}

$project = Join-Path $PSScriptRoot "..\src\Milestone.Dashboard\Milestone.Dashboard.csproj"

function Stop-IisSiteIfExists {
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    if (Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue) {
        Write-Host "Stopping app pool $AppPoolName"
        Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    }
    if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
        Write-Host "Stopping site $SiteName"
        Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
    }
}

function Wait-ForUnlockedDll {
    $dll = Join-Path $SitePath "Milestone.Dashboard.dll"
    for ($i = 0; $i -lt 20; $i++) {
        if (-not (Test-Path $dll)) { return }
        try {
            $stream = [System.IO.File]::Open($dll, "Open", "ReadWrite", "None")
            $stream.Close()
            return
        } catch {
            Start-Sleep -Seconds 1
        }
    }
    throw "IIS is still locking $dll. Close IIS Manager if it is open, then rerun this script."
}

Assert-AspNetCoreHostingBundle
Stop-IisSiteIfExists
Write-Host "Stopping IIS"
iisreset /stop | Out-Host
Wait-ForUnlockedDll

$keptSettings = Save-LiveSettings $SitePath
Write-Host "Publishing to $SitePath"
dotnet publish $project -c Release -o $SitePath
if ($LASTEXITCODE -ne 0) {
    Restore-LiveSettings $SitePath $keptSettings
    iisreset /start | Out-Host
    throw "Publish failed."
}

Restore-LiveSettings $SitePath $keptSettings
Grant-DashboardAppData -SitePath $SitePath -AppPoolName $AppPoolName

Write-Host "Starting IIS"
iisreset /start | Out-Host
if (Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue) {
    Start-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
}
if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
    Start-Website -Name $SiteName -ErrorAction SilentlyContinue
}

Write-Host "Publish complete. Hard-refresh http://localhost:$IisPort with Ctrl+F5."
Write-Host "If the badge still says Demo data, the IIS appsettings.json still has Milestone:UseDemoData true."
