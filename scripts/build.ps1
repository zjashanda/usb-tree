$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$skillRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $skillRoot 'references\UsbSerialTreeViewer.cs'
$icon = Join-Path $skillRoot 'assets\usbTree.ico'
$output = Join-Path $PSScriptRoot 'usbTree.exe'
if (-not (Test-Path $compiler)) { throw "找不到 .NET Framework C# 编译器: $compiler" }
if (-not (Test-Path $icon)) { throw "找不到图标文件: $icon" }
& $compiler /nologo /utf8output /target:winexe /platform:x64 /optimize+ `
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
  /win32icon:$icon /out:$output $source
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }
Write-Output "已生成: $output"
