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

$signingPfx = $env:MIGRAWIN_SIGN_PFX
$temporaryPfx = $null
if (-not $signingPfx -and $env:MIGRAWIN_SIGN_PFX_BASE64) {
    $temporaryRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { $env:TEMP }
    $temporaryPfx = Join-Path $temporaryRoot ("MigraWin-signing-" + [guid]::NewGuid().ToString("N") + ".pfx")
    [IO.File]::WriteAllBytes($temporaryPfx, [Convert]::FromBase64String($env:MIGRAWIN_SIGN_PFX_BASE64))
    $signingPfx = $temporaryPfx
}

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

if ($signingPfx -or $env:MIGRAWIN_SIGN_CERT_THUMBPRINT) {
    & (Join-Path $root "scripts\Sign-Artifacts.ps1") `
        -Files (Join-Path $publishDirectory "MigraWin.exe") `
        -CertificateThumbprint $env:MIGRAWIN_SIGN_CERT_THUMBPRINT `
        -PfxPath $signingPfx `
        -PfxPassword $env:MIGRAWIN_SIGN_PFX_PASSWORD
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

    $msi = Join-Path $installerDirectory "MigraWin-1.4.3-x64.msi"
    & $dotnet tool run wix build "installer\MigraWin.wxs" `
        -ext WixToolset.UI.wixext `
        -arch x64 `
        -o $msi | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Creazione dell'installer non riuscita."
    }

    if ($signingPfx -or $env:MIGRAWIN_SIGN_CERT_THUMBPRINT) {
        & (Join-Path $root "scripts\Sign-Artifacts.ps1") `
            -Files $msi `
            -CertificateThumbprint $env:MIGRAWIN_SIGN_CERT_THUMBPRINT `
            -PfxPath $signingPfx `
            -PfxPassword $env:MIGRAWIN_SIGN_PFX_PASSWORD
    }

    $hash = Get-FileHash -LiteralPath $msi -Algorithm SHA256
    "$($hash.Hash)  $([IO.Path]::GetFileName($msi))" |
        Set-Content -LiteralPath (Join-Path $installerDirectory "SHA256SUMS.txt") -Encoding ascii
    Get-Item -LiteralPath $msi | Select-Object FullName, Length, LastWriteTime
}
finally {
    Pop-Location
    if ($temporaryPfx -and (Test-Path -LiteralPath $temporaryPfx)) {
        Remove-Item -LiteralPath $temporaryPfx -Force
    }
}
