<#
.SYNOPSIS
Read-only Android SystemUI inventory. Does not change settings or grant access.
.EXAMPLE
pwsh -File .\tools\Audit-AndroidSystemUi.ps1 -Serial YOUR_ADB_SERIAL
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9._:-]+$')][string]$Serial,
    [string]$AdbPath,
    [string]$OutputPath
)
$ErrorActionPreference='Stop'
if (-not $AdbPath) {
    $AndroidAuditAdbCommand=Get-Command adb -ErrorAction SilentlyContinue
    if ($AndroidAuditAdbCommand) { $AdbPath=$AndroidAuditAdbCommand.Source }
    else { $AdbPath='C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe' }
}
if (-not (Test-Path -LiteralPath $AdbPath -PathType Leaf)) { throw 'ADB was not found. Pass -AdbPath with its full path.' }
if (-not $OutputPath) {
    $AndroidAuditRoot=Split-Path -Parent $PSScriptRoot
    $AndroidAuditName=$Serial -replace '[^A-Za-z0-9_-]','_'
    $OutputPath=Join-Path $AndroidAuditRoot ('artifacts/system-ui-audit/'+$AndroidAuditName+'.json')
}
function Read-AndroidAuditValue {
    param([string[]]$Arguments)
    $AndroidAuditLines=& $AdbPath -s $Serial @Arguments 2>&1
    $AndroidAuditCode=$LASTEXITCODE
    [ordered]@{ exitCode=$AndroidAuditCode; value=($AndroidAuditLines -join "`n").Trim() }
}
$AndroidAuditConnection=Read-AndroidAuditValue @('get-state')
if ($AndroidAuditConnection.exitCode -ne 0 -or $AndroidAuditConnection.value -ne 'device') {
    throw 'The selected device is not connected and authorised. Run adb devices, unlock the device and approve USB debugging.'
}
$AndroidAuditProperties=[ordered]@{}
foreach ($AndroidAuditProperty in @('ro.product.manufacturer','ro.product.model','ro.build.version.release','ro.build.version.sdk','ro.build.version.oneui','ro.build.version.security_patch','ro.build.display.id')) {
    $AndroidAuditProperties[$AndroidAuditProperty]=Read-AndroidAuditValue @('shell','getprop',$AndroidAuditProperty)
}
$AndroidAuditSettings=[ordered]@{}
foreach ($AndroidAuditEntry in @(
    @('secure','icon_blacklist'),
    @('secure','sysui_qs_tiles'),
    @('secure','theme_customization_overlay_packages'),
    @('system','status_bar_show_battery_percent'),
    @('system','display_battery_percentage'),
    @('system','haptic_feedback_enabled'),
    @('global','policy_control'),
    @('global','window_animation_scale'),
    @('global','transition_animation_scale'),
    @('global','animator_duration_scale')
)) {
    $AndroidAuditKey=$AndroidAuditEntry[0]+'.'+$AndroidAuditEntry[1]
    $AndroidAuditSettings[$AndroidAuditKey]=Read-AndroidAuditValue @('shell','settings','get',$AndroidAuditEntry[0],$AndroidAuditEntry[1])
}
$AndroidAuditPackage=Read-AndroidAuditValue @('shell','dumpsys','package','com.android.systemui')
$AndroidAuditPackageLines=@($AndroidAuditPackage.value -split "`n" | Where-Object { $_ -match '^\s*(versionCode=|versionName=)' })
$AndroidAuditReport=[ordered]@{
    generatedAt=[DateTime]::UtcNow.ToString('o')
    readOnly=$true
    properties=$AndroidAuditProperties
    settings=$AndroidAuditSettings
    systemUiVersion=$AndroidAuditPackageLines
    installedOverlays=(Read-AndroidAuditValue @('shell','cmd','overlay','list','--user','current'))
    statusBarCommandHelp=(Read-AndroidAuditValue @('shell','cmd','statusbar','help'))
    nightMode=(Read-AndroidAuditValue @('shell','cmd','uimode','night'))
    shellIdentity=(Read-AndroidAuditValue @('shell','id'))
    verifiedVisualChanges=@()
    note='A readable setting or installed overlay does not prove Samsung honours changes to it. No setting was written, no permission granted, no overlay enabled and no privileged API bypass attempted. Root availability was not probed.'
}
$AndroidAuditParent=Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))
[IO.Directory]::CreateDirectory($AndroidAuditParent) | Out-Null
$AndroidAuditReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output ('Saved read-only SystemUI audit: '+[IO.Path]::GetFullPath($OutputPath))
