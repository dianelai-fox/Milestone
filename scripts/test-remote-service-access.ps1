#Requires -RunAsAdministrator
param(
    [Parameter(Mandatory = $true)]
    [string]$ComputerName,
    [string]$AppPoolName = "XProtectDashboard"
)

$ErrorActionPreference = "Continue"
Write-Host "Testing service read from $env:COMPUTERNAME to $ComputerName"
Write-Host "Run this on the IIS web server (C:\inetpub\xprotect-dashboard), not on FOXUSWDMSDB305."
Write-Host ""

$siteRoot = "C:\inetpub\xprotect-dashboard"
if (-not (Test-Path $siteRoot)) {
    Write-Warning "C:\inetpub\xprotect-dashboard was not found. You are on $env:COMPUTERNAME, not the IIS web server."
    Write-Warning "DCOM can succeed here and still fail from IIS. Copy this script to the IIS box and run it there."
}

Import-Module WebAdministration -ErrorAction SilentlyContinue
$poolUser = $null
if (Get-Command Get-IISAppPool -ErrorAction SilentlyContinue) {
    $pool = Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    $poolUser = $pool.ProcessModel.UserName
}
if (-not $poolUser) {
    Write-Warning "App pool $AppPoolName was not found on this machine."
} else {
    Write-Host "App pool identity: $(if ($poolUser) { $poolUser } else { 'ApplicationPoolIdentity' })"
    if ([string]::IsNullOrWhiteSpace($poolUser) -or $poolUser -eq "ApplicationPoolIdentity") {
        Write-Host "Remote calls use computer account: $env:USERDOMAIN\$env:COMPUTERNAME`$"
    }
}

$isIp = [System.Net.IPAddress]::TryParse($ComputerName, [ref]([System.Net.IPAddress]$null))
$filter = "Name='MSSQLSERVER' OR Name LIKE 'MSSQL`$%' OR Name='SQLSERVERAGENT' OR Name LIKE 'SQLAgent`$%' OR Name='W3SVC'"
$protocols = @("Dcom")
if (-not $isIp) {
    $protocols += "Wsman"
}

foreach ($protocol in $protocols) {
    Write-Host ""
    Write-Host "Trying $protocol..."
    try {
        $opt = New-CimSessionOption -Protocol $protocol
        $session = New-CimSession -ComputerName $ComputerName -SessionOption $opt -OperationTimeoutSec 8
        try {
            $rows = @(Get-CimInstance -CimSession $session -ClassName Win32_Service -Filter $filter |
                Select-Object Name, State, DisplayName)
            if ($rows.Count -eq 0) {
                Write-Host "$protocol connected, but no SQL/IIS service names were returned."
            } else {
                $rows | Format-Table -AutoSize | Out-Host
                Write-Host "$protocol succeeded. The dashboard uses DCOM and the server IP, so this is the path IIS must be able to use."
            }
        } finally {
            Remove-CimSession $session
        }
    } catch {
        Write-Host "$protocol failed: $($_.Exception.Message)"
    }
}

if ($isIp) {
    Write-Host ""
    Write-Host "WinRM was skipped because the target is an IP address. WinRM needs a host name, HTTPS, or TrustedHosts."
}

Write-Host ""
Write-Host "Next: from the IIS web server, run this same command. Then recycle XProtectDashboard and press Ctrl+F5."
