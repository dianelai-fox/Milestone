# Run on FOXAWSMSAP076. Unpacks a zip copied through RDP.
# Does not overwrite live appsettings.json or App_Data unless -ReplaceSettings is set.
param(
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,
    [string]$SitePath,
    [switch]$ReplaceSettings
)

$ErrorActionPreference = "Stop"
$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$common = Join-Path $here "iis-common.ps1"
if (Test-Path $common) {
    . $common
} else {
    $IisSitePath = "C:\inetpub\xprotect-dashboard"
    $IisAppPoolName = "XProtectDashboard"
    $IisSiteName = "XProtect Dashboard"
    $IisWrongPoolName = "XProtect Dashboard"
    $IisPort = 8080
    $IisWebComputer = "FOXAWSMSAP076"
    function Test-IsAdministrator {
        $id = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = [Security.Principal.WindowsPrincipal]$id
        return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    function Get-WindowsPowerShell {
        return Join-Path $env:WINDIR "System32\WindowsPowerShell\v1.0\powershell.exe"
    }
}

if (-not $SitePath) { $SitePath = $IisSitePath }

if (-not (Test-Path $ZipPath)) {
    throw "Zip not found: $ZipPath. Copy xprotect-iis-package.zip to this server through RDP, then pass -ZipPath."
}

if (-not (Test-IsAdministrator)) {
    throw "Open Windows PowerShell as Administrator on $IisWebComputer."
}

$extract = Join-Path ([IO.Path]::GetTempPath()) ("xprotect-iis-expand-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $extract | Out-Null
try {
    Expand-Archive -Path $ZipPath -DestinationPath $extract -Force
    $scriptsSource = Join-Path $extract "scripts"
    if (-not (Test-Path $scriptsSource)) {
        if (Test-Path (Join-Path $extract "setup-iis-server.ps1")) {
            $scriptsSource = $extract
        } else {
            throw "The zip does not contain a scripts folder. Recreate it with pack-for-iis.ps1 on FOX2208553."
        }
    }

    $scriptsDest = Join-Path $SitePath "scripts"
    New-Item -ItemType Directory -Force -Path $SitePath, $scriptsDest, (Join-Path $SitePath "App_Data\keys"), (Join-Path $SitePath "logs") | Out-Null
    Copy-Item (Join-Path $scriptsSource "*") $scriptsDest -Recurse -Force
    Write-Host "Installed scripts to $scriptsDest"

    $siteSource = Join-Path $extract "site"
    if (Test-Path $siteSource) {
        & robocopy $siteSource $SitePath /E /XF appsettings.json appsettings.Production.json /XD App_Data logs scripts /R:2 /W:2 | Out-Host
        if ($LASTEXITCODE -ge 8) {
            throw "Site file copy failed (robocopy exit $LASTEXITCODE)."
        }
        Write-Host "Copied site files. Live appsettings.json and App_Data were kept."
    }

    $settings = Join-Path $extract "settings"
    if (Test-Path $settings) {
        foreach ($name in @("appsettings.json", "appsettings.Production.json")) {
            $source = Join-Path $settings $name
            $dest = Join-Path $SitePath $name
            if (Test-Path $source) {
                if ((Test-Path $dest) -and -not $ReplaceSettings) {
                    Write-Host "Kept existing $dest"
                } else {
                    Copy-Item $source $dest -Force
                    Write-Host "Copied $name"
                }
            }
        }
        $fromData = Join-Path $settings "App_Data"
        $toData = Join-Path $SitePath "App_Data"
        if (Test-Path $fromData) {
            if ((Test-Path $toData) -and -not $ReplaceSettings) {
                Write-Host "Kept existing $toData"
            } else {
                New-Item -ItemType Directory -Force -Path $toData | Out-Null
                & robocopy $fromData $toData /E /R:2 /W:2 | Out-Host
                Write-Host "Copied App_Data"
            }
        }
    }
} finally {
    Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue
}

$setup = Join-Path $SitePath "scripts\setup-iis-server.ps1"
Write-Host ""
Write-Host "Next, still on this server, run setup:"
Write-Host "  $(Get-WindowsPowerShell) -ExecutionPolicy Bypass -File $setup"
Write-Host "Then open http://${env:COMPUTERNAME}:${IisPort} and press Ctrl+F5."
Write-Host "Do not paste the XProtect password into chat."
