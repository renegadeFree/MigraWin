param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dotnet = Join-Path $root ".dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = "dotnet"
}

$publishDirectory = Join-Path $root "artifacts\publish"
$installerDirectory = Join-Path $root "artifacts\installer"
New-Item -ItemType Directory -Force -Path $publishDirectory, $installerDirectory | Out-Null

& $dotnet publish (Join-Path $root "src\MigraWin\MigraWin.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Publish non riuscito."
}

Push-Location $root
try {
    & $dotnet tool restore | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Ripristino degli strumenti non riuscito."
    }

    & $dotnet tool run wix extension add WixToolset.UI.wixext/6.0.2 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Installazione dell'estensione WiX non riuscita."
    }

    $msi = Join-Path $installerDirectory "MigraWin-1.4.0-x64.msi"
    & $dotnet tool run wix build "installer\MigraWin.wxs" `
        -ext WixToolset.UI.wixext `
        -arch x64 `
        -o $msi | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Creazione dell'installer non riuscita."
    }

    $hash = Get-FileHash -LiteralPath $msi -Algorithm SHA256
    "$($hash.Hash)  $([IO.Path]::GetFileName($msi))" |
        Set-Content -LiteralPath (Join-Path $installerDirectory "SHA256SUMS.txt") -Encoding ascii
    Get-Item -LiteralPath $msi | Select-Object FullName, Length, LastWriteTime
}
finally {
    Pop-Location
}
