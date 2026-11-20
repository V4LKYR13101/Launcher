using Android.Content;
using Android.OS;
using Android.Content.PM;
namespace LauncherApp.Services;

public sealed class AppCatalogWatcher(Context context, Action changed) : BroadcastReceiver
{
    private bool registered;
    private LauncherApps? launcher;
    private PackageChanges? callback;
    public void Start()
    {
        if (registered)
            return;
        var packages = new IntentFilter();
        foreach (var action in new[] { Intent.ActionPackageAdded, Intent.ActionPackageRemoved, Intent.ActionPackageChanged, Intent.ActionPackageReplaced })
            packages.AddAction(action);
        packages.AddDataScheme("package");
        var language = new IntentFilter(Intent.ActionLocaleChanged);
        var external = new IntentFilter();
        external.AddAction(Intent.ActionExternalApplicationsAvailable);
        external.AddAction(Intent.ActionExternalApplicationsUnavailable);
        external.AddAction(PowerManager.ActionPowerSaveModeChanged);
        foreach (string action in new[] { "android.intent.action.MANAGED_PROFILE_ADDED", "android.intent.action.MANAGED_PROFILE_REMOVED", "android.intent.action.MANAGED_PROFILE_AVAILABLE", "android.intent.action.MANAGED_PROFILE_UNAVAILABLE", "android.intent.action.PROFILE_ACCESSIBLE", "android.intent.action.PROFILE_INACCESSIBLE", "android.intent.action.USER_UNLOCKED" })
            external.AddAction(action);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            context.RegisterReceiver(this, packages, ReceiverFlags.NotExported);
            context.RegisterReceiver(this, language, ReceiverFlags.NotExported);
            context.RegisterReceiver(this, external, ReceiverFlags.NotExported);
        }
        else
        {
            context.RegisterReceiver(this, packages);
            context.RegisterReceiver(this, language);
            context.RegisterReceiver(this, external);
        }
        try
        {
            launcher = context.GetSystemService(Context.LauncherAppsService) as LauncherApps;
            callback = new(changed);
            launcher?.RegisterCallback(callback);
        }
        catch (Java.Lang.RuntimeException) { launcher = null; callback = null; }
        registered = true;
    }
    public void Stop()
    {
        if (registered)
        {
            context.UnregisterReceiver(this);
            registered = false;
        }
        if (callback is not null)
        {
            try
            {
                launcher?.UnregisterCallback(callback);
            }
            catch (Java.Lang.RuntimeException) { }
            callback.Dispose();
            callback = null;
            launcher = null;
        }
    }
    public override void OnReceive(Context? context, Intent? intent) => changed();
    private sealed class PackageChanges(Action changed) : LauncherApps.Callback
    {
        public override void OnPackageAdded(string? packageName, UserHandle? user) => changed();
        public override void OnPackageChanged(string? packageName, UserHandle? user) => changed();
        public override void OnPackageRemoved(string? packageName, UserHandle? user) => changed();
        public override void OnPackagesAvailable(string[]? packageNames, UserHandle? user, bool replacing) => changed();
        public override void OnPackagesUnavailable(string[]? packageNames, UserHandle? user, bool replacing) => changed();
        public override void OnShortcutsChanged(string? packageName, IList<ShortcutInfo>? shortcuts, UserHandle? user) => changed();
    }
}
