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

Stop-IisSiteIfExists
Write-Host "Stopping IIS"
iisreset /stop | Out-Host
Wait-ForUnlockedDll

Write-Host "Publishing to $SitePath"
dotnet publish $project -c Release -o $SitePath
if ($LASTEXITCODE -ne 0) {
    iisreset /start | Out-Host
    throw "Publish failed."
}

Write-Host "Starting IIS"
iisreset /start | Out-Host
if (Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue) {
    Start-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
}
if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
    Start-Website -Name $SiteName -ErrorAction SilentlyContinue
}

Write-Host "Publish complete. Hard-refresh http://localhost:8080 with Ctrl+F5."
