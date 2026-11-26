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
        RootCard("private-space", Localize(Resource.String.ui_private_space), "lock", panel =>
        {
            panel.AddView(Button(locked ? Localize(Resource.String.ui_unlock_private_space) : Localize(Resource.String.ui_lock_private_space), () => SetProfileQuiet(profile, !locked)), Match());
            if (locked)
                Paragraph(Localize(Resource.String.ui_locked_private_apps_stay_out_of_drawers_and_search), panel);
            else
            {
                Gap(panel, 8);
                AddTileGrid(panel, AppOrganization.Visible(state, apps).Where(a => a.PrivateProfile));
            }
        }, locked ? Localize(Resource.String.ui_locked_2) : Localize(Resource.String.ui_unlocked));
    }
    private void SetProfileQuiet(HomeProfile profile, bool quiet)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            Toast(Localize(Resource.String.ui_manage_this_profile_in_android_settings));
            return;
        }
        try
        {
            var manager = (UserManager?)GetSystemService(UserService);
            if (manager?.RequestQuietModeEnabled(quiet, profile.User) == false)
                Toast(Localize(Resource.String.ui_complete_the_android_unlock_screen_to_continue));
            RefreshApps(true);
        }
        catch (Java.Lang.RuntimeException) { Toast(Localize(Resource.String.ui_android_could_not_change_this_profile_check_that_launcher)); }
    }
    private void ShowProfiles() => Sheet(Localize(Resource.String.ui_profiles_private_space), (panel, dialog) =>
    {
        var profiles = ProfileCatalog.Profiles(this).Where(p => !p.User.Equals(Android.OS.Process.MyUserHandle())).ToArray();
        if (profiles.Length == 0)
            Paragraph(Localize(Resource.String.ui_no_additional_accessible_profiles_android_exposes_work_profiles_and), panel);
        foreach (var profile in profiles)
        {
            var item = profile;
            SettingRow(panel, item.Private ? Localize(Resource.String.ui_private_space) : Localize(Resource.String.ui_work_profile), item.Quiet || !item.Unlocked ? Localize(Resource.String.ui_locked_or_paused_tap_to_unlock) : Localize(Resource.String.ui_available_tap_to_lock_or_pause), () => { dialog.Dismiss(); SetProfileQuiet(item, !item.Quiet && item.Unlocked); }, "lock");
            if (!item.Private)
                continue;
            ToggleRow(panel, Localize(Resource.String.ui_show_private_space_in_apps), state.ShowPrivateSpace, value => { state.ShowPrivateSpace = value; Save(); RenderBody(); });
            ToggleRow(panel, Localize(Resource.String.ui_hide_private_space_when_locked), state.HidePrivateSpaceWhenLocked || item.HideWhenLocked, value => { state.HidePrivateSpaceWhenLocked = value; Save(); RenderBody(); });
            if (item.HideWhenLocked)
                panel.GetChildAt(panel.ChildCount - 1)!.Enabled = false;
            Paragraph(item.HideWhenLocked ? Localize(Resource.String.ui_android_is_set_to_hide_this_space_while_locked) : Localize(Resource.String.ui_hidden_spaces_can_be_opened_here_through_setup_feature), panel);
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
        catch (Java.Lang.RuntimeException) { Toast(Localize(Resource.String.ui_android_could_not_remove_this_shortcut_try_again)); }
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
            Toast(Localize(Resource.String.ui_this_pin_request_expired_request_it_again_from_the));
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
                    Toast(Localize(Resource.String.ui_the_shortcut_limit_is_200));
                    return;
                }
                string label = shortcut.ShortLabel?.ToString() ?? Localize(Resource.String.ui_shortcut);
                Confirm(Localize(Resource.String.ui_add_to_your_home), label, Localize(Resource.String.ui_add_shortcut), () =>
                {
                    if (!OperatingSystem.IsAndroidVersionAtLeast(26))
                        return;
                    try
                    {
                        if (!request.IsValid || !request.Accept())
                        {
                            Toast(Localize(Resource.String.ui_this_shortcut_is_no_longer_available));
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
                    catch (Java.Lang.RuntimeException) { Toast(Localize(Resource.String.ui_android_could_not_pin_this_shortcut)); }
                });
            }
            else if (request.GetAppWidgetProviderInfo(this) is { } provider)
            {
                if (state.Widgets.Count >= 20)
                {
                    Toast(Localize(Resource.String.ui_the_widget_limit_is_20));
                    return;
                }
                Confirm(Localize(Resource.String.ui_add_android_widget), provider.LoadLabel(PackageManager) ?? Localize(Resource.String.ui_android_widget), Localize(Resource.String.ui_add_widget), () =>
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
                            Toast(Localize(Resource.String.ui_this_widget_request_expired));
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
                    catch (Java.Lang.RuntimeException) { widgetHost.DeleteAppWidgetId(id); pendingWidget = -1; Toast(Localize(Resource.String.ui_android_could_not_add_this_widget)); }
                });
            }
        });
    }
}
