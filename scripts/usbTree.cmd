@echo off
setlocal
if "%~1"=="" (
  start "" powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0usbTree.ps1"
) else (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0usbTree.ps1" %*
)
exit /b %ERRORLEVEL%
