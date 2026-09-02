# One-time IIS setup for FOXAWSMSAP076.
# Run from FOX2208553 (it remotes to the web server) or on FOXAWSMSAP076 itself.
param(
    [string]$RemoteComputer,
    [string]$SitePath,
    [string]$AppPoolName,
    [string]$SiteName,
    [int]$Port,
    [string]$FromComputer
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "iis-common.ps1")
if (-not $RemoteComputer) { $RemoteComputer = $IisWebComputer }
if (-not $SitePath) { $SitePath = $IisSitePath }
if (-not $AppPoolName) { $AppPoolName = $IisAppPoolName }
if (-not $SiteName) { $SiteName = $IisSiteName }
if (-not $Port) { $Port = $IisPort }
if (-not $FromComputer) { $FromComputer = $IisDevComputer }

$setup = {
    param($SitePath, $AppPoolName, $SiteName, $Port, $WrongPool)
    $ErrorActionPreference = "Stop"
    Import-Module WebAdministration

    $module = $null
    if (Get-Command Get-WebGlobalModule -ErrorAction SilentlyContinue) {
        $module = Get-WebGlobalModule -Name "AspNetCoreModuleV2" -ErrorAction SilentlyContinue
    }
    $ancm = Join-Path ${env:ProgramFiles} "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    if (-not $module -and -not (Test-Path $ancm)) {
        throw @"
IIS on $env:COMPUTERNAME does not have AspNetCoreModuleV2.
Install the .NET 8 Hosting Bundle, then iisreset:
https://dotnet.microsoft.com/download/dotnet/8.0
Under ASP.NET Core Runtime 8.0, download Hosting Bundle.
"@
    }

    Write-Host "Creating $SitePath on $env:COMPUTERNAME"
    New-Item -ItemType Directory -Force -Path $SitePath, (Join-Path $SitePath "App_Data\keys"), (Join-Path $SitePath "logs") | Out-Null

    if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
        Write-Host "Creating app pool $AppPoolName"
        New-WebAppPool -Name $AppPoolName | Out-Null
    }
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" managedRuntimeVersion ""
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" enable32BitAppOnWin64 $false
    Write-Host "App pool $AppPoolName is No Managed Code."

    if (Test-Path "IIS:\AppPools\$WrongPool") {
        Write-Warning "Unused pool '$WrongPool' exists. After the site is on $AppPoolName, run scripts\fix-iis-app-pool.ps1 on this server."
    }

    $site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
    if (-not $site) {
        Write-Host "Creating site '$SiteName' on port $Port"
        New-Website -Name $SiteName -PhysicalPath $SitePath -ApplicationPool $AppPoolName -Port $Port | Out-Null
    } else {
        Set-ItemProperty "IIS:\Sites\$SiteName" applicationPool $AppPoolName
        Set-ItemProperty "IIS:\Sites\$SiteName" physicalPath $SitePath
        Write-Host "Site '$SiteName' now uses $AppPoolName at $SitePath"
    }

    $appData = Join-Path $SitePath "App_Data"
    $logs = Join-Path $SitePath "logs"
    icacls $appData /grant "IIS AppPool\${AppPoolName}:(OI)(CI)M" /T | Out-Host
    icacls $logs /grant "IIS AppPool\${AppPoolName}:(OI)(CI)M" /T | Out-Host

    if (Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue) {
        Restart-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    }

    Write-Host "IIS on $env:COMPUTERNAME is ready. Grant service checks using $env:USERDOMAIN\$env:COMPUTERNAME`$."
}

if (-not (Test-SameComputer $RemoteComputer)) {
    Write-Host "This PC is $env:COMPUTERNAME. Copying scripts to $RemoteComputer, then you run setup there."
    try {
        $dest = Get-UncSitePath -Computer $RemoteComputer -SitePath $SitePath
        New-Item -ItemType Directory -Force -Path $dest, (Join-Path $dest "App_Data\keys"), (Join-Path $dest "logs") | Out-Null
        Copy-ScriptsToIis -RemoteComputer $RemoteComputer | Out-Null
    } catch {
        Write-Host "Could not copy to $RemoteComputer from this PC: $($_.Exception.Message)"
        Write-Host "Packing a zip to copy through RDP instead."
        & (Join-Path $PSScriptRoot "pack-for-iis.ps1")
    }
    Write-Host ""
    Write-Host (Get-RunOnIisHelp "setup-iis-server.ps1")
    Write-Host ""
    Write-Host "After that succeeds, come back here and run copy-live-iis-data.ps1 and publish-iis.ps1."
    exit 1
} else {
    if (-not (Test-IsAdministrator)) {
        throw "Setting up IIS on this server requires Administrator PowerShell."
    }
    & $setup $SitePath $AppPoolName $SiteName $Port $IisWrongPoolName
}

$fromUnc = $null
try {
    $fromUnc = Get-UncSitePath -Computer $FromComputer -SitePath $SitePath
} catch {
    $fromUnc = $null
}
if ($fromUnc -and (Test-PathQuick (Join-Path $fromUnc "appsettings.json"))) {
    Write-Host "Found live settings on $FromComputer. Next:"
    Write-Host "  powershell -ExecutionPolicy Bypass -File .\scripts\copy-live-iis-data.ps1"
} else {
    Write-Host "No live appsettings.json found on $FromComputer."
    Write-Host "After the first publish, use Connect to XProtect on $RemoteComputer. Do not paste the password into chat."
}

Write-Host "Then from $IisDevComputer run:  powershell -ExecutionPolicy Bypass -File .\scripts\publish-iis.ps1"
Write-Host "Open http://${RemoteComputer}:${Port} and press Ctrl+F5."
