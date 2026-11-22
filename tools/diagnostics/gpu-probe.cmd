@echo off
setlocal
rem Run the probe even when PowerShell script execution is disabled for this user.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0gpu-probe.ps1" %*
