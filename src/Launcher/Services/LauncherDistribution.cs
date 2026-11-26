namespace LauncherApp.Services;

internal static class LauncherDistribution
{
#if LAUNCHER_PLAY
    internal static bool IsPlay => true;
    internal const string FileAuthority = "com.kevlog.launcher.searchfiles";
#else
    internal static bool IsPlay => false;
    internal const string FileAuthority = "com.launcher.home.searchfiles";
#endif
}
