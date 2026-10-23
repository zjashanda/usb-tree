param(
    [string]$CertificateThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [string]$SignToolPath
)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$skillRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $skillRoot 'references\UsbSerialTreeViewer.cs'
$icon = Join-Path $skillRoot 'assets\usbTree.ico'
$output = Join-Path $PSScriptRoot 'usbTree.exe'
$cliOutput = Join-Path $PSScriptRoot 'usbTree-cli.exe'
$libraryOutput = Join-Path $PSScriptRoot 'usbTree.dll'
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
& $compiler @commonArgs /target:library /out:$libraryOutput $source
if ($LASTEXITCODE -ne 0) { throw "DLL 编译失败，退出码 $LASTEXITCODE" }

$artifacts = @($output, $cliOutput, $libraryOutput)
if ($CertificateThumbprint) {
    $signTool = $null
    if ($SignToolPath) {
        if (-not (Test-Path -LiteralPath $SignToolPath)) {
            throw "找不到 signtool: $SignToolPath"
        }
        $signTool = $SignToolPath
    } else {
        $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
        if ($command) {
            $signTool = $command.Source
        }
        if (-not $signTool) {
            $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
            if (Test-Path -LiteralPath $kitsRoot) {
                $candidate = Get-ChildItem -LiteralPath $kitsRoot -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
                    Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
                    Sort-Object FullName -Descending |
                    Select-Object -First 1
                if ($candidate) { $signTool = $candidate.FullName }
            }
        }
        if (-not $signTool) {
            throw "找不到 signtool.exe。请安装 Windows SDK，或用 -SignToolPath 指定路径。"
        }
    }

    foreach ($file in $artifacts) {
        & $signTool sign /sha1 $CertificateThumbprint /tr $TimestampUrl /td sha256 /fd sha256 $file
        if ($LASTEXITCODE -ne 0) { throw "签名失败: $file" }
    }
    Write-Output "已使用证书 $CertificateThumbprint 签名 exe 和 dll。"
} else {
    Write-Warning "未提供代码签名证书：usbTree.exe/usbTree-cli.exe 可能被 Windows 11 智能应用控制拦截。请优先使用 usbTree.cmd 或 usbTree-cli.cmd，或通过 -CertificateThumbprint 指定受信任的代码签名证书。"
}

$checksumFiles = @(
    'SKILL.md',
    'agents/openai.yaml',
    'references/UsbSerialTreeViewer.cs',
    'assets/usbTree.ico',
    'scripts/usbTree.exe',
    'scripts/usbTree-cli.exe',
    'scripts/usbTree.dll',
    'scripts/usbTree.ps1',
    'scripts/usbTree-cli.ps1',
    'scripts/usbTree.cmd',
    'scripts/usbTree-cli.cmd',
    'scripts/usbTree.vbs',
    'scripts/usbTree-linux.py',
    'scripts/usbTree-linux',
    'scripts/build.ps1',
    'scripts/make-icon.ps1'
)
$checksumLines = foreach ($relative in $checksumFiles) {
    $path = Join-Path $skillRoot $relative
    if (Test-Path -LiteralPath $path) {
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $relative"
    }
}
$checksumPath = Join-Path $skillRoot 'SHA256SUMS.txt'
[IO.File]::WriteAllText($checksumPath, (($checksumLines -join "`n") + "`n"),
    (New-Object Text.UTF8Encoding($false)))

Write-Output "已生成: $output"
Write-Output "已生成: $cliOutput"
Write-Output "已生成: $libraryOutput"
Write-Output "已生成: $checksumPath"
