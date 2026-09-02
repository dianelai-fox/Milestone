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
if (Test-Path "IIS:\AppPools\$resolvedPool") {
    $poolUser = (Get-Item "IIS:\AppPools\$resolvedPool").processModel.userName
    if ([string]::IsNullOrWhiteSpace($poolUser)) {
        $poolUser = "ApplicationPoolIdentity"
    }
    Write-Host "App pool identity: $poolUser"
    if ($poolUser -eq "ApplicationPoolIdentity" -or $poolUser -eq "NetworkService" -or $poolUser -eq "LocalSystem") {
        Write-Host "Remote calls use computer account: $env:USERDOMAIN\$env:COMPUTERNAME`$"
    }
} elseif (Get-Command Get-IISAppPool -ErrorAction SilentlyContinue) {
    $pool = Get-IISAppPool -Name $resolvedPool -ErrorAction SilentlyContinue
    $poolUser = $pool.ProcessModel.UserName
    Write-Host "App pool identity: $(if ($poolUser) { $poolUser } else { 'ApplicationPoolIdentity' })"
} else {
    Write-Warning "Could not read app pool $resolvedPool. The site was still found if the line above listed it."
}

function Test-TcpPort([string]$Target, [int]$Port) {
    try {
        $r = Test-NetConnection -ComputerName $Target -Port $Port -WarningAction SilentlyContinue
        $ok = [bool]$r.TcpTestSucceeded
        Write-Host ("  TCP {0,-5} {1}" -f $Port, $(if ($ok) { "open" } else { "blocked or no reply" }))
        return $ok
    } catch {
        Write-Host ("  TCP {0,-5} {1}" -f $Port, $_.Exception.Message)
        return $false
    }
}

$isIp = [System.Net.IPAddress]::TryParse($ComputerName, [ref]([System.Net.IPAddress]$null))
Write-Host ""
Write-Host "Port check from $env:COMPUTERNAME (SMB 445 / RDP 3389 can work while WMI 135 is blocked):"
$tcp445 = Test-TcpPort $ComputerName 445
$tcp3389 = Test-TcpPort $ComputerName 3389
$tcp135 = Test-TcpPort $ComputerName 135

$filter = "Name='MSSQLSERVER' OR Name LIKE 'MSSQL`$%' OR Name='SQLSERVERAGENT' OR Name LIKE 'SQLAgent`$%' OR Name='W3SVC'"
$protocols = @("Dcom")
if (-not $isIp) {
    $protocols += "Wsman"
}

$dcomTimedOut = $false
foreach ($protocol in $protocols) {
    Write-Host ""
    Write-Host "Trying $protocol..."
    try {
        $opt = New-CimSessionOption -Protocol $protocol
        $session = New-CimSession -ComputerName $ComputerName -SessionOption $opt -OperationTimeoutSec 8 -ErrorAction Stop
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
        $msg = $_.Exception.Message
        Write-Host "$protocol failed: $msg"
        if ($msg -match "Timed out|timeout|0x40004") {
            $dcomTimedOut = $true
        }
    }
}

if ($isIp) {
    Write-Host ""
    Write-Host "WinRM was skipped because the target is an IP address. WinRM needs a host name, HTTPS, or TrustedHosts."
}

Write-Host ""
if ($dcomTimedOut) {
    Write-Host "This is a firewall / RPC path problem, not a SQL login problem."
    Write-Host "FOXAWSMSAP076 is the IIS server. FOXUSWDMSDB305 (10.180.80.156) is on-prem."
    if (-not $tcp135) {
        Write-Host "TCP 135 (RPC mapper) did not connect. Granting CORP\$($env:COMPUTERNAME)`$ will not help until 135 is open."
    } else {
        Write-Host "TCP 135 is open. DCOM still timed out, so the RPC ports after the mapper are blocked or WMI is hanging."
        Write-Host "On FOXUSWDMSDB305 run grant-remote-service-access.ps1 -Account 'CORP\$($env:COMPUTERNAME)`$' so the WMI firewall group is on."
        Write-Host "If that is already done, ask the network team to allow dynamic RPC (often 49152-65535) from $env:COMPUTERNAME ($ComputerName path) to the SQL host."
    }
    if ($tcp445 -or $tcp3389) {
        Write-Host "SMB or RDP is open, so Server Status can show Online and still show No access on every service pill."
    }
    Write-Host "DCOM worked from FOX2208553 because that PC is on the same LAN as the SQL servers."
} elseif (-not $siteFound) {
    Write-Host "Next: run this same command on FOXAWSMSAP076, then recycle XProtectDashboard and press Ctrl+F5."
} else {
    Write-Host "If DCOM succeeded, recycle XProtectDashboard and press Ctrl+F5."
    Write-Host "If DCOM failed with Access denied, grant the IIS computer account on the target with grant-remote-service-access.ps1."
}
