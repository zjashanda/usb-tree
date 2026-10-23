@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0usbTree-cli.ps1" %*
exit /b %ERRORLEVEL%
