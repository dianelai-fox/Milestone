# Copy scripts from FOX2208553 onto FOXAWSMSAP076.
# Run this on the development PC. Do not run scripts from \\FOX2208553\C$\Users\dianela on the web server.
param(
    [string]$RemoteComputer
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "iis-common.ps1")
if (-not $RemoteComputer) { $RemoteComputer = $IisWebComputer }

if (Test-SameComputer $RemoteComputer) {
    Write-Host "This computer is $RemoteComputer. Scripts are already local under $(Get-IisScriptsPath)."
    return
}

Write-Host "Copying scripts from $env:COMPUTERNAME to $RemoteComputer"
Copy-ScriptsToIis -RemoteComputer $RemoteComputer | Out-Null
$local = Get-IisScriptsPath "setup-iis-server.ps1"
$ps = Get-WindowsPowerShell
Write-Host ""
Write-Host "RDP to $RemoteComputer. Open Windows PowerShell as Administrator (not PowerShell 7)."
Write-Host "Press Ctrl+C if a UNC command is still frozen."
Write-Host ""
Write-Host "  $ps -ExecutionPolicy Bypass -File $local"
