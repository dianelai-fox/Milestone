#Requires -RunAsAdministrator
param(
    [string]$SitePath = "C:\inetpub\xprotect-dashboard",
    [string]$AppPoolName = "XProtectDashboard",
    [string]$SiteName = "XProtect Dashboard"
)

$ErrorActionPreference = "Stop"
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

function Save-LiveSettings {
    $paths = @(
        (Join-Path $SitePath "appsettings.json"),
        (Join-Path $SitePath "appsettings.Production.json")
    )
    $saved = @()
    foreach ($path in $paths) {
        if (Test-Path $path) {
            $backup = "$path.publish-keep"
            Copy-Item $path $backup -Force
            $saved += $backup
            Write-Host "Keeping live settings $path"
        }
    }
    return $saved
}

function Restore-LiveSettings($saved) {
    foreach ($backup in $saved) {
        $path = $backup -replace "\.publish-keep$", ""
        Copy-Item $backup $path -Force
        Remove-Item $backup -Force
        Write-Host "Restored live settings $path"
    }

    $settings = Join-Path $SitePath "appsettings.json"
    if (-not (Test-Path $settings)) {
        $template = Join-Path $SitePath "appsettings.template.json"
        if (Test-Path $template) {
            Copy-Item $template $settings
            Write-Host "Created $settings from the template. Set Milestone:UseDemoData to false for live XProtect."
        }
    }
}

function Assert-AspNetCoreHostingBundle {
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    $module = $null
    if (Get-Command Get-WebGlobalModule -ErrorAction SilentlyContinue) {
        $module = Get-WebGlobalModule -Name "AspNetCoreModuleV2" -ErrorAction SilentlyContinue
    }
    $ancm = Join-Path ${env:ProgramFiles} "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    if (-not $module -and -not (Test-Path $ancm)) {
        throw @"
IIS on this server does not have AspNetCoreModuleV2.
That is what causes HTTP 500.19 error 0x8007000d on web.config.

Install the .NET 8 Hosting Bundle on this machine, then iisreset:
https://dotnet.microsoft.com/download/dotnet/8.0
Under ASP.NET Core Runtime 8.0, download Hosting Bundle.

If the bundle was installed before IIS, repair the Hosting Bundle after IIS is present.
"@
    }
}

Assert-AspNetCoreHostingBundle
Stop-IisSiteIfExists
Write-Host "Stopping IIS"
iisreset /stop | Out-Host
Wait-ForUnlockedDll

$keptSettings = Save-LiveSettings
Write-Host "Publishing to $SitePath"
dotnet publish $project -c Release -o $SitePath
if ($LASTEXITCODE -ne 0) {
    Restore-LiveSettings $keptSettings
    iisreset /start | Out-Host
    throw "Publish failed."
}

Restore-LiveSettings $keptSettings

Write-Host "Starting IIS"
iisreset /start | Out-Host
if (Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue) {
    Start-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
}
if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
    Start-Website -Name $SiteName -ErrorAction SilentlyContinue
}

Write-Host "Publish complete. Hard-refresh http://localhost:8080 with Ctrl+F5."
Write-Host "If the badge still says Demo data, the IIS appsettings.json still has Milestone:UseDemoData true."
