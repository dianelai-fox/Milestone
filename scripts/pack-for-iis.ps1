# Build a zip on this PC. Copy the zip to FOXAWSMSAP076 through RDP.
# C$ and WinRM are not used. Does not put the XProtect password in the zip unless
# you pass -IncludeLiveSettings and it already exists in the old IIS folder.
param(
    [switch]$IncludeSite,
    [switch]$IncludeLiveSettings,
    [string]$ZipPath
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "iis-common.ps1")

$desktop = [Environment]::GetFolderPath("Desktop")
if (-not $ZipPath) {
    $ZipPath = Join-Path $desktop "xprotect-iis-package.zip"
}

$stage = Join-Path ([IO.Path]::GetTempPath()) ("xprotect-iis-pack-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $stage | Out-Null
try {
    $scriptsDest = Join-Path $stage "scripts"
    New-Item -ItemType Directory -Force -Path $scriptsDest | Out-Null
    Copy-Item (Join-Path $PSScriptRoot "*") $scriptsDest -Recurse -Force
    Write-Host "Packed scripts"

    if ($IncludeSite) {
        $project = Join-Path $PSScriptRoot "..\src\Milestone.Dashboard\Milestone.Dashboard.csproj"
        $publish = Join-Path $stage "site"
        if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
            throw "dotnet was not found. Install the .NET 8 SDK on this PC, or omit -IncludeSite."
        }
        dotnet publish $project -c Release -o $publish
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed."
        }
        foreach ($keepOut in @("appsettings.json", "appsettings.Production.json")) {
            $path = Join-Path $publish $keepOut
            if (Test-Path $path) { Remove-Item $path -Force }
        }
        foreach ($folder in @("App_Data", "logs")) {
            $path = Join-Path $publish $folder
            if (Test-Path $path) { Remove-Item $path -Recurse -Force }
        }
        Write-Host "Packed site files (appsettings.json and App_Data were left out)"
    }

    if ($IncludeLiveSettings) {
        $old = $IisSitePath
        $settings = Join-Path $stage "settings"
        New-Item -ItemType Directory -Force -Path $settings | Out-Null
        $copied = $false
        foreach ($name in @("appsettings.json", "appsettings.Production.json")) {
            $source = Join-Path $old $name
            if (Test-Path $source) {
                Copy-Item $source (Join-Path $settings $name) -Force
                $copied = $true
                Write-Host "Packed $name from the old IIS folder on this PC"
            }
        }
        $oldData = Join-Path $old "App_Data"
        if (Test-Path $oldData) {
            Copy-Item $oldData (Join-Path $settings "App_Data") -Recurse -Force
            $copied = $true
            Write-Host "Packed App_Data from the old IIS folder on this PC"
        }
        if (-not $copied) {
            Write-Host "No live IIS settings were found at $old. Use Connect to XProtect on the new server."
            Remove-Item $settings -Recurse -Force
        }
    }

    if (Test-Path $ZipPath) {
        Remove-Item $ZipPath -Force
    }
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $ZipPath
} finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Created $ZipPath"
Write-Host "Copy that zip onto FOXAWSMSAP076 through RDP (copy the file, paste on the server desktop)."
Write-Host "Do not use \\FOXAWSMSAP076\C$ — that share is not available."
Write-Host ""
$ps = Get-WindowsPowerShell
Write-Host "On FOXAWSMSAP076, Windows PowerShell as Administrator:"
Write-Host "  $ps -ExecutionPolicy Bypass -File C:\inetpub\xprotect-dashboard\scripts\expand-iis-package.ps1 -ZipPath `"<paste zip path>`""
Write-Host "If scripts are not on the server yet, unzip to the desktop and run expand-iis-package.ps1 from that scripts folder."
