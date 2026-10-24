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

    $backCard = New-RoundedPath 145 210 320 470 88
    $backFill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(72, 148, 233, 247))
    $backBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(205, 173, 240, 250), 14)
    $graphics.FillPath($backFill, $backCard)
    $graphics.DrawPath($backBorder, $backCard)

    $shadow = New-RoundedPath 414 295 360 490 92
    $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(85, 0, 0, 0))
    $graphics.FillPath($shadowBrush, $shadow)

    $frontCard = New-RoundedPath 382 250 354 490 92
    $frontGradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(390, 250),
        [System.Drawing.PointF]::new(740, 740),
        [System.Drawing.Color]::FromArgb(255, 17, 156, 235),
        [System.Drawing.Color]::FromArgb(255, 45, 212, 191))
    $frontBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(225, 224, 250, 255), 12)
    $graphics.FillPath($frontGradient, $frontCard)
    $graphics.DrawPath($frontBorder, $frontCard)

    $highlight = New-RoundedPath 414 282 290 92 42
    $highlightBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(58, 255, 255, 255))
    $graphics.FillPath($highlightBrush, $highlight)

    $route = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $route.StartFigure()
    $route.AddBezier(132, 672, 252, 670, 214, 438, 338, 438)
    $route.AddBezier(338, 438, 450, 438, 464, 386, 530, 350)
    $routePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 230, 250, 255), 48)
    $routePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $routePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawPath($routePen, $route)

    $arrowHead = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $arrowHead.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(492, 294),
        [System.Drawing.PointF]::new(640, 290),
        [System.Drawing.PointF]::new(560, 420)
    ))
    $graphics.FillPath([System.Drawing.Brushes]::White, $arrowHead)

    $badge = New-RoundedPath 674 625 168 168 54
    $badgeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 6, 39, 57))
    $graphics.FillPath($badgeBrush, $badge)
    $check = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 110, 231, 183), 28)
    $check.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $check.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLines($check, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(715, 708),
        [System.Drawing.PointF]::new(746, 738),
        [System.Drawing.PointF]::new(804, 674)
    ))

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
