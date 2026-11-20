using Android.Content;
using Android.Content.PM;
using Android.Graphics.Drawables;
using Android.OS;
using Launcher.Core;
namespace LauncherApp.Services;

public sealed record HomeProfile(UserHandle User, long Serial, bool Private, bool Quiet, bool Unlocked, bool HideWhenLocked = false);
public static class ProfileCatalog
{
    public static HomeProfile[] Profiles(Context context)
    {
        var manager = context.GetSystemService(Context.UserService) as UserManager;
        var launcher = context.GetSystemService(Context.LauncherAppsService) as LauncherApps;
        if (manager is null)
            return [];
        var result = new List<HomeProfile>();
        IList<UserHandle> users;
        try
        {
            users = (OperatingSystem.IsAndroidVersionAtLeast(26) ? launcher?.Profiles : null) ?? manager.UserProfiles ?? [];
        }
        catch (Java.Lang.RuntimeException) { return []; }
        foreach (var user in users)
        {
            try
            {
                var info = OperatingSystem.IsAndroidVersionAtLeast(35) ? launcher?.GetLauncherUserInfo(user) : null;
                bool hidden = OperatingSystem.IsAndroidVersionAtLeast(35) && info?.UserType == "android.os.usertype.profile.PRIVATE";
                bool hideWhenLocked = OperatingSystem.IsAndroidVersionAtLeast(36) && info?.UserConfig?.GetBoolean("private_space_entrypoint_hidden", false) == true;
                result.Add(new(user, manager.GetSerialNumberForUser(user), hidden, manager.IsQuietModeEnabled(user), manager.InvokeIsUserUnlocked(user), hideWhenLocked));
            }
            catch (Java.Lang.RuntimeException) { }
        }
        return result.ToArray();
    }
    public static UserHandle? User(Context context, long serial) => serial < 0 ? Android.OS.Process.MyUserHandle() : (context.GetSystemService(Context.UserService) as UserManager)?.GetUserForSerialNumber(serial);
    public static Drawable? Icon(Context context, AppEntry app)
    {
        var launcher = context.GetSystemService(Context.LauncherAppsService) as LauncherApps;
        var user = User(context, app.ProfileSerial);
        if (launcher is null || user is null)
            return null;
        if (app.ShortcutId.Length > 0 && OperatingSystem.IsAndroidVersionAtLeast(25) && launcher.HasShortcutHostPermission)
        {
            using var query = new LauncherApps.ShortcutQuery();
            query.SetPackage(app.PackageName);
            query.SetShortcutIds([app.ShortcutId]);
            query.SetQueryFlags((LauncherAppsShortcutQueryFlags)11);
            var shortcut = launcher.GetShortcuts(query, user)?.FirstOrDefault();
            return shortcut is null ? null : launcher.GetShortcutIconDrawable(shortcut, (int)context.Resources!.DisplayMetrics!.DensityDpi);
        }
        return launcher.GetActivityList(app.PackageName, user)?.FirstOrDefault(a => a.ComponentName?.ClassName == app.ActivityName)?.GetBadgedIcon((int)context.Resources!.DisplayMetrics!.DensityDpi);
    }
}
