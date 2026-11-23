using Android.App;
using Android.Content;
using Android.Content.PM;

namespace LauncherApp.Services;

internal static class DownloadRequest
{
    internal const int PermissionRequest = 918;
    private const string EnabledKey = "enabled";
    private const string NotificationKey = "notification_requested";

    internal static bool Enabled(Context context) => DownloadHistory.Preferences(context).GetBoolean(EnabledKey, true);

    internal static void SetEnabled(Context context, bool enabled)
    {
        DownloadHistory.Preferences(context).Edit()!.PutBoolean(EnabledKey, enabled)!.Apply();
        using var component = new ComponentName(context.PackageName!, ShareDownloadActivity.ShareActivityName);
        context.PackageManager!.SetComponentEnabledSetting(component,
            enabled ? ComponentEnabledState.Enabled : ComponentEnabledState.Disabled, ComponentEnableOption.DontKillApp);
    }

    internal static bool HasStorageAccess(Context context) => OperatingSystem.IsAndroidVersionAtLeast(29) ||
        context.CheckSelfPermission(Android.Manifest.Permission.WriteExternalStorage) == Permission.Granted;

    internal static bool RequestPermission(Activity activity)
    {
        if (!HasStorageAccess(activity))
        {
            activity.RequestPermissions([Android.Manifest.Permission.WriteExternalStorage], PermissionRequest);
            return true;
        }
        var preferences = DownloadHistory.Preferences(activity);
        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            activity.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted &&
            !preferences.GetBoolean(NotificationKey, false))
        {
            preferences.Edit()!.PutBoolean(NotificationKey, true)!.Apply();
            activity.RequestPermissions([Android.Manifest.Permission.PostNotifications], PermissionRequest);
            return true;
        }
        return false;
    }

    internal static string? Start(Activity activity, Uri url)
    {
        if (!Enabled(activity))
            return "Downloads are off in Launcher settings.";
        if (!HasStorageAccess(activity))
            return "Allow storage access to save to Downloads.";

        using var intent = new Intent(activity, typeof(DirectDownloadService));
        intent.PutExtra(DirectDownloadService.UrlExtra, url.AbsoluteUri);
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                activity.StartForegroundService(intent);
            else
                activity.StartService(intent);
            return null;
        }
        catch (Java.Lang.IllegalStateException) { return "Could not start the download."; }
    }
}
