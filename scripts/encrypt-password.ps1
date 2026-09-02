#Requires -RunAsAdministrator
# Run on FOXAWSMSAP076 after publish. The encrypted value only works on that IIS server.
param(
    [string]$SitePath = "C:\inetpub\xprotect-dashboard",
    [string]$AppPoolName = "XProtectDashboard"
)

$ErrorActionPreference = "Stop"
$appsettings = Join-Path $SitePath "appsettings.json"
$dll = Join-Path $SitePath "Milestone.Dashboard.dll"
$keys = Join-Path $SitePath "App_Data\keys"
$dotnet = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"

if (-not (Test-Path $appsettings)) { throw "Cannot find $appsettings" }
if (-not (Test-Path $dll)) { throw "Cannot find $dll. Publish the site first." }
if (-not (Test-Path $dotnet)) { throw "Cannot find $dotnet. Install the .NET 8 Hosting Bundle." }

New-Item -ItemType Directory -Force -Path $keys | Out-Null
icacls (Join-Path $SitePath "App_Data") /grant "IIS AppPool\${AppPoolName}:(OI)(CI)M" /T | Out-Null

$secure = Read-Host "Type the current XProtect password" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

if ([string]::IsNullOrWhiteSpace($plain)) {
    throw "Password was empty."
}

Push-Location $SitePath
try {
    $encrypted = $plain | & $dotnet exec $dll encrypt-password
} finally {
    Pop-Location
    $plain = $null
}

$encrypted = ($encrypted | Select-Object -Last 1).Trim()
if ($encrypted -notlike "ENC:*") {
    throw "Encrypt failed. Output was: $encrypted"
}

$backup = "$appsettings.bak"
Copy-Item $appsettings $backup -Force
$raw = Get-Content $appsettings -Raw
$safe = $encrypted.Replace("\", "\\").Replace('"', '\"')
$updated = [regex]::Replace(
    $raw,
    '("Milestone"\s*:\s*\{[\s\S]*?"Password"\s*:\s*")([^"]*)(")',
    { param($match) $match.Groups[1].Value + $safe + $match.Groups[3].Value },
    1
)
if ($updated -eq $raw) {
    throw "Could not find Milestone.Password in appsettings.json. A backup is at $backup. Encrypted value: $encrypted"
}

Set-Content -Path $appsettings -Value $updated -Encoding UTF8
Write-Host ""
Write-Host "The password is now encrypted in appsettings.json"
Write-Host "A backup was saved as appsettings.json.bak"
Write-Host "Next: recycle the XProtectDashboard app pool, then press Ctrl+F5 in the browser."
