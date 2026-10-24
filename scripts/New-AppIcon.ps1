$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$assetDirectory = Join-Path $root "src\MigraWin\Assets"
$documentationDirectory = Join-Path $root "docs\images"
New-Item -ItemType Directory -Force -Path $assetDirectory, $documentationDirectory | Out-Null

function New-RoundedPath([float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-MigraWinArtwork([int]$size) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $scale = $size / 1024.0
    $graphics.ScaleTransform($scale, $scale)
    $background = New-RoundedPath 38 38 948 948 196
    $gradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(80, 70),
        [System.Drawing.PointF]::new(930, 960),
        [System.Drawing.Color]::FromArgb(255, 6, 22, 37),
        [System.Drawing.Color]::FromArgb(255, 6, 92, 105))
    $graphics.FillPath($gradient, $background)

    $glow = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $glow.AddEllipse(475, -170, 760, 760)
    $glowBrush = [System.Drawing.Drawing2D.PathGradientBrush]::new($glow)
    $glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(135, 37, 99, 235)
    $glowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 37, 99, 235))
    $graphics.FillPath($glowBrush, $glow)

    $portalPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 202, 249, 255), 82)
    $portalPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $portalPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($portalPen, 150, 162, 500, 700, 42, 276)

    $arrowPen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 68)
    $arrowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $arrowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($arrowPen, 285, 512, 680, 512)

    $arrowHead = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $arrowHead.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(770, 512),
        [System.Drawing.PointF]::new(632, 414),
        [System.Drawing.PointF]::new(632, 610)
    ))
    $graphics.FillPath([System.Drawing.Brushes]::White, $arrowHead)

    $graphics.Dispose()
    return $bitmap
}

function Get-PngBytes($bitmap) {
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return $stream.ToArray()
}

$master = New-MigraWinArtwork 1024
$markPath = Join-Path $assetDirectory "MigraWinMark.png"
$documentationPath = Join-Path $documentationDirectory "migrawin-icon.png"
$master.Save($markPath, [System.Drawing.Imaging.ImageFormat]::Png)
$master.Save($documentationPath, [System.Drawing.Imaging.ImageFormat]::Png)
$master.Dispose()

$frames = @()
foreach ($size in @(16, 20, 24, 32, 40, 48, 64, 128, 256)) {
    $image = New-MigraWinArtwork $size
    $pngBytes = Get-PngBytes $image
    if ($pngBytes.Length -eq 0) {
        throw "Frame ICO vuoto per $size px."
    }
    $frames += [pscustomobject]@{ Size = $size; Bytes = $pngBytes }
    $image.Dispose()
}

$iconPath = Join-Path $assetDirectory "MigraWin.ico"
$iconStream = [System.IO.File]::Create($iconPath)
$writer = [System.IO.BinaryWriter]::new($iconStream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)
    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $frameBytes = [byte[]]$frame.Bytes
        $writer.Write($frameBytes, 0, $frameBytes.Length)
    }
}
finally {
    $writer.Dispose()
    $iconStream.Dispose()
}

Get-Item $iconPath, $markPath, $documentationPath |
    Select-Object FullName, Length
