# Prefer C$ when it works. If the share is missing, pack a zip for RDP copy.
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

try {
    Write-Host "Trying admin share on $RemoteComputer"
    Copy-ScriptsToIis -RemoteComputer $RemoteComputer | Out-Null
    $local = Get-IisScriptsPath "setup-iis-server.ps1"
    $ps = Get-WindowsPowerShell
    Write-Host "RDP to $RemoteComputer and run:"
    Write-Host "  $ps -ExecutionPolicy Bypass -File $local"
} catch {
    Write-Host "Admin share failed: $($_.Exception.Message)"
    Write-Host "Packing a zip to copy through RDP instead."
    & (Join-Path $PSScriptRoot "pack-for-iis.ps1")
}
