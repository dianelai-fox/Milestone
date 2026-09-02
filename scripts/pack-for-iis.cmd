@echo off
REM Works from C:\Windows\system32 when you Run as Administrator.
cd /d "%~dp0\.."
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -ExecutionPolicy Bypass -File "%~dp0pack-for-iis.ps1" %*
