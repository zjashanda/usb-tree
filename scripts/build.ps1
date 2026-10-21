$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$skillRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $skillRoot 'references\UsbSerialTreeViewer.cs'
$icon = Join-Path $skillRoot 'assets\usbTree.ico'
$output = Join-Path $PSScriptRoot 'usbTree.exe'
$cliOutput = Join-Path $PSScriptRoot 'usbTree-cli.exe'
if (-not (Test-Path $compiler)) { throw "找不到 .NET Framework C# 编译器: $compiler" }
if (-not (Test-Path $icon)) { throw "找不到图标文件: $icon" }
$commonArgs = @(
  '/nologo', '/utf8output', '/codepage:65001', '/platform:x64', '/optimize+',
  '/r:System.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll'
)
& $compiler @commonArgs /target:winexe /main:Program `
  /win32icon:$icon /out:$output $source
if ($LASTEXITCODE -ne 0) { throw "GUI 编译失败，退出码 $LASTEXITCODE" }
& $compiler @commonArgs /target:exe /main:CliProgram `
  /win32icon:$icon /out:$cliOutput $source
if ($LASTEXITCODE -ne 0) { throw "CLI 编译失败，退出码 $LASTEXITCODE" }
Write-Output "已生成: $output"
Write-Output "已生成: $cliOutput"
