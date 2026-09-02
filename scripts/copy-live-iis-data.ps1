# Copy live IIS settings and App_Data from the old site on FOX2208553
# to FOXAWSMSAP076. Does not copy binaries and does not print secrets.
param(
    [string]$FromComputer,
    [string]$ToComputer,
    [string]$SitePath
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "iis-common.ps1")
if (-not $FromComputer) { $FromComputer = $IisDevComputer }
if (-not $ToComputer) { $ToComputer = $IisWebComputer }
if (-not $SitePath) { $SitePath = $IisSitePath }

if (Test-SameComputer $FromComputer -and Test-SameComputer $ToComputer) {
    throw "From and To are the same computer ($env:COMPUTERNAME). Pass -FromComputer and -ToComputer."
}

$from = Get-UncSitePath -Computer $FromComputer -SitePath $SitePath
$to = Get-UncSitePath -Computer $ToComputer -SitePath $SitePath

if (-not (Test-Path $from)) {
    throw "Source site folder was not found: $from. Publish used to live at $SitePath on $FromComputer."
}

Write-Host "Copying live IIS data"
Write-Host "  from $from"
Write-Host "  to   $to"
New-Item -ItemType Directory -Force -Path $to, (Join-Path $to "App_Data\keys"), (Join-Path $to "logs") | Out-Null

$copied = @()
foreach ($name in @("appsettings.json", "appsettings.Production.json")) {
    $source = Join-Path $from $name
    if (Test-Path $source) {
        Copy-Item $source (Join-Path $to $name) -Force
        $copied += $name
        Write-Host "Copied $name"
    } else {
        Write-Host "Skipped $name (not on $FromComputer)"
    }
}

$fromData = Join-Path $from "App_Data"
$toData = Join-Path $to "App_Data"
if (Test-Path $fromData) {
    New-Item -ItemType Directory -Force -Path $toData | Out-Null
    & robocopy $fromData $toData /E /R:2 /W:2 /NFL /NDL | Out-Host
    if ($LASTEXITCODE -ge 8) {
        throw "App_Data copy failed (robocopy exit $LASTEXITCODE)."
    }
    $copied += "App_Data"
    Write-Host "Copied App_Data (map pins, Server Status CSV, data-protection keys)"
} else {
    Write-Host "Skipped App_Data (not on $FromComputer)"
}

if ($copied.Count -eq 0) {
    throw "Nothing to copy. $from has no appsettings.json or App_Data."
}

$remoteAcl = {
    param($SitePath, $AppPoolName)
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    $appData = Join-Path $SitePath "App_Data"
    $logs = Join-Path $SitePath "logs"
    New-Item -ItemType Directory -Force -Path (Join-Path $appData "keys"), $logs | Out-Null
    icacls $appData /grant "IIS AppPool\${AppPoolName}:(OI)(CI)M" /T | Out-Host
    icacls $logs /grant "IIS AppPool\${AppPoolName}:(OI)(CI)M" /T | Out-Host
    if (Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue) {
        Restart-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
        Write-Host "Recycled $AppPoolName on $env:COMPUTERNAME"
    }
}

try {
    Invoke-OnComputer -Computer $ToComputer -ScriptBlock $remoteAcl -ArgumentList @($SitePath, $IisAppPoolName)
} catch {
    Write-Warning "Copied files, but could not grant App_Data or recycle the pool: $($_.Exception.Message)"
    Write-Warning "On $ToComputer run: powershell -ExecutionPolicy Bypass -File .\scripts\grant-app-data-access.ps1"
}

Write-Host ""
Write-Host "Live settings are on $ToComputer. Secrets were not printed."
Write-Host "If Password starts with ENC:, App_Data\keys must be on $ToComputer or login will fail."
Write-Host "If login fails, use Connect to XProtect on $ToComputer. Do not paste the password into chat."
Write-Host "Open http://${ToComputer}:${IisPort} and press Ctrl+F5."
