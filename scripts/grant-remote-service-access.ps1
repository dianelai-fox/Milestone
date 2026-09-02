#Requires -RunAsAdministrator
# -ShowIisIdentity must run on FOXAWSMSAP076. FOX2208553 is only the development PC.
param(
    [string]$Account,
    [string]$AppPoolName = "XProtectDashboard",
    [switch]$ShowIisIdentity
)

$ErrorActionPreference = "Stop"

function Get-DashboardSite {
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    $expected = [IO.Path]::GetFullPath("C:\inetpub\xprotect-dashboard").TrimEnd('\')
    if (Get-Command Get-Website -ErrorAction SilentlyContinue) {
        foreach ($site in @(Get-Website)) {
            if (-not $site.physicalPath) { continue }
            $path = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($site.physicalPath)).TrimEnd('\')
            if ($path -eq $expected) {
                return [pscustomobject]@{ Found = $true; SiteName = $site.Name; AppPool = $site.applicationPool; Path = $path }
            }
        }
    }
    return [pscustomobject]@{ Found = Test-Path $expected; SiteName = $null; AppPool = $AppPoolName; Path = $expected }
}

function Get-IisOutboundAccount {
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    $site = Get-DashboardSite
    if ($site.AppPool) {
        $AppPoolName = $site.AppPool
    }
    $user = $null
    if (Get-Command Get-IISAppPool -ErrorAction SilentlyContinue) {
        $user = (Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue).ProcessModel.UserName
    }
    if (-not $user -and (Get-Command Get-WebConfigurationProperty -ErrorAction SilentlyContinue)) {
        $user = (Get-WebConfigurationProperty -Filter "/system.applicationHost/applicationPools/add[@name='$AppPoolName']/processModel" -Name userName).Value
    }

    if ([string]::IsNullOrWhiteSpace($user) -or $user -eq "ApplicationPoolIdentity" -or $user -eq "NetworkService" -or $user -eq "LocalSystem") {
        $machine = "$env:USERDOMAIN\$env:COMPUTERNAME`$".ToUpperInvariant()
        return [pscustomobject]@{
            AppPoolUser = $(if ($user) { $user } else { "ApplicationPoolIdentity" })
            GrantAccount = $machine
            Note = "This app pool uses a local identity. Remote WMI/WinRM uses the IIS computer account $machine."
        }
    }

    return [pscustomobject]@{
        AppPoolUser = $user
        GrantAccount = $user
        Note = "Grant this domain account on each monitored Windows server."
    }
}

function Add-LocalGroupIfMissing([string]$Group, [string]$Member) {
    try {
        if (-not (Get-LocalGroupMember -Group $Group -Member $Member -ErrorAction SilentlyContinue)) {
            Add-LocalGroupMember -Group $Group -Member $Member
            Write-Host "Added $Member to $Group"
        } else {
            Write-Host "$Member is already in $Group"
        }
    } catch {
        Write-Warning "Could not add $Member to $Group : $($_.Exception.Message)"
    }
}

function Grant-Cimv2RemoteRead([string]$Member) {
    $sid = ([System.Security.Principal.NTAccount]$Member).Translate([System.Security.Principal.SecurityIdentifier])
    $invoke = @{ Namespace = "root\cimv2"; Path = "__SystemSecurity=@" }
    $descriptor = (Invoke-WmiMethod @invoke -Name GetSecurityDescriptor).Descriptor
    $already = $false
    foreach ($existing in @($descriptor.DACL)) {
        if ($existing.Trustee.SidString -eq $sid.Value) {
            $already = $true
            break
        }
    }
    if ($already) {
        Write-Host "WMI root\cimv2 already has an ACE for $Member"
        return
    }

    $trustee = ([wmiclass]"Win32_Trustee").CreateInstance()
    $trustee.SidString = $sid.Value
    $ace = ([wmiclass]"Win32_Ace").CreateInstance()
    # Enable Account (1) + Execute Methods (2) + Remote Enable (32)
    $ace.AccessMask = 35
    $ace.AceFlags = 0
    $ace.AceType = 0
    $ace.Trustee = $trustee
    $descriptor.DACL += $ace
    $null = Invoke-WmiMethod @invoke -Name SetSecurityDescriptor -ArgumentList $descriptor
    Write-Host "Granted Remote Enable on root\cimv2 to $Member"
}

if ($ShowIisIdentity) {
    $site = Get-DashboardSite
    Write-Host "This computer: $env:COMPUTERNAME"
    if (-not $site.Found) {
        throw @"
C:\inetpub\xprotect-dashboard was not found on $env:COMPUTERNAME.
FOX2208553 is the development PC. The IIS site is on FOXAWSMSAP076.
RDP to FOXAWSMSAP076, browse http://localhost:8080, then run -ShowIisIdentity there.
"@
    }
    $identity = Get-IisOutboundAccount
    Write-Host "Site: $($site.SiteName)"
    Write-Host "App pool: $($identity.AppPoolUser) / $($site.AppPool)"
    Write-Host "IIS identity: $($identity.AppPoolUser)"
    Write-Host "Grant this account on each monitored Windows server:"
    Write-Host "  $($identity.GrantAccount)"
    Write-Host $identity.Note
    Write-Host ""
    Write-Host "On each monitored server, run:"
    Write-Host "  powershell -ExecutionPolicy Bypass -File .\scripts\grant-remote-service-access.ps1 -Account '$($identity.GrantAccount)'"
    return
}

if ([string]::IsNullOrWhiteSpace($Account)) {
    throw "Pass -ShowIisIdentity on the IIS server, or -Account 'DOMAIN\\user-or-computer$' on each monitored Windows server."
}

Write-Host "Granting remote service-read access to $Account on $env:COMPUTERNAME"

Add-LocalGroupIfMissing "Distributed COM Users" $Account
Add-LocalGroupIfMissing "Performance Monitor Users" $Account
if (Get-LocalGroup -Name "Remote Management Users" -ErrorAction SilentlyContinue) {
    Add-LocalGroupIfMissing "Remote Management Users" $Account
}

Grant-Cimv2RemoteRead $Account

try {
    Enable-NetFirewallRule -DisplayGroup "Windows Management Instrumentation (WMI)" -ErrorAction SilentlyContinue
    Enable-NetFirewallRule -DisplayGroup "Windows Remote Management" -ErrorAction SilentlyContinue
    Write-Host "Enabled WMI and WinRM firewall groups when present."
} catch {
    Write-Warning "Could not enable firewall groups: $($_.Exception.Message)"
}

try {
    Enable-PSRemoting -Force
    Write-Host "WinRM is enabled."
} catch {
    Write-Warning "WinRM was not enabled: $($_.Exception.Message). DCOM WMI can still work."
}

Write-Host "Done. Recycle the XProtectDashboard app pool, then refresh Server Status."
