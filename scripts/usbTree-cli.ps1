$ErrorActionPreference = 'Stop'

if (-not [Environment]::Is64BitProcess -and [Environment]::Is64BitOperatingSystem) {
    $nativePowerShell = Join-Path $env:WINDIR 'Sysnative\WindowsPowerShell\v1.0\powershell.exe'
    if (Test-Path -LiteralPath $nativePowerShell) {
        & $nativePowerShell -NoProfile -ExecutionPolicy Bypass -STA -File $PSCommandPath @args
        exit $LASTEXITCODE
    }
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$dll = Join-Path $PSScriptRoot 'usbTree.dll'
if (-not (Test-Path -LiteralPath $dll)) {
    throw "找不到 usbTree.dll，请先运行 build.ps1 生成它。"
}

[Reflection.Assembly]::LoadFrom($dll) | Out-Null
exit [UsbTreeLauncher]::RunCli([string[]]$args)
