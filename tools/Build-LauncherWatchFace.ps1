param(
    [Parameter(Mandatory)][string] $AndroidSdk,
    [Parameter(Mandatory)][string] $JavaSdk,
    [Parameter(Mandatory)][string] $Validator,
    [Parameter(Mandatory)][string] $KeyStore,
    [Parameter(Mandatory)][string] $KeyAlias,
    [string] $BuildToolsVersion = '37.0.0',
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/watch-face')
)

$ErrorActionPreference = 'Stop'
if (-not $env:LAUNCHER_WATCHFACE_STOREPASS) { throw 'Set LAUNCHER_WATCHFACE_STOREPASS for the watch face keystore.' }

function Invoke-BuildTool {
    param([string] $Executable, [string[]] $Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable exited with code $LASTEXITCODE." }
}

$source = Join-Path $PSScriptRoot '../src/Launcher.WatchFace'
$manifest = Join-Path $source 'AndroidManifest.xml'
$manifestXml = [xml](Get-Content -LiteralPath $manifest -Raw)
$version = $manifestXml.manifest.GetAttribute('android:versionName')
$build = [long]$manifestXml.manifest.GetAttribute('android:versionCode')
$buildTools = Join-Path $AndroidSdk "build-tools/$BuildToolsVersion"
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("launcher-roll-" + [guid]::NewGuid().ToString('N'))
$previousJava = $env:JAVA_HOME
New-Item -ItemType Directory -Path $work, $OutputDirectory -Force | Out-Null
try {
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
    foreach ($name in 'res.zip', 'unsigned.apk', 'aligned.apk', 'Launcher-Roll.apk', 'Launcher-Roll.apk.idsig') {
        $file = Join-Path $work $name
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $work
}
