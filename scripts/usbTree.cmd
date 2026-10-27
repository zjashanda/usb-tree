@echo off
setlocal
set "POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "POWERSHELL=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
if "%~1"=="" (
  start "" "%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "%~dp0usbTree.ps1"
) else (
  "%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0usbTree.ps1" %*
)
exit /b %ERRORLEVEL%
