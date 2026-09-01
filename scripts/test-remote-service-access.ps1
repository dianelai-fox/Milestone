#Requires -RunAsAdministrator
param(
    [Parameter(Mandatory = $true)]
    [string]$ComputerName,
    [string]$AppPoolName = "XProtectDashboard"
)

$ErrorActionPreference = "Continue"
Write-Host "Testing service read from $env:COMPUTERNAME to $ComputerName"
Write-Host "This must be the IIS web server (the one with C:\inetpub\xprotect-dashboard), not FOXUSWDMSDB305."
Write-Host ""

Import-Module WebAdministration -ErrorAction SilentlyContinue
$poolUser = $null
if (Get-Command Get-IISAppPool -ErrorAction SilentlyContinue) {
    $pool = Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    $poolUser = $pool.ProcessModel.UserName
}
if (-not $poolUser) {
    Write-Warning "App pool $AppPoolName was not found on this machine. Run this script on the IIS web server."
} else {
    Write-Host "App pool identity: $(if ($poolUser) { $poolUser } else { 'ApplicationPoolIdentity' })"
    if ([string]::IsNullOrWhiteSpace($poolUser) -or $poolUser -eq "ApplicationPoolIdentity") {
        Write-Host "Remote calls use computer account: $env:USERDOMAIN\$env:COMPUTERNAME`$"
    }
}

$filter = "Name='MSSQLSERVER' OR Name LIKE 'MSSQL$%' OR Name='SQLSERVERAGENT' OR Name LIKE 'SQLAgent$%' OR Name='W3SVC'"
foreach ($protocol in @("Dcom", "Wsman")) {
    Write-Host ""
    Write-Host "Trying $protocol..."
    try {
        $opt = New-CimSessionOption -Protocol $protocol
        $session = New-CimSession -ComputerName $ComputerName -SessionOption $opt -OperationTimeoutSec 8
        try {
            $rows = @(Get-CimInstance -CimSession $session -ClassName Win32_Service -Filter $filter |
                Select-Object Name, State, DisplayName)
            if ($rows.Count -eq 0) {
                Write-Host "$protocol connected, but no SQL/IIS service names were returned. The instance may be named (MSSQL`$INSTANCE)."
            } else {
                $rows | Format-Table -AutoSize | Out-Host
            }
        } finally {
            Remove-CimSession $session
        }
    } catch {
        Write-Host "$protocol failed: $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "If both fail with Access denied, the account granted on $ComputerName is not the IIS outbound account."
Write-Host "Run grant-remote-service-access.ps1 -ShowIisIdentity on this IIS server, then grant THAT account on $ComputerName."
