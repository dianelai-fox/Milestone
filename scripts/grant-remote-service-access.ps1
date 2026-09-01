#Requires -RunAsAdministrator
param(
    [string]$Account,
    [string]$AppPoolName = "XProtectDashboard",
    [switch]$ShowIisIdentity
)

$ErrorActionPreference = "Stop"

function Get-IisOutboundAccount {
    Import-Module WebAdministration -ErrorAction SilentlyContinue
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
    $siteRoot = "C:\inetpub\xprotect-dashboard"
    if (-not (Test-Path $siteRoot) -and -not (Get-Command Get-IISAppPool -ErrorAction SilentlyContinue)) {
        throw "Run -ShowIisIdentity on the IIS web server that hosts the dashboard (C:\inetpub\xprotect-dashboard), not on FOXUSWDMSDB305 or your PC."
    }
    $identity = Get-IisOutboundAccount
    Write-Host "App pool: $AppPoolName"
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
