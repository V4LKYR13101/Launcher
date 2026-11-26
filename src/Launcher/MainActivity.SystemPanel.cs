using Android.Content;
using Android.App;
using Android.Widget;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private void HandleSystemPanelIntent(Intent? intent)
    {
        string? key = intent?.GetStringExtra("system_panel_notification"), reply = intent?.GetStringExtra("system_panel_reply"), player = intent?.GetStringExtra("system_panel_player");
        intent?.RemoveExtra("system_panel_notification");
        intent?.RemoveExtra("system_panel_reply");
        intent?.RemoveExtra("system_panel_player");
        if (key is not null || reply is not null || player is not null)
            handler.Post(() =>
        {
            // Refresh rules before authenticating an overlay entry point.
            state.Apps = store.Load().Apps;
            if (player is not null)
                WithAppUnlock(player, () => { var launch = PackageManager?.GetLaunchIntentForPackage(player); if (launch is not null) OpenIntent(launch); });
            else if (ConversationService.NotificationSnapshot().FirstOrDefault(n => n.Key == (key ?? reply)) is { } notification)
            {
                if (reply is not null)
                    ReplyTo(notification);
                else
                    OpenNotification(notification);
            }
        });
    }
    private Dialog? systemPanelSettings;
    private LinearLayout? systemPanelSettingsContent;
    private void RefreshSystemPanelSettings()
    {
        if (systemPanelSettings?.IsShowing != true)
            return;
        int position = (systemPanelSettingsContent?.Parent as ScrollView)?.ScrollY ?? 0;
        systemPanelSettings.Dismiss();
        handler.Post(() => { ShowSystemPanelSettings(); if (systemPanelSettingsContent?.Parent is ScrollView view) view.Post(() => view.ScrollTo(0, position)); });
    }
    private void ShowSystemPanelSettings()
    {
        if (LauncherDistribution.IsPlay)
        {
            Toast(Localize(Resource.String.ui_play_optional_services_unavailable));
            return;
        }
        if (systemPanelSettings?.IsShowing == true)
            return;
        systemPanelSettings = Sheet(Localize(Resource.String.ui_system_panel), (panel, dialog) =>
    {
        systemPanelSettingsContent = panel;
        Paragraph(Localize(Resource.String.ui_a_matching_status_strip_and_quick_panel_across_your), panel);
        ToggleRow(panel, Localize(Resource.String.ui_enable_system_panel), state.SystemPanelEnabled, v => { state.SystemPanelEnabled = v; Save(); });
        SettingRow(panel, Localize(Resource.String.ui_accessibility_access), SystemPanelService.IsEnabled(this) ? Localize(Resource.String.ui_connected) : Localize(Resource.String.ui_required_enable_launcher_system_panel), () => OpenIntent(new Intent(Android.Provider.Settings.ActionAccessibilitySettings)), "settings");
        Paragraph(Localize(Resource.String.ui_if_android_blocks_access_for_an_apk_installed_through), panel);
        SettingRow(panel, Localize(Resource.String.ui_notification_access), ConversationService.Enabled(this) ? Localize(Resource.String.ui_connected) : Localize(Resource.String.ui_required_for_notifications_and_media), () => OpenIntent(new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS")), "message");
        SettingRow(panel, Localize(Resource.String.ui_brightness_access), Android.Provider.Settings.System.CanWrite(this) ? Localize(Resource.String.ui_allowed) : Localize(Resource.String.ui_optional_change_system_brightness), () => OpenIntent(new Intent(Android.Provider.Settings.ActionManageWriteSettings, Android.Net.Uri.Parse("package:" + PackageName))), "sun");
        SettingRow(panel, Localize(Resource.String.ui_connection_names), Localize(Resource.String.ui_optional_nearby_devices_and_wi_fi_name), RequestConnectionDetails, "wifi");
        Paragraph(Localize(Resource.String.ui_the_optional_status_strip_draws_over_other_apps_below), panel);
        ToggleRow(panel, Localize(Resource.String.ui_show_status_strip), state.SystemPanelStatus, v => { state.SystemPanelStatus = v; Save(); });
        ToggleRow(panel, Localize(Resource.String.ui_place_below_android_status_bar), state.SystemPanelBelowStatus, v => { state.SystemPanelBelowStatus = v; Save(); });
        ToggleRow(panel, Localize(Resource.String.ui_panel_animations), state.SystemPanelAnimations, v => { state.SystemPanelAnimations = v; Save(); });
        SettingRow(panel, Localize(Resource.String.ui_handle_position), AppearanceValue(state.SystemPanelSide), () => Choose(Resource.String.ui_handle_position, [Localize(Resource.String.ui_left), Localize(Resource.String.ui_right)], i => { state.SystemPanelSide = i == 0 ? "Left" : "Right"; Save(); RefreshSystemPanelSettings(); }), "more");
        SettingRow(panel, Localize(Resource.String.ui_handle_distance), state.SystemPanelOffset + Localize(Resource.String.ui_dp_below_androids_status_bar), () => Choose(Resource.String.ui_handle_distance, [Localize(Resource.String.ui_near_32_dp), Localize(Resource.String.ui_middle_48_dp), Localize(Resource.String.ui_lower_80_dp)], i => { state.SystemPanelOffset = new[] { 32, 48, 80 }[i]; Save(); RefreshSystemPanelSettings(); }), "more");
        SettingRow(panel, Localize(Resource.String.ui_panel_theme), AppearanceValue(state.SystemPanelTheme), () => Choose(Resource.String.ui_panel_theme, [Localize(Resource.String.ui_follow_launcher), Localize(Resource.String.ui_dark), Localize(Resource.String.ui_light)], i => { state.SystemPanelTheme = new[] { "Follow Launcher", "Dark", "Light" }[i]; Save(); RefreshSystemPanelSettings(); }), "sun");
        SettingRow(panel, Localize(Resource.String.ui_surface_opacity), state.SystemPanelOpacity + "%", () => Choose(Resource.String.ui_surface_opacity, ["100%", "95%", "90%"], i => { state.SystemPanelOpacity = new[] { 100, 95, 90 }[i]; Save(); RefreshSystemPanelSettings(); }), "photo");
        SettingRow(panel, Localize(Resource.String.ui_control_order), string.Join(" · ", state.SystemPanelControls ?? []), () => Choose(Resource.String.ui_control_order, [Localize(Resource.String.ui_connections_first), Localize(Resource.String.ui_display_first), Localize(Resource.String.ui_sound_first)], i => { state.SystemPanelControls = i == 1 ? ["Display", "Battery", "Wi-Fi", "Bluetooth", "Sound", "Settings"] : i == 2 ? ["Sound", "Bluetooth", "Wi-Fi", "Display", "Battery", "Settings"] : ["Wi-Fi", "Bluetooth", "Sound", "Display", "Battery", "Settings"]; Save(); RefreshSystemPanelSettings(); }), "grid");
        SettingRow(panel, Localize(Resource.String.ui_open_panel), Localize(Resource.String.ui_preview_after_enabling_the_switch_and_accessibility_access), () => { dialog.Dismiss(); if (!SystemPanelService.Open()) Toast(Localize(Resource.String.ui_enable_system_panel_and_its_accessibility_access_first)); }, "phone");
        Paragraph(Localize(Resource.String.ui_the_handle_uses_only_its_small_visible_area_the), panel);
        SettingRow(panel, Localize(Resource.String.ui_disable_accessibility_service), Localize(Resource.String.ui_remove_the_panel_and_disconnect_its_service), () => { state.SystemPanelEnabled = false; Save(); SystemPanelService.Disconnect(); dialog.Dismiss(); }, "settings");
    });
    }
}
