
$ErrorActionPreference = "Stop"

# Launcher release configuration
$version = "1.5.5"
$buildNumber = 32

$root = "E:\Repository\launcher"

$project = Join-Path $root "src\Launcher\Launcher.csproj"

$keyDirectory = Join-Path $env:USERPROFILE ".android\launcher-release"

$javaDirectory = "C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot"

$buildTools = "C:\Program Files (x86)\Android\android-sdk\build-tools\37.0.0"

$outputDirectory = Join-Path $root "artifacts\release-$version"

$unsignedApk = Join-Path $root "src\Launcher\bin\Release\net11.0-android\com.launcher.home.apk"

$alignedApk = Join-Path $outputDirectory "Launcher-aligned.apk"

$releaseApk = Join-Path $outputDirectory "Launcher-$version.apk"

$legacyKey = Join-Path $keyDirectory "launcher-legacy.keystore"

$releaseKey = Join-Path $keyDirectory "launcher-release.p12"

$lineage = Join-Path $keyDirectory "launcher-signing.lineage"

$apksigner = Join-Path $buildTools "apksigner.bat"

$zipalign = Join-Path $buildTools "zipalign.exe"

# Check required files
foreach ($file in @(
    $project,
    $legacyKey,
    $releaseKey,
    $lineage,
    $apksigner,
    $zipalign,
    (Join-Path $keyDirectory "store-password.dpapi"),
    (Join-Path $keyDirectory "signing-metadata.json")
)) {
    if (-not (Test-Path -LiteralPath $file)) {
        throw "Required file missing: $file"
    }
}

New-Item -ItemType Directory -Force -Path $outputDirectory |
    Out-Null

# Read production signing identity and password
$metadata = Get-Content `
    (Join-Path $keyDirectory "signing-metadata.json") `
    -Raw | ConvertFrom-Json

$securePassword = Get-Content `
    (Join-Path $keyDirectory "store-password.dpapi") `
    -Raw | ConvertTo-SecureString

$credential = [System.Net.NetworkCredential]::new(
    "",
    $securePassword
)

$previousJavaHome = $env:JAVA_HOME

try {
    $env:JAVA_HOME = $javaDirectory

    # Never put the production password directly
    # into the command-line arguments.
    $env:LAUNCHER_RELEASE_STOREPASS = $credential.Password

    # Existing legacy key password.
    $env:LAUNCHER_LEGACY_STOREPASS = "android"

    Write-Host "Building Launcher $version..." -ForegroundColor Cyan

    & dotnet build $project `
        -c Release `
        "-p:JavaSdkDirectory=$javaDirectory" `
        "-p:ApplicationDisplayVersion=$version" `
        "-p:ApplicationVersion=$buildNumber"

    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed."
    }

    if (-not (Test-Path -LiteralPath $unsignedApk)) {
        throw "Unsigned APK not found: $unsignedApk"
    }

    Write-Host "Aligning APK..." -ForegroundColor Cyan

    & $zipalign -f -P 16 4 $unsignedApk $alignedApk

    if ($LASTEXITCODE -ne 0) {
        throw "APK alignment failed."
    }

    Write-Host "Signing with production lineage..." -ForegroundColor Cyan

    & $apksigner sign `
        --ks $legacyKey `
        --ks-key-alias androiddebugkey `
        --ks-pass env:LAUNCHER_LEGACY_STOREPASS `
        --key-pass env:LAUNCHER_LEGACY_STOREPASS `
        --next-signer `
        --ks $releaseKey `
        --ks-key-alias launcher-release `
        --ks-pass env:LAUNCHER_RELEASE_STOREPASS `
        --key-pass env:LAUNCHER_RELEASE_STOREPASS `
        --lineage $lineage `
        --rotation-min-sdk-version 28 `
        --out $releaseApk `
        $alignedApk

    if ($LASTEXITCODE -ne 0) {
        throw "Production APK signing failed."
    }

    Write-Host "Verifying signing certificate..." -ForegroundColor Cyan

    $verification = & $apksigner verify `
        --verbose `
        --print-certs `
        $releaseApk

    if ($LASTEXITCODE -ne 0) {
        throw "APK signature verification failed."
    }

    $verification | Out-Host

    $expectedCertificate = $metadata.certificateSha256

    if (
        ($verification -join "`n").ToUpperInvariant() -notmatch
        $expectedCertificate.ToUpperInvariant()
    ) {
        throw "APK does not contain the expected production certificate."
    }

    & $zipalign -c -P 16 4 $releaseApk

    if ($LASTEXITCODE -ne 0) {
        throw "Signed APK alignment verification failed."
    }

    # Save metadata for GitHub / website updates
    $hash = (Get-FileHash $releaseApk -Algorithm SHA256).Hash

    $size = (Get-Item $releaseApk).Length

    $releaseInfo = @{
        version = $version
        build = $buildNumber
        file = "Launcher-$version.apk"
        bytes = $size
        sha256 = $hash
        minimumSdk = 24
    }

    $releaseInfo |
        ConvertTo-Json |
        Set-Content `
            (Join-Path $outputDirectory "release-info.json")

    Write-Host ""
    Write-Host "RELEASE READY" -ForegroundColor Green
    Write-Host "APK: $releaseApk"
    Write-Host "Version: $version"
    Write-Host "Build: $buildNumber"
    Write-Host "SHA256: $hash"
}
finally {
    Remove-Item Env:LAUNCHER_RELEASE_STOREPASS `
        -ErrorAction SilentlyContinue

    Remove-Item Env:LAUNCHER_LEGACY_STOREPASS `
        -ErrorAction SilentlyContinue

    $env:JAVA_HOME = $previousJavaHome
}
