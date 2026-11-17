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
    private TextView? updateStatusView, updateActionView;
    private ProgressBar? updateProgressView;
    private string updateStatus = "Check for a new version from kevlog.co.za.";
    private bool updateBusy, awaitingUpdatePermission;
    private int updatePercent;
    private AppRelease? availableUpdate;
    private string UpdatePath => Path.Combine(CacheDir!.AbsolutePath, "Launcher-update.apk");
    private long InstalledBuild => OperatingSystem.IsAndroidVersionAtLeast(28) ? InstalledPackage().LongVersionCode : InstalledPackage().VersionCode;
    private PackageInfo InstalledPackage() => PackageManager!.GetPackageInfo(PackageName!, PackageInfoFlags.Activities)!;

    private void RenderUpdates()
    {
        Title("App updates");
        Paragraph("Launcher " + InstalledPackage().VersionName + " · build " + InstalledBuild);
        updateStatusView = Text(updateStatus, 15); updateStatusView.SetPadding(0,Dp(12),0,Dp(20));
        body.AddView(updateStatusView, Match());
        updateProgressView = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleHorizontal)
            { Max=100, Progress=updatePercent, Visibility=updateBusy ? ViewStates.Visible : ViewStates.Gone,
              ProgressTintList=Android.Content.Res.ColorStateList.ValueOf(palette.Accent) };
        body.AddView(updateProgressView,Match(Dp(5))); Gap(body,16);
        updateActionView=Button(updateBusy?"Working…":"Check for updates",()=>_ = CheckForUpdatesAsync(),true);
        updateActionView.Enabled=!updateBusy;body.AddView(updateActionView,Match());
        Gap(body,14);Paragraph("Updates keep your drawers, settings and widgets. Android asks you to approve installation.");
    }

    private void UpdateMessage(string message)
    {
        updateStatus=message;
        if(!IsDestroyed && page=="Updates" && updateStatusView is not null)updateStatusView.Text=message;
    }
    private void UpdateBusy(bool busy)
    {
        updateBusy=busy;
        if(page!="Updates")return;
        if(updateActionView is not null){updateActionView.Enabled=!busy;updateActionView.Text=busy?"Working…":"Check for updates";}
        if(updateProgressView is not null)updateProgressView.Visibility=busy?ViewStates.Visible:ViewStates.Gone;
    }
    private async Task CheckForUpdatesAsync()
    {
        if(updateBusy)return;
        UpdateBusy(true);UpdateMessage("Checking for updates…");
        try
        {
            availableUpdate=await AppUpdates.CheckAsync(updateLifetime.Token);
            if(IsDestroyed)return;
            if(availableUpdate.Build<=InstalledBuild){UpdateMessage("Launcher " + InstalledPackage().VersionName + " is up to date.");return;}
            UpdateMessage("Version " + availableUpdate.Version + " is available.");
            var release=availableUpdate;
            Confirm("Launcher update",$"Download version {release.Version} ({release.Bytes/1048576d:0.0} MB)? Your layout and settings are kept.",
                "Download and install",()=>_ = DownloadUpdateAsync(release));
        }
        catch(OperationCanceledException){if(!IsDestroyed)UpdateMessage("Update check cancelled.");}
        catch(Exception e)when(e is HttpRequestException or IOException or System.Text.Json.JsonException)
        {UpdateMessage("Could not check for updates. Check your connection and try again.");}
        finally{if(!IsDestroyed)UpdateBusy(false);}
    }
    private async Task DownloadUpdateAsync(AppRelease release)
    {
        if(updateBusy)return;
        UpdateBusy(true);updatePercent=0;
        try
        {
            await AppUpdates.DownloadAsync(release,UpdatePath,percent=>RunOnUiThread(()=>
            {
                if(IsDestroyed)return;
                updatePercent=percent;UpdateMessage($"Downloading update… {percent}%");
                if(page=="Updates" && updateProgressView is not null)updateProgressView.Progress=percent;
            }),updateLifetime.Token);
            if(IsDestroyed)return;
            VerifyUpdate(release);
            if(OperatingSystem.IsAndroidVersionAtLeast(26) && !PackageManager!.CanRequestPackageInstalls())
            {
                awaitingUpdatePermission=true;UpdateMessage("Allow Launcher to install updates, then return here.");
                StartActivity(new Intent(Android.Provider.Settings.ActionManageUnknownAppSources,Android.Net.Uri.Parse("package:"+PackageName)));
            }
            else InstallUpdate();
        }
        catch(OperationCanceledException){if(!IsDestroyed)UpdateMessage("Update download cancelled.");}
        catch(Exception e)when(e is HttpRequestException or IOException or Java.Lang.RuntimeException)
        {UpdateMessage("Could not install the update. " + e.Message);}
        finally{if(!IsDestroyed)UpdateBusy(false);}
    }
    private void VerifyUpdate(AppRelease release)
    {
        var flags=OperatingSystem.IsAndroidVersionAtLeast(28)?PackageInfoFlags.SigningCertificates:PackageInfoFlags.Signatures;
        var installed=PackageManager!.GetPackageInfo(PackageName!,flags)!;
        var archive=PackageManager.GetPackageArchiveInfo(UpdatePath,flags) ?? throw new IOException("The APK is invalid.");
        var left=OperatingSystem.IsAndroidVersionAtLeast(28)?installed.SigningInfo?.GetApkContentsSigners():installed.Signatures;
        var right=OperatingSystem.IsAndroidVersionAtLeast(28)?archive.SigningInfo?.GetApkContentsSigners():archive.Signatures;
        var history=OperatingSystem.IsAndroidVersionAtLeast(28)&&archive.SigningInfo?.HasMultipleSigners==false?archive.SigningInfo.GetSigningCertificateHistory():null;
        var build=OperatingSystem.IsAndroidVersionAtLeast(28)?archive.LongVersionCode:archive.VersionCode;
        if(archive.PackageName!=PackageName || build!=release.Build || archive.VersionName!=release.Version ||
            left is not {Count:>0} || right is not {Count:>0} ||
            !SigningTrust.Accepts(left.Select(s=>Convert.ToHexString(SHA256.HashData(s.ToByteArray()!))),right.Select(s=>Convert.ToHexString(SHA256.HashData(s.ToByteArray()!))),history?.Select(s=>Convert.ToHexString(SHA256.HashData(s.ToByteArray()!)))??[]))
            throw new IOException("The update does not match this installation.");
    }
    private void ResumeUpdateInstall()
    {
        if(!awaitingUpdatePermission)return;
        awaitingUpdatePermission=false;
        if(OperatingSystem.IsAndroidVersionAtLeast(26) && !PackageManager!.CanRequestPackageInstalls())
        {UpdateMessage("Installation permission was not granted. Check for updates to try again.");return;}
        try{if(availableUpdate is {} release){VerifyUpdate(release);InstallUpdate();}}
        catch(Exception e)when(e is IOException or Java.Lang.RuntimeException){UpdateMessage("Could not open the update. "+e.Message);}
    }
    private void InstallUpdate()
    {
        var uri=Android.Net.Uri.Parse("content://com.launcher.home.updates/Launcher-update.apk")!;
        using var intent=new Intent(Intent.ActionView);
        intent.SetDataAndType(uri,"application/vnd.android.package-archive");
        intent.ClipData=ClipData.NewRawUri("Launcher update",uri);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        StartActivity(intent);UpdateMessage("Complete the update in Android’s installer.");
    }
}
