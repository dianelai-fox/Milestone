# Build on this PC (FOX2208553) and copy to IIS on FOXAWSMSAP076.
# Does not overwrite live appsettings.json or App_Data on the web server.
param(
    [string]$RemoteComputer,
    [string]$SitePath,
    [string]$AppPoolName,
    [string]$SiteName
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "iis-common.ps1")
if (-not $RemoteComputer) { $RemoteComputer = $IisWebComputer }
if (-not $SitePath) { $SitePath = $IisSitePath }
if (-not $AppPoolName) { $AppPoolName = $IisAppPoolName }
if (-not $SiteName) { $SiteName = $IisSiteName }

$project = Join-Path $PSScriptRoot "..\src\Milestone.Dashboard\Milestone.Dashboard.csproj"
$staging = Join-Path $PSScriptRoot "..\publish"
$dest = Get-UncSitePath -Computer $RemoteComputer -SitePath $SitePath

if (Test-SameComputer $RemoteComputer) {
    Write-Host "This computer is $RemoteComputer. Publishing locally."
    & (Join-Path $PSScriptRoot "publish-iis.ps1") -Local -SitePath $SitePath -AppPoolName $AppPoolName -SiteName $SiteName -RemoteComputer $RemoteComputer
    return
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet was not found on $env:COMPUTERNAME. Install the .NET 8 SDK on this PC, then rerun."
}

Write-Host "Building on $env:COMPUTERNAME"
Write-Host "Publishing to $RemoteComputer $SitePath"
if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}
dotnet publish $project -c Release -o $staging
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed on $env:COMPUTERNAME."
}

$remotePrep = {
    param($SitePath, $AppPoolName, $SiteName)
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $SitePath, (Join-Path $SitePath "App_Data\keys"), (Join-Path $SitePath "logs") | Out-Null
    if (Get-Command Stop-WebAppPool -ErrorAction SilentlyContinue) {
        if (Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue) {
            Write-Host "Stopping app pool $AppPoolName on $env:COMPUTERNAME"
            Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
        }
    }
}

$remoteFinish = {
    param($SitePath, $AppPoolName, $SiteName)
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    $appData = Join-Path $SitePath "App_Data"
    $logs = Join-Path $SitePath "logs"
    New-Item -ItemType Directory -Force -Path (Join-Path $appData "keys"), $logs | Out-Null
    icacls $appData /grant "IIS AppPool\${AppPoolName}:(OI)(CI)M" /T | Out-Host
    icacls $logs /grant "IIS AppPool\${AppPoolName}:(OI)(CI)M" /T | Out-Host
    $settings = Join-Path $SitePath "appsettings.json"
    if (-not (Test-Path $settings)) {
        $template = Join-Path $SitePath "appsettings.template.json"
        if (Test-Path $template) {
            Copy-Item $template $settings
            Write-Host "Created $settings from the template. Set Milestone:UseDemoData to false for live XProtect."
        }
    }
    if (Get-Command Start-WebAppPool -ErrorAction SilentlyContinue) {
        if (Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue) {
            Start-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
        }
        if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
            Start-Website -Name $SiteName -ErrorAction SilentlyContinue
        }
    }
}

try {
    Invoke-OnComputer -Computer $RemoteComputer -ScriptBlock $remotePrep -ArgumentList @($SitePath, $AppPoolName, $SiteName)
} catch {
    Write-Warning "WinRM cannot stop the app pool on $RemoteComputer. Copy will use the admin share."
    Write-Warning "If a DLL is locked, RDP to $RemoteComputer and run:"
    Write-Warning "  powershell -ExecutionPolicy Bypass -File $(Get-DevScriptsUnc 'recycle-app-pool.ps1')"
}

try {
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
} catch {
    Write-Host "Cannot reach $dest ($($_.Exception.Message)). Packing a zip for RDP copy."
    & (Join-Path $PSScriptRoot "pack-for-iis.ps1") -IncludeSite
    return
}

$kept = Save-LiveSettings $dest

Write-Host "Copying site files to $dest"
$robocopy = @(
    $staging, $dest, "/MIR",
    "/XF", "appsettings.json", "appsettings.Production.json",
    "/XD", "App_Data", "logs",
    "/R:2", "/W:2", "/NFL", "/NDL"
)
& robocopy @robocopy | Out-Host
if ($LASTEXITCODE -ge 8) {
    Restore-LiveSettings $dest $kept
    throw "Copy to $dest failed (robocopy exit $LASTEXITCODE)."
}

Restore-LiveSettings $dest $kept

try {
    Invoke-OnComputer -Computer $RemoteComputer -ScriptBlock $remoteFinish -ArgumentList @($SitePath, $AppPoolName, $SiteName)
} catch {
    Write-Warning "Files are on $RemoteComputer. Recycle the pool there (WinRM is blocked):"
    Write-Warning "  powershell -ExecutionPolicy Bypass -File $(Get-DevScriptsUnc 'recycle-app-pool.ps1')"
}

Write-Host ""
Write-Host "Publish complete. Live appsettings.json and App_Data on $RemoteComputer were kept."
Write-Host "Open http://${RemoteComputer}:${IisPort} and press Ctrl+F5."
Write-Host "If the badge still says Demo data, the IIS appsettings.json still has Milestone:UseDemoData true."
Write-Host "Server Status service checks use ${RemoteComputer}`$, not ${IisDevComputer}`$."
