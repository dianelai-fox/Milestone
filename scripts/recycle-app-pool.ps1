#Requires -RunAsAdministrator
# Run on FOXAWSMSAP076 after a publish from FOX2208553 when WinRM is blocked.
param(
    [string]$AppPoolName,
    [string]$SitePath
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "iis-common.ps1")
if (-not $AppPoolName) { $AppPoolName = $IisAppPoolName }
if (-not $SitePath) { $SitePath = $IisSitePath }

if (-not (Test-SameComputer $IisWebComputer) -and -not (Test-Path $SitePath)) {
    Write-Host (Get-RunOnIisHelp "recycle-app-pool.ps1")
    exit 1
}

if (-not (Test-IsAdministrator)) {
    throw "Recycle the app pool from Administrator PowerShell on $IisWebComputer."
}

Grant-DashboardAppData -SitePath $SitePath -AppPoolName $AppPoolName
Import-Module WebAdministration
if (Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue) {
    Restart-WebAppPool -Name $AppPoolName
    Write-Host "Recycled $AppPoolName on $env:COMPUTERNAME"
} else {
    Write-Warning "App pool $AppPoolName was not found. Run setup-iis-server.ps1 on this server first."
}

Write-Host "Open http://${env:COMPUTERNAME}:${IisPort} and press Ctrl+F5."
