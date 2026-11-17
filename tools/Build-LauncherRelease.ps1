param(
    [string]$KeyDirectory="$env:USERPROFILE\.android\launcher-release",
    [string]$JavaDirectory='C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot',
    [string]$AndroidBuildTools='C:\Program Files (x86)\Android\android-sdk\build-tools\37.0.0',
    [string]$OutputDirectory='artifacts\release-1.5'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=[IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
New-Item -ItemType Directory -Path $output -Force | Out-Null
function Get-SourceIdentity {
    @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | Sort-Object FullName | ForEach-Object { @{path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} }) | ConvertTo-Json -Depth 3 -Compress
}
$sourceBefore=Get-SourceIdentity
$rtk=(Get-Command rtk -ErrorAction SilentlyContinue).Source
if(!$rtk){$rtk='C:\Users\mrlog\AppData\Local\Microsoft\WinGet\Packages\rtk-ai.rtk_Microsoft.Winget.Source_8wekyb3d8bbwe\rtk.exe'}
& $rtk dotnet run --project (Join-Path $root 'tests\Launcher.Core.Tests') -c Release
if($LASTEXITCODE -ne 0){throw 'Core checks failed. No release was prepared.'}
$metadata=Get-Content -LiteralPath (Join-Path $KeyDirectory 'signing-metadata.json') -Raw | ConvertFrom-Json
$secure=Get-Content -LiteralPath (Join-Path $KeyDirectory 'store-password.dpapi') -Raw | ConvertTo-SecureString
$credential=[System.Net.NetworkCredential]::new('', $secure)
$env:LAUNCHER_RELEASE_STOREPASS=$credential.Password
$env:LAUNCHER_LEGACY_STOREPASS='android'
$previousJava=$env:JAVA_HOME;$env:JAVA_HOME=$JavaDirectory
try {
    foreach($release in @(@{Name='Launcher-bridge-1.5.0.apk';Version='1.5.0';Build=26;Rotate=$false},@{Name='Launcher-1.5.1.apk';Version='1.5.1';Build=27;Rotate=$true})){
        Write-Output ('Building '+$release.Version+' ('+$release.Build+')')
        & $rtk dotnet build (Join-Path $root 'src\Launcher\Launcher.csproj') -c Release -warnaserror "-p:JavaSdkDirectory=$JavaDirectory" "-p:ApplicationDisplayVersion=$($release.Version)" "-p:ApplicationVersion=$($release.Build)"
        if($LASTEXITCODE -ne 0){throw 'Release compilation failed.'}
        $source=Join-Path $root 'src\Launcher\bin\Release\net11.0-android\com.launcher.home.apk'
        $destination=Join-Path $output $release.Name
        $arguments=@('sign','--ks',(Join-Path $KeyDirectory 'launcher-legacy.keystore'),'--ks-key-alias','androiddebugkey','--ks-pass','env:LAUNCHER_LEGACY_STOREPASS','--key-pass','env:LAUNCHER_LEGACY_STOREPASS')
        if($release.Rotate){$arguments+=@('--next-signer','--ks',(Join-Path $KeyDirectory 'launcher-release.p12'),'--ks-key-alias','launcher-release','--ks-pass','env:LAUNCHER_RELEASE_STOREPASS','--key-pass','env:LAUNCHER_RELEASE_STOREPASS','--lineage',(Join-Path $KeyDirectory 'launcher-signing.lineage'),'--rotation-min-sdk-version','28')}
        $arguments+=@('--out',$destination,$source)
        & (Join-Path $AndroidBuildTools 'apksigner.bat') @arguments
        if($LASTEXITCODE -ne 0){throw 'APK signing failed.'}
        $verification=& (Join-Path $AndroidBuildTools 'apksigner.bat') verify --verbose --print-certs $destination
        if($LASTEXITCODE -ne 0){throw 'APK signature verification failed.'}
        $verification | Set-Content -LiteralPath ($destination+'.signing.txt')
        $expected=if($release.Rotate){$metadata.certificateSha256}else{$metadata.legacyCertificateSha256}
        if((($verification -join "`n").ToUpperInvariant()) -notmatch $expected){throw 'The APK signer does not match the expected identity.'}
        & (Join-Path $AndroidBuildTools 'zipalign.exe') -c -P 16 4 $destination
        if($LASTEXITCODE -ne 0){throw 'APK alignment verification failed.'}
        @{version=$release.Version;build=$release.Build;bytes=(Get-Item -LiteralPath $destination).Length;sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash;signerSha256=$expected;rotated=$release.Rotate;minimumSdk=24;abis=@('arm64-v8a','x86_64')} | ConvertTo-Json | Set-Content -LiteralPath ($destination+'.json')
        Write-Output ('Verified '+$destination)
    }
} finally {
    Remove-Item Env:LAUNCHER_RELEASE_STOREPASS -ErrorAction SilentlyContinue
    Remove-Item Env:LAUNCHER_LEGACY_STOREPASS -ErrorAction SilentlyContinue
    $env:JAVA_HOME=$previousJava
}
Write-Output 'Prepared bridge and rotated APKs. Publication is a separate action.'
if((Get-SourceIdentity) -cne $sourceBefore){throw 'Source changed during the release build. Rebuild before distributing either APK.'}
$sourceBefore | Set-Content -LiteralPath (Join-Path $output 'source-sha256.json')
@{android=@{version='1.5.0';build=26;sha256=(Get-FileHash (Join-Path $output 'Launcher-bridge-1.5.0.apk') -Algorithm SHA256).Hash;bytes=(Get-Item (Join-Path $output 'Launcher-bridge-1.5.0.apk')).Length;url='https://kevlog.co.za/downloads/projects/launcher/BRIDGE_FILE_ID/'};androidProduction=@{version='1.5.1';build=27;sha256=(Get-FileHash (Join-Path $output 'Launcher-1.5.1.apk') -Algorithm SHA256).Hash;bytes=(Get-Item (Join-Path $output 'Launcher-1.5.1.apk')).Length;url='https://kevlog.co.za/downloads/projects/launcher/PRODUCTION_FILE_ID/'}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'launcher-manifest.template.json')
