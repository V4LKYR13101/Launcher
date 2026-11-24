param(
    [string] $AndroidSdk,
    [string] $JavaSdk,
    [string] $Validator,
    [string] $KeyStore,
    [string] $KeyAlias = 'launcher-watch-face',
    [string] $BuildToolsVersion = '37.0.0',
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/watch-face')
)

$ErrorActionPreference = 'Stop'

function Invoke-BuildTool {
    param([string] $Executable, [string[]] $Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable exited with code $LASTEXITCODE." }
}

if (-not $AndroidSdk) {
    $AndroidSdk = @(
        "${env:ProgramFiles(x86)}/Android/android-sdk"
        $env:ANDROID_HOME
        $env:ANDROID_SDK_ROOT
    ) | Where-Object {
        $_ -and (Test-Path -LiteralPath "$_/platforms/android-36/android.jar") -and
        (Test-Path -LiteralPath "$_/build-tools/$BuildToolsVersion/aapt2.exe")
    } | Select-Object -First 1
}
if (-not $AndroidSdk) { throw 'Android SDK with platform 36 and the selected build tools was not found. Pass -AndroidSdk.' }

if (-not $JavaSdk) {
    $JavaSdk = @(
        "$env:ProgramFiles/Eclipse Adoptium/jdk-21.0.12.101-hotspot"
        $env:JAVA_HOME
    ) | Where-Object {
        $_ -and (Test-Path -LiteralPath "$_/bin/javac.exe")
    } | Select-Object -First 1
}
if (-not $JavaSdk) { throw 'Java SDK was not found. Pass -JavaSdk with a JDK 21 installation.' }

$keyDirectory = Join-Path $env:USERPROFILE '.android/launcher-release'
$useLocalKey = -not $KeyStore
if ($useLocalKey) { $KeyStore = Join-Path $keyDirectory 'launcher-watch-face.p12' }
if (-not (Test-Path -LiteralPath $KeyStore -PathType Leaf)) {
    throw "Watch face signing key not found at $KeyStore."
}

$downloadValidator = -not $Validator
if ($downloadValidator) {
    $Validator = Join-Path ([IO.Path]::GetTempPath()) 'launcher-watch-tools/validator.jar'
}

$source = Join-Path $PSScriptRoot '../src/Launcher.WatchFace'
$manifest = Join-Path $source 'AndroidManifest.xml'
$manifestXml = [xml](Get-Content -LiteralPath $manifest -Raw)
$version = $manifestXml.manifest.GetAttribute('android:versionName')
$build = [long]$manifestXml.manifest.GetAttribute('android:versionCode')
$buildTools = Join-Path $AndroidSdk "build-tools/$BuildToolsVersion"
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("launcher-roll-" + [guid]::NewGuid().ToString('N'))
$previousJava = $env:JAVA_HOME
$previousStorePassword = $env:LAUNCHER_WATCHFACE_STOREPASS
New-Item -ItemType Directory -Path $work, $OutputDirectory -Force | Out-Null
try {
    if (-not $env:LAUNCHER_WATCHFACE_STOREPASS) {
        if (-not $useLocalKey) { throw 'Set LAUNCHER_WATCHFACE_STOREPASS when using a custom watch face keystore.' }
        $passwordPath = Join-Path $keyDirectory 'store-password.dpapi'
        if (-not (Test-Path -LiteralPath $passwordPath -PathType Leaf)) {
            throw 'The local signing password was not found. Set LAUNCHER_WATCHFACE_STOREPASS.'
        }
        $securePassword = Get-Content -LiteralPath $passwordPath -Raw | ConvertTo-SecureString
        $env:LAUNCHER_WATCHFACE_STOREPASS = [Net.NetworkCredential]::new('', $securePassword).Password
    }
    if (-not (Test-Path -LiteralPath $Validator -PathType Leaf)) {
        if (-not $downloadValidator) { throw "Watch Face Push validator not found at $Validator." }
        $download = Join-Path $work 'validator.jar'
        $validatorVersion = '1.1.0-alpha01'
        $maven = 'https://dl.google.com/dl/android/maven2/com/google/android/wearable/watchface/validator'
        Invoke-BuildTool 'curl.exe' @('--fail', '--silent', '--show-error', '--location',
            '--output', $download, "$maven/validator-push-cli/$validatorVersion/validator-push-cli-$validatorVersion.jar")
        New-Item -ItemType Directory -Path (Split-Path -Parent $Validator) -Force | Out-Null
        Move-Item -LiteralPath $download -Destination $Validator -Force
    }

    $env:JAVA_HOME = $JavaSdk
    $resources = Join-Path $work 'res.zip'
    $unsigned = Join-Path $work 'unsigned.apk'
    $aligned = Join-Path $work 'aligned.apk'
    $signed = Join-Path $work 'Launcher-Roll.apk'
    Invoke-BuildTool "$buildTools/aapt2.exe" @('compile', '--dir', "$source/res", '-o', $resources)
    Invoke-BuildTool "$buildTools/aapt2.exe" @('link', '-I', "$AndroidSdk/platforms/android-36/android.jar",
        '--manifest', $manifest, '-o', $unsigned, $resources)
    Invoke-BuildTool "$buildTools/zipalign.exe" @('-f', '4', $unsigned, $aligned)
    Invoke-BuildTool "$buildTools/apksigner.bat" @('sign', '--ks', $KeyStore, '--ks-key-alias', $KeyAlias,
        '--ks-pass', 'env:LAUNCHER_WATCHFACE_STOREPASS', '--out', $signed, $aligned)
    Invoke-BuildTool "$buildTools/apksigner.bat" @('verify', $signed)

    $validation = Invoke-BuildTool "$JavaSdk/bin/java.exe" @('-jar', $Validator, "--apk_path=$signed", '--package_name=com.launcher.home')
    $validationText = $validation -join [Environment]::NewLine
    $tokenMatch = [regex]::Match($validationText, '(?im)\b(?:generated |validation )?token\s*:\s*(\S+)\s*$')
    if (-not $tokenMatch.Success) { throw "Watch Face Push did not issue a validation token.`n$validationText" }
    $token = $tokenMatch.Groups[1].Value
    $size = (Get-Item -LiteralPath $signed).Length
    if ($size + [Text.Encoding]::UTF8.GetByteCount($token) + 10 -gt 96KB) {
        throw 'The signed watch face exceeds the Launcher Data Layer request limit.'
    }

    $sha256 = (Get-FileHash -LiteralPath $signed -Algorithm SHA256).Hash.ToLowerInvariant()
    $downloadId = $sha256.Substring(0, 32)
    $artifact = Join-Path $OutputDirectory "Launcher-Roll-$version.apk"
    Copy-Item -LiteralPath $signed -Destination $artifact -Force
    [ordered]@{
        watchFace = [ordered]@{
            release = [ordered]@{
                version = $version
                build = $build
                url = "https://kevlog.co.za/downloads/projects/launcher/$downloadId/"
                sha256 = $sha256
                bytes = $size
            }
            validationToken = $token
        }
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'watch-face-feed-entry.json') -Encoding utf8
    Get-Item -LiteralPath $artifact
}
finally {
    $env:JAVA_HOME = $previousJava
    $env:LAUNCHER_WATCHFACE_STOREPASS = $previousStorePassword
    foreach ($name in 'res.zip', 'unsigned.apk', 'aligned.apk', 'Launcher-Roll.apk', 'Launcher-Roll.apk.idsig', 'validator.jar') {
        $file = Join-Path $work $name
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $work
}
