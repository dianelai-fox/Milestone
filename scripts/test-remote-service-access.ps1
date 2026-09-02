#Requires -RunAsAdministrator
param(
    [Parameter(Mandatory = $true)]
    [string]$ComputerName,
    [string]$AppPoolName = "XProtectDashboard"
)

$ErrorActionPreference = "Continue"
Write-Host "Testing service read from $env:COMPUTERNAME to $ComputerName"
Write-Host ""

$siteRoot = "C:\inetpub\xprotect-dashboard"
$siteFound = Test-Path $siteRoot
if (-not $siteFound) {
    Write-Warning "This computer is $env:COMPUTERNAME. C:\inetpub\xprotect-dashboard is missing."
    Write-Warning "FOX2208553 is the development PC. Run this on FOXAWSMSAP076, the IIS web server."
    Write-Warning "DCOM can succeed from your login on the PC and still fail from IIS."
}

Import-Module WebAdministration -ErrorAction SilentlyContinue
$resolvedPool = $AppPoolName
if (Get-Command Get-Website -ErrorAction SilentlyContinue) {
    foreach ($site in @(Get-Website)) {
        if (-not $site.physicalPath) { continue }
        $path = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($site.physicalPath)).TrimEnd('\')
        if ($path -eq [IO.Path]::GetFullPath($siteRoot).TrimEnd('\')) {
            $resolvedPool = $site.applicationPool
            Write-Host "Found dashboard site '$($site.Name)' using app pool '$resolvedPool'"
            break
        }
    }
}

$poolUser = $null
if (Get-Command Get-IISAppPool -ErrorAction SilentlyContinue) {
    $pool = Get-IISAppPool -Name $resolvedPool -ErrorAction SilentlyContinue
    $poolUser = $pool.ProcessModel.UserName
}
if (-not $poolUser) {
    Write-Warning "App pool $resolvedPool was not found on this machine."
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
