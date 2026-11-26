#if LAUNCHER_PLAY
using Android.Content.PM;

namespace LauncherApp;

public partial class MainActivity
{
    private readonly CancellationTokenSource updateLifetime = new();
    private PackageInfo InstalledPackage() => PackageManager!.GetPackageInfo(PackageName!, PackageInfoFlags.Activities)!;
    private void RenderUpdates()
    {
        Title(Localize(Resource.String.ui_app_updates));
        Paragraph(Localize(Resource.String.ui_play_manages_updates));
    }

    private void ResumeUpdateInstall() { }
}
#endif
