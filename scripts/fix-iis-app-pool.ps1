#Requires -RunAsAdministrator
param(
    [string]$SitePath = "C:\inetpub\xprotect-dashboard",
    [string]$CorrectPool = "XProtectDashboard",
    [string]$WrongPool = "XProtect Dashboard",
    [string]$SiteName = "XProtect Dashboard"
)

$ErrorActionPreference = "Stop"
Import-Module WebAdministration

function Ensure-NoManagedCodePool([string]$Name) {
    if (-not (Test-Path "IIS:\AppPools\$Name")) {
        Write-Host "Creating app pool $Name"
        New-WebAppPool -Name $Name | Out-Null
    }
    Set-ItemProperty "IIS:\AppPools\$Name" managedRuntimeVersion ""
    Set-ItemProperty "IIS:\AppPools\$Name" enable32BitAppOnWin64 $false
    Write-Host "App pool $Name is No Managed Code (required for ASP.NET Core)."
}

Ensure-NoManagedCodePool $CorrectPool

$assigned = $false
if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
    Set-ItemProperty "IIS:\Sites\$SiteName" applicationPool $CorrectPool
    Write-Host "Site '$SiteName' now uses $CorrectPool"
    $assigned = $true
}

$expected = [IO.Path]::GetFullPath($SitePath).TrimEnd('\')
foreach ($site in @(Get-Website)) {
    if (-not $site.physicalPath) { continue }
    $path = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($site.physicalPath)).TrimEnd('\')
    if ($path -eq $expected -and $site.applicationPool -ne $CorrectPool) {
        Set-ItemProperty "IIS:\Sites\$($site.Name)" applicationPool $CorrectPool
        Write-Host "Site '$($site.Name)' now uses $CorrectPool"
        $assigned = $true
    }
}

if (-not $assigned) {
    Write-Warning "No IIS site pointed at $SitePath was found. In IIS Manager, set the XProtect Dashboard site to app pool $CorrectPool."
}

if (Get-WebAppPoolState -Name $CorrectPool -ErrorAction SilentlyContinue) {
    Restart-WebAppPool -Name $CorrectPool
    Write-Host "Recycled $CorrectPool"
}

if (Test-Path "IIS:\AppPools\$WrongPool") {
    $stillUsed = @(Get-Website | Where-Object { $_.applicationPool -eq $WrongPool })
    $apps = Get-WebConfigurationProperty -Filter "/system.applicationHost/sites/site/application[@applicationPool='$WrongPool']" -Name path -ErrorAction SilentlyContinue
    if ($stillUsed.Count -eq 0 -and -not $apps) {
        Stop-WebAppPool -Name $WrongPool -ErrorAction SilentlyContinue
        Remove-WebAppPool -Name $WrongPool
        Write-Host "Removed unused pool '$WrongPool'"
    } else {
        Write-Warning "Pool '$WrongPool' still has a site. Move that site to $CorrectPool, then rerun this script."
    }
}

Write-Host "Done. Open http://localhost:8080 and press Ctrl+F5."
Write-Host "The remaining pool name must be $CorrectPool with .NET CLR Version = No Managed Code."
