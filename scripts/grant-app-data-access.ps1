#Requires -RunAsAdministrator
# Run on FOXAWSMSAP076 (the IIS web server).
param(
    [string]$SitePath = "C:\inetpub\xprotect-dashboard",
    [string]$AppPoolName = "XProtectDashboard"
)

$ErrorActionPreference = "Stop"

$appData = Join-Path $SitePath "App_Data"
$keys = Join-Path $appData "keys"
$logs = Join-Path $SitePath "logs"
$identity = "IIS AppPool\$AppPoolName"

New-Item -ItemType Directory -Force -Path $keys, $logs | Out-Null
icacls $appData /grant "${identity}:(OI)(CI)M" /T | Out-Host
icacls $logs /grant "${identity}:(OI)(CI)M" /T | Out-Host

Import-Module WebAdministration -ErrorAction SilentlyContinue
if (Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue) {
    Restart-WebAppPool -Name $AppPoolName
    Write-Host "Recycled $AppPoolName"
}

Write-Host ""
Write-Host "The IIS app pool $AppPoolName can now write App_Data and logs."
Write-Host "That stops Access to the path C:\Windows\TEMP\MilestoneDashboard is denied."
Write-Host "Open the site and press Ctrl+F5."
