# Shared defaults for the two-machine workflow:
#   FOX2208553  = development PC (source: C:\Users\dianela\Milestone)
#   FOXAWSMSAP076 = IIS web server (site: C:\inetpub\xprotect-dashboard)

$IisDevComputer = "FOX2208553"
$IisWebComputer = "FOXAWSMSAP076"
$IisSitePath = "C:\inetpub\xprotect-dashboard"
$IisAppPoolName = "XProtectDashboard"
$IisWrongPoolName = "XProtect Dashboard"
$IisSiteName = "XProtect Dashboard"
$IisPort = 8080

function Test-SameComputer([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name)) { return $false }
    $here = $env:COMPUTERNAME
    $short = ($Name -split "\.")[0]
    return $here.Equals($Name, [StringComparison]::OrdinalIgnoreCase) -or
        $here.Equals($short, [StringComparison]::OrdinalIgnoreCase)
}

function Get-UncSitePath([string]$Computer, [string]$SitePath) {
    if (Test-SameComputer $Computer) {
        return [IO.Path]::GetFullPath($SitePath)
    }
    $root = $SitePath.TrimEnd('\')
    if ($root -match '^[A-Za-z]:\\') {
        return "\\$Computer\$($root[0])`$\$($root.Substring(3))"
    }
    throw "Site path must be a local drive path such as C:\inetpub\xprotect-dashboard."
}

function Test-IsAdministrator {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$id
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Assert-AspNetCoreHostingBundle {
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    $module = $null
    if (Get-Command Get-WebGlobalModule -ErrorAction SilentlyContinue) {
        $module = Get-WebGlobalModule -Name "AspNetCoreModuleV2" -ErrorAction SilentlyContinue
    }
    $ancm = Join-Path ${env:ProgramFiles} "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    if (-not $module -and -not (Test-Path $ancm)) {
        throw @"
IIS on $env:COMPUTERNAME does not have AspNetCoreModuleV2.
That is what causes HTTP 500.19 error 0x8007000d on web.config.

Install the .NET 8 Hosting Bundle on this machine, then iisreset:
https://dotnet.microsoft.com/download/dotnet/8.0
Under ASP.NET Core Runtime 8.0, download Hosting Bundle.

If the bundle was installed before IIS, repair the Hosting Bundle after IIS is present.
"@
    }
}

function Grant-DashboardAppData([string]$SitePath, [string]$AppPoolName) {
    $appData = Join-Path $SitePath "App_Data"
    $keys = Join-Path $appData "keys"
    $logs = Join-Path $SitePath "logs"
    New-Item -ItemType Directory -Force -Path $keys, $logs | Out-Null
    icacls $appData /grant "IIS AppPool\${AppPoolName}:(OI)(CI)M" /T | Out-Host
    icacls $logs /grant "IIS AppPool\${AppPoolName}:(OI)(CI)M" /T | Out-Host
}

function Save-LiveSettings([string]$SitePath) {
    $paths = @(
        (Join-Path $SitePath "appsettings.json"),
        (Join-Path $SitePath "appsettings.Production.json")
    )
    $saved = @()
    foreach ($path in $paths) {
        if (Test-Path $path) {
            $backup = "$path.publish-keep"
            Copy-Item $path $backup -Force
            $saved += $backup
            Write-Host "Keeping live settings $path"
        }
    }
    return $saved
}

function Restore-LiveSettings([string]$SitePath, $saved) {
    foreach ($backup in @($saved)) {
        $path = $backup -replace "\.publish-keep$", ""
        Copy-Item $backup $path -Force
        Remove-Item $backup -Force
        Write-Host "Restored live settings $path"
    }

    $settings = Join-Path $SitePath "appsettings.json"
    if (-not (Test-Path $settings)) {
        $template = Join-Path $SitePath "appsettings.template.json"
        if (Test-Path $template) {
            Copy-Item $template $settings
            Write-Host "Created $settings from the template. Set Milestone:UseDemoData to false for live XProtect."
        }
    }
}

function Get-IisScriptsPath([string]$ScriptName) {
    $root = Join-Path $IisSitePath "scripts"
    if ([string]::IsNullOrWhiteSpace($ScriptName)) {
        return $root
    }
    return Join-Path $root $ScriptName
}

function Get-DevScriptsUnc([string]$ScriptName) {
    return Get-IisScriptsPath $ScriptName
}

function Get-WindowsPowerShell {
    return Join-Path $env:WINDIR "System32\WindowsPowerShell\v1.0\powershell.exe"
}

function Test-PathQuick([string]$Path, [int]$Seconds = 4) {
    $job = Start-Job -ScriptBlock { Test-Path -LiteralPath $using:Path }
    if (-not (Wait-Job $job -Timeout $Seconds)) {
        Stop-Job $job -ErrorAction SilentlyContinue
        Remove-Job $job -Force -ErrorAction SilentlyContinue
        return $false
    }
    $result = Receive-Job $job
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    return [bool]$result
}

function Copy-ScriptsToIis([string]$RemoteComputer) {
    $source = $PSScriptRoot
    if (-not $source) {
        $source = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    $dest = Get-UncSitePath -Computer $RemoteComputer -SitePath (Get-IisScriptsPath)
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    & robocopy $source $dest /E /R:2 /W:2 /NFL /NDL /NJH | Out-Host
    if ($LASTEXITCODE -ge 8) {
        throw "Could not copy scripts to $dest (robocopy exit $LASTEXITCODE)."
    }
    Write-Host "Copied scripts to $dest"
    return $dest
}

function Get-RunOnIisHelp([string]$ScriptName) {
    $local = Get-IisScriptsPath $ScriptName
    $ps = Get-WindowsPowerShell
    return @"
Do not run scripts from \\$IisDevComputer\C$\Users\dianela\... on $IisWebComputer.
That path hangs for accounts such as sa-dlai (no access to Diane's profile share).

On FOX2208553, copy the scripts first:
  powershell -ExecutionPolicy Bypass -File C:\Users\dianela\Milestone\scripts\copy-scripts-to-iis.ps1

Then RDP to $IisWebComputer, open Windows PowerShell as Administrator (not PowerShell 7), and run:
  $ps -ExecutionPolicy Bypass -File $local
"@
}

function Invoke-OnComputer {
    param(
        [string]$Computer,
        [scriptblock]$ScriptBlock,
        [object[]]$ArgumentList = @()
    )
    if (Test-SameComputer $Computer) {
        return & $ScriptBlock @ArgumentList
    }
    try {
        return Invoke-Command -ComputerName $Computer -ScriptBlock $ScriptBlock -ArgumentList $ArgumentList -ErrorAction Stop
    } catch {
        throw "Cannot use PowerShell remoting to ${Computer}: $($_.Exception.Message)"
    }
}
