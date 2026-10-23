$ErrorActionPreference = 'Stop'

try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $dll = Join-Path $PSScriptRoot 'usbTree.dll'
    if (-not (Test-Path -LiteralPath $dll)) {
        throw "找不到 usbTree.dll，请先运行 build.ps1 生成它。"
    }

    [Reflection.Assembly]::LoadFrom($dll) | Out-Null

    if ($args.Count -gt 0) {
        exit [UsbTreeLauncher]::RunCli([string[]]$args)
    }

    [UsbTreeLauncher]::RunGui()
} catch {
    if ($args.Count -gt 0) {
        throw
    }

    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show(
        $_.Exception.ToString(),
        'usbTree 启动失败',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    exit 1
}
