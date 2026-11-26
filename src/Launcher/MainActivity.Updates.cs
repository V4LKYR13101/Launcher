using Android.Content;
using Android.Content.PM;
using Android.Views;
using Android.Widget;
using Launcher.Core;
using System.Security.Cryptography;

namespace LauncherApp;

public partial class MainActivity
{
    private readonly CancellationTokenSource updateLifetime = new();
    private TextView? updateStatusView, updateActionView, updateNotesView;
    private ProgressBar? updateProgressView;
    private string? updateStatus;
    private bool updateBusy, awaitingUpdatePermission;
    private int updatePercent;
    private AppRelease? availableUpdate;
    private string UpdatePath => Path.Combine(CacheDir!.AbsolutePath, "Launcher-update.apk");
    private long InstalledBuild => OperatingSystem.IsAndroidVersionAtLeast(28) ? InstalledPackage().LongVersionCode : InstalledPackage().VersionCode;
    private PackageInfo InstalledPackage() => PackageManager!.GetPackageInfo(PackageName!, PackageInfoFlags.Activities)!;

    private void RenderUpdates()
    {
        Title(Localize(Resource.String.ui_app_updates));
        Paragraph(Localize(Resource.String.ui_launcher_2) + InstalledPackage().VersionName + Localize(Resource.String.ui_build) + InstalledBuild);
        updateStatusView = Text(updateStatus ?? Localize(Resource.String.ui_check_kevlog_for_new_version), 15);
        updateStatusView.SetPadding(0, Dp(12), 0, Dp(20));
        body.AddView(updateStatusView, Match());
        updateProgressView = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleHorizontal)
        {
            Max = 100,
            Progress = updatePercent,
            Visibility = updateBusy ? ViewStates.Visible : ViewStates.Gone,
            ProgressTintList = Android.Content.Res.ColorStateList.ValueOf(palette.Accent)
        };
        body.AddView(updateProgressView, Match(Dp(5)));
        Gap(body, 16);
        updateActionView = Button(updateBusy ? Localize(Resource.String.ui_working) : Localize(Resource.String.ui_check_for_updates), () => _ = CheckForUpdatesAsync(), true);
        updateActionView.Enabled = !updateBusy;
        body.AddView(updateActionView, Match());
        updateNotesView = Button(Localize(Resource.String.ui_release_notes), () =>
        {
            if (AppUpdates.NotesUri(availableUpdate) is { } notes)
                OpenUrl(notes.AbsoluteUri);
        });
        UpdateNotesVisibility();
        body.AddView(updateNotesView, Match());
        Gap(body, 14);
        Paragraph(Localize(Resource.String.ui_updates_keep_your_drawers_settings_and_widgets_android_asks));
    }

    private void UpdateNotesVisibility()
    {
        if (updateNotesView is not null)
            updateNotesView.Visibility = AppUpdates.NotesUri(availableUpdate) is null ? ViewStates.Gone : ViewStates.Visible;
    }

    private void UpdateMessage(string message)
    {
        updateStatus = message;
        if (!IsDestroyed && page == "Updates" && updateStatusView is not null)
            updateStatusView.Text = message;
    }
    private void UpdateBusy(bool busy)
    {
        updateBusy = busy;
        if (page != "Updates")
            return;
        if (updateActionView is not null)
        {
            updateActionView.Enabled = !busy;
            updateActionView.Text = busy ? Localize(Resource.String.ui_working) : Localize(Resource.String.ui_check_for_updates);
        }
        if (updateProgressView is not null)
            updateProgressView.Visibility = busy ? ViewStates.Visible : ViewStates.Gone;
    }
    private async Task CheckForUpdatesAsync()
    {
        if (updateBusy)
            return;
        UpdateBusy(true);
        UpdateMessage(Localize(Resource.String.ui_checking_for_updates));
        try
        {
            availableUpdate = await AppUpdates.CheckAsync(updateLifetime.Token);
            if (IsDestroyed)
                return;
            UpdateNotesVisibility();
            if (availableUpdate.Build <= InstalledBuild)
            {
                UpdateMessage(Localize(Resource.String.ui_launcher_2) + InstalledPackage().VersionName + Localize(Resource.String.ui_is_up_to_date));
                return;
            }
            UpdateMessage(Localize(Resource.String.ui_version) + availableUpdate.Version + Localize(Resource.String.ui_is_available));
            var release = availableUpdate;
            Confirm(Localize(Resource.String.ui_launcher_update), Localize(Resource.String.ui_download_version_value_value_mb_your_layout_and_settings, release.Version, release.Bytes / 1048576d),
                Localize(Resource.String.ui_download_and_install), () => _ = DownloadUpdateAsync(release), AppUpdates.NotesUri(release));
        }
        catch (OperationCanceledException) { if (!IsDestroyed) UpdateMessage(Localize(Resource.String.ui_update_check_cancelled)); }
        catch (Exception e) when (e is HttpRequestException or IOException or System.Text.Json.JsonException)
        {
            UpdateMessage(Localize(Resource.String.ui_could_not_check_for_updates_check_your_connection_and));
        }
        finally { if (!IsDestroyed) UpdateBusy(false); }
    }
    private async Task DownloadUpdateAsync(AppRelease release)
    {
        if (updateBusy)
            return;
        UpdateBusy(true);
        updatePercent = 0;
        try
        {
            await AppUpdates.DownloadAsync(release, UpdatePath, percent => RunOnUiThread(() =>
            {
                if (IsDestroyed)
                    return;
                updatePercent = percent;
                UpdateMessage(Localize(Resource.String.ui_downloading_update_value, percent));
                if (page == "Updates" && updateProgressView is not null)
                    updateProgressView.Progress = percent;
            }), updateLifetime.Token);
            if (IsDestroyed)
                return;
            VerifyUpdate(release);
            if (OperatingSystem.IsAndroidVersionAtLeast(26) && !PackageManager!.CanRequestPackageInstalls())
            {
                awaitingUpdatePermission = true;
                UpdateMessage(Localize(Resource.String.ui_allow_launcher_to_install_updates_then_return_here));
                StartActivity(new Intent(Android.Provider.Settings.ActionManageUnknownAppSources, Android.Net.Uri.Parse("package:" + PackageName)));
            }
            else
                InstallUpdate();
        }
        catch (OperationCanceledException) { if (!IsDestroyed) UpdateMessage(Localize(Resource.String.ui_update_download_cancelled)); }
        catch (Exception e) when (e is HttpRequestException or IOException or Java.Lang.RuntimeException)
        {
            UpdateMessage(Localize(Resource.String.ui_could_not_install_the_update) + e.Message);
        }
        finally { if (!IsDestroyed) UpdateBusy(false); }
    }
    private void VerifyUpdate(AppRelease release)
    {
        var flags = OperatingSystem.IsAndroidVersionAtLeast(28) ? PackageInfoFlags.SigningCertificates : PackageInfoFlags.Signatures;
        var installed = PackageManager!.GetPackageInfo(PackageName!, flags)!;
        var archive = PackageManager.GetPackageArchiveInfo(UpdatePath, flags) ?? throw new IOException("The APK is invalid.");
        var left = OperatingSystem.IsAndroidVersionAtLeast(28) ? installed.SigningInfo?.GetApkContentsSigners() : installed.Signatures;
        var right = OperatingSystem.IsAndroidVersionAtLeast(28) ? archive.SigningInfo?.GetApkContentsSigners() : archive.Signatures;
        var history = OperatingSystem.IsAndroidVersionAtLeast(28) && archive.SigningInfo?.HasMultipleSigners == false ? archive.SigningInfo.GetSigningCertificateHistory() : null;
        var build = OperatingSystem.IsAndroidVersionAtLeast(28) ? archive.LongVersionCode : archive.VersionCode;
        if (archive.PackageName != PackageName || build != release.Build || archive.VersionName != release.Version ||
            left is not { Count: > 0 } || right is not { Count: > 0 } ||
            !SigningTrust.Accepts(left.Select(s => Convert.ToHexString(SHA256.HashData(s.ToByteArray()!))), right.Select(s => Convert.ToHexString(SHA256.HashData(s.ToByteArray()!))), history?.Select(s => Convert.ToHexString(SHA256.HashData(s.ToByteArray()!))) ?? []))
            throw new IOException("The update does not match this installation.");
    }
    private void ResumeUpdateInstall()
    {
        if (!awaitingUpdatePermission)
            return;
        awaitingUpdatePermission = false;
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && !PackageManager!.CanRequestPackageInstalls())
        {
            UpdateMessage(Localize(Resource.String.ui_installation_permission_was_not_granted_check_for_updates_to));
            return;
        }
        try
        {
            if (availableUpdate is { } release)
            {
                VerifyUpdate(release);
                InstallUpdate();
            }
        }
        catch (Exception e) when (e is IOException or Java.Lang.RuntimeException) { UpdateMessage(Localize(Resource.String.ui_could_not_open_the_update) + e.Message); }
    }
    private void InstallUpdate()
    {
        var uri = Android.Net.Uri.Parse("content://com.launcher.home.updates/Launcher-update.apk")!;
        using var intent = new Intent(Intent.ActionView);
        intent.SetDataAndType(uri, "application/vnd.android.package-archive");
        intent.ClipData = ClipData.NewRawUri("Launcher update", uri);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        StartActivity(intent);
        UpdateMessage(Localize(Resource.String.ui_complete_the_update_in_androids_installer));
    }
}
