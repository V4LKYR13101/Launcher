param(
    [string]$KeyDirectory="$env:USERPROFILE\.android\launcher-release",
    [string]$JavaDirectory='C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot',
    [string]$AndroidBuildTools='C:\Program Files (x86)\Android\android-sdk\build-tools\37.0.0'
)
$ErrorActionPreference='Stop'
$key=Join-Path $KeyDirectory 'launcher-release.p12'
if(Test-Path -LiteralPath $key){throw 'A release key already exists. It has not been changed.'}
New-Item -ItemType Directory -Path $KeyDirectory -Force | Out-Null
$owner=[System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$acl=[System.Security.AccessControl.DirectorySecurity]::new()
$acl.SetOwner([System.Security.Principal.NTAccount]::new($owner))
$acl.SetAccessRuleProtection($true,$false)
$rule=[System.Security.AccessControl.FileSystemAccessRule]::new($owner,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
$acl.AddAccessRule($rule)
Set-Acl -LiteralPath $KeyDirectory -AclObject $acl
$env:LAUNCHER_RELEASE_STOREPASS=[Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$taskPreviousJava=$env:JAVA_HOME
$env:JAVA_HOME=$JavaDirectory
try {
    & (Join-Path $JavaDirectory 'bin\keytool.exe') -genkeypair -alias launcher-release -keyalg RSA -keysize 3072 -sigalg SHA256withRSA -validity 10000 -storetype PKCS12 -keystore $key -storepass:env LAUNCHER_RELEASE_STOREPASS -keypass:env LAUNCHER_RELEASE_STOREPASS -dname 'CN=Launcher Release, O=Kevlog, C=ZA' -noprompt
    if($LASTEXITCODE -ne 0){throw 'Release key generation failed.'}
    $secret=ConvertTo-SecureString $env:LAUNCHER_RELEASE_STOREPASS -AsPlainText -Force
    ConvertFrom-SecureString $secret | Set-Content -LiteralPath (Join-Path $KeyDirectory 'store-password.dpapi') -NoNewline
    [IO.File]::WriteAllText((Join-Path $KeyDirectory 'offline-recovery.txt'),"Launcher release signing recovery`nKeystore: launcher-release.p12`nAlias: launcher-release`nPassword: "+$env:LAUNCHER_RELEASE_STOREPASS+"`nKeep this folder in a private offline backup. It authorizes future Launcher releases.")
    $certificate=Join-Path $KeyDirectory 'launcher-release.der'
    & (Join-Path $JavaDirectory 'bin\keytool.exe') -exportcert -alias launcher-release -keystore $key -storepass:env LAUNCHER_RELEASE_STOREPASS -file $certificate
    if($LASTEXITCODE -ne 0){throw 'Certificate export failed.'}
    $fingerprint=(Get-FileHash -LiteralPath $certificate -Algorithm SHA256).Hash
    $legacyCandidates=@("$env:USERPROFILE\.android\debug.keystore","$env:LOCALAPPDATA\Xamarin\Mono for Android\debug.keystore")
    $expectedLegacy='4948630F9E81B77D80DE6846166A3D36BD27AB59279E494D176C029CB150DC7D'
    $env:LAUNCHER_LEGACY_STOREPASS='android'
    $legacy=$null
    foreach($candidate in $legacyCandidates){
        if(!(Test-Path -LiteralPath $candidate)){continue}
        $export=Join-Path $KeyDirectory 'legacy-candidate.der'
        & (Join-Path $JavaDirectory 'bin\keytool.exe') -exportcert -alias androiddebugkey -keystore $candidate -storepass:env LAUNCHER_LEGACY_STOREPASS -file $export 2>$null
        if($LASTEXITCODE -eq 0 -and (Get-FileHash -LiteralPath $export -Algorithm SHA256).Hash -eq $expectedLegacy){$legacy=$candidate;break}
    }
    if(!$legacy){throw 'The signing key used by the published installation could not be found. The new key is preserved; no migration lineage was created.'}
    $legacyCopy=Join-Path $KeyDirectory 'launcher-legacy.keystore'
    Copy-Item -LiteralPath $legacy -Destination $legacyCopy
    $lineage=Join-Path $KeyDirectory 'launcher-signing.lineage'
    & (Join-Path $AndroidBuildTools 'apksigner.bat') rotate --out $lineage --old-signer --ks $legacyCopy --ks-key-alias androiddebugkey --ks-pass env:LAUNCHER_LEGACY_STOREPASS --key-pass env:LAUNCHER_LEGACY_STOREPASS --new-signer --ks $key --ks-key-alias launcher-release --ks-pass env:LAUNCHER_RELEASE_STOREPASS --key-pass env:LAUNCHER_RELEASE_STOREPASS
    if($LASTEXITCODE -ne 0){throw 'Signing lineage creation failed. Existing keys are preserved.'}
    $metadata=@{certificateSha256=$fingerprint;legacyCertificateSha256=$expectedLegacy;alias='launcher-release';createdUtc=[DateTime]::UtcNow.ToString('o');rotationMinimumSdk=28;lineage='launcher-signing.lineage'}
    $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $KeyDirectory 'signing-metadata.json')
    $metadata | ConvertTo-Json
} finally {
    Remove-Item Env:LAUNCHER_RELEASE_STOREPASS -ErrorAction SilentlyContinue
    Remove-Item Env:LAUNCHER_LEGACY_STOREPASS -ErrorAction SilentlyContinue
    $env:JAVA_HOME=$taskPreviousJava
}
