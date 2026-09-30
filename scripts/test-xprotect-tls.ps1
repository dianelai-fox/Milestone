# Run on FOXAWSMSAP076. Does not use the XProtect password.
param(
    [string]$Gateway = "https://Milestone.int.apps.fox"
)

$ErrorActionPreference = "Continue"
$uri = [Uri]$Gateway.TrimEnd('/')
$hostName = $uri.Host
$port = if ($uri.IsDefaultPort) { 443 } else { $uri.Port }

Write-Host "TLS check from $env:COMPUTERNAME to ${hostName}:${port}"
Write-Host ""

try {
    $dns = [System.Net.Dns]::GetHostAddresses($hostName)
    Write-Host ("DNS: " + ($dns | ForEach-Object { $_.IPAddressToString }) -join ", ")
} catch {
    Write-Host "DNS failed: $($_.Exception.Message)"
    Write-Host "FOXAWSMSAP076 cannot resolve $hostName. Add DNS or a hosts entry, then retry."
    exit 1
}

$tcp = Test-NetConnection -ComputerName $hostName -Port $port -WarningAction SilentlyContinue
Write-Host ("TCP ${port}: " + $(if ($tcp.TcpTestSucceeded) { "open" } else { "blocked" }))
if (-not $tcp.TcpTestSucceeded) {
    Write-Host "Ask the network team to allow HTTPS from $env:COMPUTERNAME to $hostName`:$port."
    exit 1
}

$script:tlsErrors = [System.Net.Security.SslPolicyErrors]::None
$script:cert = $null
$client = New-Object System.Net.Sockets.TcpClient
try {
    $client.ReceiveTimeout = 8000
    $client.SendTimeout = 8000
    $client.Connect($hostName, $port)
    $ssl = New-Object System.Net.Security.SslStream(
        $client.GetStream(),
        $false,
        {
            param($sender, $certificate, $chain, $errors)
            $script:tlsErrors = $errors
            $script:cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $certificate
            return $true
        })
    $ssl.AuthenticateAsClient($hostName)
    $ssl.Dispose()
} catch {
    Write-Host "TLS handshake failed: $($_.Exception.Message)"
    if ($_.Exception.InnerException) {
        Write-Host $_.Exception.InnerException.Message
    }
} finally {
    $client.Dispose()
}

if ($script:cert) {
    Write-Host "Certificate: $($script:cert.Subject)"
    Write-Host "Issued by:   $($script:cert.Issuer)"
    Write-Host "Not after:   $($script:cert.NotAfter)"
}
Write-Host "Policy:      $script:tlsErrors"

Write-Host ""
if ($script:tlsErrors -eq [System.Net.Security.SslPolicyErrors]::None) {
    Write-Host "Windows trusts this certificate from $env:COMPUTERNAME. If the dashboard still fails, recycle XProtectDashboard and press Ctrl+F5."
    exit 0
}

if ($script:tlsErrors.ToString() -match "RemoteCertificateNameMismatch") {
    Write-Host "The certificate name does not match $hostName. Use the name on the certificate as the Gateway URL."
    exit 1
}

Write-Host "FOXAWSMSAP076 does not trust the issuer of $hostName."
Write-Host "That is expected on a new IIS server that never received the Fox internal CA."
Write-Host "Fix: import the same corporate root/intermediate CA that FOX2208553 has into Local Computer > Trusted Root Certification Authorities on $env:COMPUTERNAME, then iisreset."
Write-Host "Temporary lab only: on Connect to XProtect, check Bypass TLS certificate errors, Test login, Save, recycle XProtectDashboard, Ctrl+F5."
Write-Host "Do not paste the XProtect password into chat."
exit 1
