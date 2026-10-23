$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$dll = Join-Path $PSScriptRoot 'usbTree.dll'
if (-not (Test-Path -LiteralPath $dll)) {
    throw "找不到 usbTree.dll，请先运行 build.ps1 生成它。"
}

[Reflection.Assembly]::LoadFrom($dll) | Out-Null
exit [UsbTreeLauncher]::RunCli([string[]]$args)
