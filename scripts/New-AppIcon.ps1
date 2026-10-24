param(
    [string]$SourcePath = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$assetDirectory = Join-Path $root "src\MigraWin\Assets"
$documentationDirectory = Join-Path $root "docs\images"
if (-not $SourcePath) {
    $SourcePath = Join-Path $assetDirectory "MigraWinSource.png"
}

$sourcePath = (Resolve-Path -LiteralPath $SourcePath).Path
$supplied = [System.Drawing.Bitmap]::new($sourcePath)
if ($supplied.Width -ne $supplied.Height) {
    throw "L'icona sorgente deve essere quadrata."
}

New-Item -ItemType Directory -Force -Path $assetDirectory, $documentationDirectory | Out-Null
[IO.File]::Copy($sourcePath, (Join-Path $assetDirectory "MigraWinSource.png"), $true)

$source = [System.Drawing.Bitmap]::new(
    $supplied.Width,
    $supplied.Height,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sourceGraphics = [System.Drawing.Graphics]::FromImage($source)
$sourceGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$sourceGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$sourceGraphics.Clear([System.Drawing.Color]::Transparent)
$tile = [System.Drawing.Drawing2D.GraphicsPath]::new()
$padding = [int]($supplied.Width * 0.025)
$radius = [int]($supplied.Width * 0.18)
$diameter = $radius * 2
$tile.AddArc($padding, $padding, $diameter, $diameter, 180, 90)
$tile.AddArc($supplied.Width - $padding - $diameter, $padding, $diameter, $diameter, 270, 90)
$tile.AddArc($supplied.Width - $padding - $diameter, $supplied.Height - $padding - $diameter, $diameter, $diameter, 0, 90)
$tile.AddArc($padding, $supplied.Height - $padding - $diameter, $diameter, $diameter, 90, 90)
$tile.CloseFigure()
$background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.PointF]::new(32, 24),
    [System.Drawing.PointF]::new(492, 496),
    [System.Drawing.Color]::FromArgb(255, 5, 18, 36),
    [System.Drawing.Color]::FromArgb(255, 8, 99, 118))
$sourceGraphics.FillPath($background, $tile)

$glow = [System.Drawing.Drawing2D.GraphicsPath]::new()
$glow.AddEllipse(184, -82, 430, 430)
$glowBrush = [System.Drawing.Drawing2D.PathGradientBrush]::new($glow)
$glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(145, 22, 119, 255)
$glowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 22, 119, 255))
$sourceGraphics.FillPath($glowBrush, $glow)

$glyph = [System.Drawing.Bitmap]::new(
    $supplied.Width,
    $supplied.Height,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $supplied.Height; $y++) {
    for ($x = 0; $x -lt $supplied.Width; $x++) {
        $alpha = $supplied.GetPixel($x, $y).A
        if ($alpha -gt 0) {
            $glyph.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, 239, 253, 255))
        }
    }
}

foreach ($point in @(@(114, 156), @(402, 156), @(254, 388))) {
    $nodeGlow = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $nodeGlow.AddEllipse($point[0] - 28, $point[1] - 28, 56, 56)
    $nodeBrush = [System.Drawing.Drawing2D.PathGradientBrush]::new($nodeGlow)
    $nodeBrush.CenterColor = [System.Drawing.Color]::FromArgb(135, 68, 225, 255)
    $nodeBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 68, 225, 255))
    $sourceGraphics.FillPath($nodeBrush, $nodeGlow)
    $nodeGlow.Dispose()
    $nodeBrush.Dispose()
}

$sourceGraphics.DrawImage($glyph, 56, 48, 400, 400)
$border = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(115, 126, 238, 255), 3)
$sourceGraphics.DrawPath($border, $tile)
$sourceGraphics.Dispose()
$glyph.Dispose()
$supplied.Dispose()
$tile.Dispose()
$background.Dispose()
$glow.Dispose()
$glowBrush.Dispose()
$border.Dispose()
$source.Save(
    (Join-Path $assetDirectory "MigraWinMark.png"),
    [System.Drawing.Imaging.ImageFormat]::Png)
$source.Save(
    (Join-Path $documentationDirectory "migrawin-icon.png"),
    [System.Drawing.Imaging.ImageFormat]::Png)

function Resize-Icon([int]$size) {
    $bitmap = [System.Drawing.Bitmap]::new(
        $size,
        $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.DrawImage($source, 0, 0, $size, $size)
    $graphics.Dispose()
    return $bitmap
}

function Get-PngBytes($bitmap) {
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return $stream.ToArray()
}

$frames = @()
foreach ($size in @(16, 20, 24, 32, 40, 48, 64, 128, 256)) {
    $image = Resize-Icon $size
    $pngBytes = Get-PngBytes $image
    if ($pngBytes.Length -eq 0) {
        throw "Frame ICO vuoto per $size px."
    }

    $frames += [pscustomobject]@{ Size = $size; Bytes = $pngBytes }
    $image.Dispose()
}

$iconPath = Join-Path $assetDirectory "MigraWin.ico"
$iconStream = [IO.File]::Create($iconPath)
$writer = [IO.BinaryWriter]::new($iconStream)
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
    $source.Dispose()
}

Get-Item $iconPath,
    (Join-Path $assetDirectory "MigraWinMark.png"),
    (Join-Path $documentationDirectory "migrawin-icon.png") |
    Select-Object FullName, Length
