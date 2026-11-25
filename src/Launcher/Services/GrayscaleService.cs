using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
using Android.Views.Accessibility;

namespace LauncherApp.Services;

[Service(Name = "com.launcher.home.GrayscaleService", Label = "@string/ui_launcher_app_grayscale", Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE", Exported = true)]
[IntentFilter(["android.accessibilityservice.AccessibilityService"])]
[MetaData("android.accessibilityservice", Resource = "@xml/grayscale_service")]
public sealed class GrayscaleService : AccessibilityService
{
    private const string EnabledKey = "accessibility_display_daltonizer_enabled", ModeKey = "accessibility_display_daltonizer";
    private static GrayscaleService? current;
    private string foreground = "";
    private bool wantsGrayscale, panelVisible;
    public static void SetPanelVisible(bool visible)
    {
        if (current is { } service)
        {
            service.panelVisible = visible;
            service.ApplyForeground();
        }
    }
    private ISharedPreferences Session => GetSharedPreferences("grayscale-session", FileCreationMode.Private)!;
    public static bool HasSystemAccess(Context context) => context.CheckSelfPermission("android.permission.WRITE_SECURE_SETTINGS") == Permission.Granted;
    public static bool IsEnabled(Context context)
    {
        string service = new ComponentName(context, "com.launcher.home.GrayscaleService").FlattenToString();
        return (Settings.Secure.GetString(context.ContentResolver, Settings.Secure.EnabledAccessibilityServices) ?? "").Split(':')
            .Any(value => ComponentName.UnflattenFromString(value)?.FlattenToString().Equals(service, StringComparison.OrdinalIgnoreCase) == true);
    }
    public static bool Ready(Context context) => HasSystemAccess(context) && IsEnabled(context);
    public static bool Global(Context context) => context.GetSharedPreferences("grayscale-session", FileCreationMode.Private)!.GetBoolean("global", false);
    public static void SetGlobal(Context context, bool value)
    {
        context.GetSharedPreferences("grayscale-session", FileCreationMode.Private)!.Edit()!.PutBoolean("global", value)!.Apply();
        current?.ApplyForeground();
    }
    public static void RulesChanged() => current?.ApplyForeground();
    public static void StopAndRestore()
    {
        if (current is { } service)
        {
            SetGlobal(service, false);
            service.Restore();
            service.DisableSelf();
        }
    }
    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        current = this;
        Restore();
        ApplyForeground();
    }
    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
        if (e?.EventType != EventTypes.WindowStateChanged)
            return;
        var package = e.PackageName?.ToString();
        if (string.IsNullOrWhiteSpace(package))
            return;
        // Keyboard windows do not change the policy of the app being edited.
        string keyboard = (Settings.Secure.GetString(ContentResolver, Settings.Secure.DefaultInputMethod) ?? "").Split('/')[0];
        if (package == keyboard)
            return;
        if (package == PackageName && !(e.ClassName?.ToString() ?? "").Contains("MainActivity", StringComparison.Ordinal) && !(e.ClassName?.ToString() ?? "").Contains("Dialog", StringComparison.Ordinal))
            return;
        foreground = package;
        ApplyForeground();
    }
    private void ApplyForeground()
    {
        var state = new StateStore(this).Load();
        wantsGrayscale = !panelVisible && (Global(this) || (foreground.Length > 0 && foreground != PackageName && foreground != "com.android.systemui" &&
            state.Apps.Any(p => p.Key.StartsWith(foreground + "/", StringComparison.Ordinal) && p.Value.OpenInGrayscale)));
        if (!wantsGrayscale)
        {
            Restore();
            return;
        }
        if (!HasSystemAccess(this))
            return;
        var prefs = Session;
        if (prefs.GetBoolean("active", false))
            return;
        int enabled = Settings.Secure.GetInt(ContentResolver, EnabledKey, 0), mode = Settings.Secure.GetInt(ContentResolver, ModeKey, -1);
        // Persist the previous display state before touching the system setting.
        if (!prefs.Edit()!.PutInt("enabled", enabled)!.PutInt("mode", mode)!.PutBoolean("active", true)!.Commit())
            return;
        try
        {
            if (!Settings.Secure.PutInt(ContentResolver, ModeKey, 0) || !Settings.Secure.PutInt(ContentResolver, EnabledKey, 1))
                Restore();
        }
        catch (Java.Lang.SecurityException) { Restore(); }
    }
    private void Restore()
    {
        var prefs = Session;
        if (!prefs.GetBoolean("active", false) || !HasSystemAccess(this))
            return;
        try
        {
            int enabled = Settings.Secure.GetInt(ContentResolver, EnabledKey, 0), mode = Settings.Secure.GetInt(ContentResolver, ModeKey, -1);
            int previousEnabled = prefs.GetInt("enabled", 0), previousMode = prefs.GetInt("mode", -1);
            // Preserve a user's manual change made while the service was active.
            if (mode == 0 && (enabled == 1 || enabled == previousEnabled))
            {
                if (!Settings.Secure.PutInt(ContentResolver, EnabledKey, previousEnabled))
                    return;
                if (!Settings.Secure.PutInt(ContentResolver, ModeKey, previousMode))
                    return;
            }
            prefs.Edit()!.PutBoolean("active", false)!.Commit();
        }
        catch (Java.Lang.SecurityException) { }
    }
    public override void OnInterrupt() => Restore();
    public override bool OnUnbind(Intent? intent)
    {
        Restore();
        if (current == this)
            current = null;
        return base.OnUnbind(intent);
    }
    public override void OnDestroy()
    {
        Restore();
        if (current == this)
            current = null;
        base.OnDestroy();
    }
}
