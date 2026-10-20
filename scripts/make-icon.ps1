$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$skillRoot = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $skillRoot 'assets'
New-Item -Path $assets -ItemType Directory -Force | Out-Null
$path = Join-Path $assets 'usbTree.ico'
$bitmap = New-Object System.Drawing.Bitmap 64, 64
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(24, 38, 54))
$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$cyan = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(62, 201, 214))
$blue = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(62, 201, 214), 4)
$green = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(117, 220, 145))
$graphics.FillRectangle($cyan, 28, 9, 8, 30)
$graphics.FillRectangle($cyan, 19, 27, 26, 10)
$graphics.FillRectangle($cyan, 23, 37, 18, 9)
$graphics.DrawLine($blue, 32, 46, 32, 54)
$graphics.DrawLine($blue, 32, 51, 48, 51)
$graphics.DrawLine($blue, 48, 51, 48, 43)
$graphics.FillEllipse($green, 44, 39, 8, 8)
$graphics.DrawLine($blue, 32, 54, 18, 54)
$graphics.DrawLine($blue, 18, 54, 18, 47)
$graphics.FillEllipse($green, 14, 43, 8, 8)
$graphics.FillPolygon($white, [System.Drawing.Point[]]@(
    (New-Object System.Drawing.Point(32, 4)),
    (New-Object System.Drawing.Point(26, 12)),
    (New-Object System.Drawing.Point(38, 12))))
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Create)
try { $icon.Save($stream) } finally { $stream.Dispose(); $icon.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
Write-Output "已生成: $path"
