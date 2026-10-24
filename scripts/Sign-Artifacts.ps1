param(
    [Parameter(Mandatory = $true)]
    [string[]]$Files,
    [string]$CertificateThumbprint,
    [string]$PfxPath,
    [string]$PfxPassword,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

$signTool = $env:SIGNTOOL_PATH
if (-not $signTool) {
    $kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    $signTool = Get-ChildItem -LiteralPath $kitsRoot -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $signTool -or -not (Test-Path -LiteralPath $signTool)) {
    throw "signtool.exe non trovato. Impostare SIGNTOOL_PATH o installare Windows SDK."
}

$importedCertificate = $null
try {
    if ($PfxPath) {
        if (-not (Test-Path -LiteralPath $PfxPath)) {
            throw "Certificato PFX non trovato: $PfxPath"
        }

        $securePassword = ConvertTo-SecureString $PfxPassword -AsPlainText -Force
        $importedCertificate = Import-PfxCertificate `
            -FilePath $PfxPath `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -Password $securePassword
        $CertificateThumbprint = $importedCertificate.Thumbprint
    }

    if (-not $CertificateThumbprint) {
        throw "Specificare CertificateThumbprint oppure PfxPath."
    }

    foreach ($file in $Files) {
        if (-not (Test-Path -LiteralPath $file)) {
            throw "Artefatto da firmare non trovato: $file"
        }

        & $signTool sign `
            /sha1 $CertificateThumbprint `
            /fd SHA256 `
            /tr $TimestampUrl `
            /td SHA256 `
            /a `
            $file
        if ($LASTEXITCODE -ne 0) {
            throw "Firma non riuscita: $file"
        }

        & $signTool verify /pa /v $file
        if ($LASTEXITCODE -ne 0) {
            throw "Verifica Authenticode non riuscita: $file"
        }
    }
}
finally {
    if ($importedCertificate) {
        Remove-Item -LiteralPath ("Cert:\CurrentUser\My\" + $importedCertificate.Thumbprint) -Force -ErrorAction SilentlyContinue
    }
}
