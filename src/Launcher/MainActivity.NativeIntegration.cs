using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private void RenderPrivateSpace()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
            return;
        var profile = ProfileCatalog.Profiles(this).FirstOrDefault(p => p.Private);
        if (profile is null)
            return;
        bool locked = profile.Quiet || !profile.Unlocked;
        if (!state.ShowPrivateSpace || locked && (state.HidePrivateSpaceWhenLocked || profile.HideWhenLocked))
            return;
        RootCard("private-space", "Private space", "lock", panel =>
        {
            panel.AddView(Button(locked ? "Unlock private space" : "Lock private space", () => SetProfileQuiet(profile, !locked)), Match());
            if (locked)
                Paragraph("Locked. Private apps stay out of drawers and search.", panel);
            else
            {
                Gap(panel, 8);
                AddTileGrid(panel, AppOrganization.Visible(state, apps).Where(a => a.PrivateProfile));
            }
        }, locked ? "Locked" : "Unlocked");
    }
    private void SetProfileQuiet(HomeProfile profile, bool quiet)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            Toast("Manage this profile in Android settings.");
            return;
        }
        try
        {
            var manager = (UserManager?)GetSystemService(UserService);
            if (manager?.RequestQuietModeEnabled(quiet, profile.User) == false)
                Toast("Complete the Android unlock screen to continue.");
            RefreshApps(true);
        }
        catch (Java.Lang.RuntimeException) { Toast("Android could not change this profile. Check that Launcher is your default home."); }
    }
    private void ShowProfiles() => Sheet("Profiles & private space", (panel, dialog) =>
    {
        var profiles = ProfileCatalog.Profiles(this).Where(p => !p.User.Equals(Android.OS.Process.MyUserHandle())).ToArray();
        if (profiles.Length == 0)
            Paragraph("No additional accessible profiles. Android exposes work profiles and supported Private Space when Launcher is your default home.", panel);
        foreach (var profile in profiles)
        {
            var item = profile;
            SettingRow(panel, item.Private ? "Private space" : "Work profile", item.Quiet || !item.Unlocked ? "Locked or paused · tap to unlock" : "Available · tap to lock or pause", () => { dialog.Dismiss(); SetProfileQuiet(item, !item.Quiet && item.Unlocked); }, "lock");
            if (!item.Private)
                continue;
            ToggleRow(panel, "Show Private Space in Apps", state.ShowPrivateSpace, value => { state.ShowPrivateSpace = value; Save(); RenderBody(); });
            ToggleRow(panel, "Hide Private Space when locked", state.HidePrivateSpaceWhenLocked || item.HideWhenLocked, value => { state.HidePrivateSpaceWhenLocked = value; Save(); RenderBody(); });
            if (item.HideWhenLocked)
                panel.GetChildAt(panel.ChildCount - 1)!.Enabled = false;
            Paragraph(item.HideWhenLocked ? "Android is set to hide this space while locked. Change that preference in Android's Private Space settings." : "Hidden spaces can be opened here through Setup & feature health. Private apps remain outside global drawers and search.", panel);
        }
    });
    private void RemovePinnedShortcut(AppEntry app)
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(25) && GetSystemService(LauncherAppsService) is LauncherApps launcher && launcher.HasShortcutHostPermission && ProfileCatalog.User(this, app.ProfileSerial) is { } user)
            {
                var remaining = state.Shortcuts.Where(s => s.Package == app.PackageName && s.ProfileSerial == app.ProfileSerial && s.ShortcutId != app.ShortcutId).Select(s => s.ShortcutId).ToArray();
                launcher.PinShortcuts(app.PackageName, remaining, user);
            }
            state.Shortcuts.RemoveAll(s => s.Package == app.PackageName && s.ProfileSerial == app.ProfileSerial && s.ShortcutId == app.ShortcutId);
            state.Apps.Remove(app.Id);
            Save();
            RefreshApps(true);
            Render();
        }
        catch (Java.Lang.RuntimeException) { Toast("Android could not remove this shortcut. Try again."); }
    }
    private void HandlePinRequest(Intent? intent)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26) || intent?.Action is not ("android.content.pm.action.CONFIRM_PIN_SHORTCUT" or "android.content.pm.action.CONFIRM_PIN_APPWIDGET"))
            return;
        var launcher = GetSystemService(LauncherAppsService) as LauncherApps;
        var request = launcher?.GetPinItemRequest(intent);
        intent.SetAction(Intent.ActionMain);
        if (request?.IsValid != true)
        {
            Toast("This pin request expired. Request it again from the source app.");
            return;
        }
        handler.Post(() =>
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26) || IsDestroyed)
                return;
            if (request.ShortcutInfo is { } shortcut)
            {
                if (state.Shortcuts.Count >= 200)
                {
                    Toast("The shortcut limit is 200.");
                    return;
                }
                string label = shortcut.ShortLabel?.ToString() ?? "Shortcut";
                Confirm("Add to your home?", label, "Add shortcut", () =>
                {
                    if (!OperatingSystem.IsAndroidVersionAtLeast(26))
                        return;
                    try
                    {
                        if (!request.IsValid || !request.Accept())
                        {
                            Toast("This shortcut is no longer available.");
                            return;
                        }
                        var manager = GetSystemService(UserService) as UserManager;
                        long serial = shortcut.UserHandle?.Equals(Android.OS.Process.MyUserHandle()) == true ? -1 : manager?.GetSerialNumberForUser(shortcut.UserHandle!) ?? -1;
                        string package = shortcut.Package!, shortcutId = shortcut.Id!;
                        state.Shortcuts.RemoveAll(s => s.Package == package && s.ShortcutId == shortcutId && s.ProfileSerial == serial);
                        state.Shortcuts.Add(new()
                        {
                            Package = package,
                            ShortcutId = shortcutId,
                            Label = label,
                            ProfileSerial = serial
                        });
                        Save();
                        RefreshApps(true);
                        Navigate("Tiles");
                    }
                    catch (Java.Lang.RuntimeException) { Toast("Android could not pin this shortcut."); }
                });
            }
            else if (request.GetAppWidgetProviderInfo(this) is { } provider)
            {
                if (state.Widgets.Count >= 20)
                {
                    Toast("The widget limit is 20.");
                    return;
                }
                Confirm("Add Android widget?", provider.LoadLabel(PackageManager) ?? "Android widget", "Add widget", () =>
                {
                    if (!OperatingSystem.IsAndroidVersionAtLeast(26))
                        return;
                    int id = widgetHost.AllocateAppWidgetId();
                    try
                    {
                        var options = new Bundle();
                        options.PutInt(AppWidgetManager.ExtraAppwidgetId, id);
                        if (!request.IsValid || !request.Accept(options))
                        {
                            widgetHost.DeleteAppWidgetId(id);
                            Toast("This widget request expired.");
                            return;
                        }
                        if (provider.Configure is not null)
                        {
                            pendingWidget = id;
                            StartActivityForResult(new Intent(AppWidgetManager.ActionAppwidgetConfigure).SetComponent(provider.Configure).PutExtra(AppWidgetManager.ExtraAppwidgetId, id), 802);
                        }
                        else
                        {
                            state.Widgets.Add(new()
                            {
                                Id = id,
                                AutoHeight = true
                            });
                            Save();
                            Navigate("Root");
                        }
                    }
                    catch (Java.Lang.RuntimeException) { widgetHost.DeleteAppWidgetId(id); pendingWidget = -1; Toast("Android could not add this widget."); }
                });
            }
        });
    }
}
