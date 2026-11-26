using Android.App;
using Android.Content;
using Android.Widget;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private void ShowFeatureHealth() => Sheet(Localize(Resource.String.ui_your_home_ready), (panel, dialog) =>
    {
        var capabilities = new DeviceCapabilities(this);
        Paragraph(Localize(Resource.String.ui_core_launching_works_without_optional_access_enable_only_the), panel);
        bool home = PackageManager?.ResolveActivity(new Intent(Intent.ActionMain).AddCategory(Intent.CategoryHome), 0)?.ActivityInfo?.PackageName == PackageName;
        SettingRow(panel, Localize(Resource.String.ui_default_home), home ? Localize(Resource.String.ui_ready) : Localize(Resource.String.ui_choose_launcher_in_android_settings), () => { dialog.Dismiss(); Navigate("Permissions"); }, "root");
        SettingRow(panel, Localize(Resource.String.ui_profiles_private_space), Localize(Resource.String.ui_work_apps_and_supported_private_profiles), () => { dialog.Dismiss(); ShowProfiles(); }, "lock");
        SettingRow(panel, Localize(Resource.String.ui_notifications_media), ConversationService.Enabled(this) ? Localize(Resource.String.ui_connected) : Localize(Resource.String.ui_optional_access_not_connected), () => { dialog.Dismiss(); Navigate("Permissions"); }, "message");
        SettingRow(panel, Localize(Resource.String.ui_timer_alerts), capabilities.Notifications ? (capabilities.ExactTimers ? Localize(Resource.String.ui_notifications_and_exact_alarms_ready) : Localize(Resource.String.ui_alerts_enabled_exact_timing_needs_access)) : Localize(Resource.String.ui_notification_permission_required), OpenTimerPermissions, "clock");
        SettingRow(panel, Localize(Resource.String.ui_app_protection), capabilities.SecureLock ? Localize(Resource.String.ui_screen_lock_authentication_available) : Localize(Resource.String.ui_unavailable_set_an_android_screen_lock), () => OpenIntent(new Intent(Android.Provider.Settings.ActionSecuritySettings)), "lock");
        SettingRow(panel, Localize(Resource.String.ui_depth_motion), capabilities.LowMemoryDevice ? Localize(Resource.String.ui_off_low_memory_device) : capabilities.PowerSaving ? Localize(Resource.String.ui_off_while_battery_saver_is_active) : state.LayeredInterface ? Localize(Resource.String.ui_ready_follows_android_animation_settings) : Localize(Resource.String.ui_off_in_appearance), () => { dialog.Dismiss(); Navigate("Appearance"); }, "photo");
        SettingRow(panel, Localize(Resource.String.ui_ambient_theme), ambient.Available ? Localize(Resource.String.ui_light_sensor_available) : Localize(Resource.String.ui_unavailable_no_light_sensor), () => { dialog.Dismiss(); Navigate("Automatic theme"); }, "sun");
        SettingRow(panel, Localize(Resource.String.ui_tilt_depth), depthMotion.Available ? Localize(Resource.String.ui_rotation_sensor_available) : Localize(Resource.String.ui_unavailable_no_rotation_sensor), () => { dialog.Dismiss(); Navigate("Appearance"); }, "phone");
        SettingRow(panel, Localize(Resource.String.ui_weather), string.IsNullOrEmpty(state.Weather.City) ? Localize(Resource.String.ui_choose_a_city) : WeatherSummary(), () => { dialog.Dismiss(); Navigate("Weather"); }, "cloud");
        SettingRow(panel, Localize(Resource.String.ui_agenda), AgendaReader.Allowed(this) ? Localize(Resource.String.ui_connected_choose_calendars) : Localize(Resource.String.ui_optional_calendar_access_not_connected), ShowCalendarChoices, "calendar");
        SettingRow(panel, Localize(Resource.String.ui_recovery), store.RecoveryAvailable ? Localize(Resource.String.ui_a_recovery_copy_is_retained_on_this_phone) : store.PreviousAvailable ? Localize(Resource.String.ui_previous_saved_state_available) : Localize(Resource.String.ui_a_recovery_point_is_created_after_saving), () => { dialog.Dismiss(); ShowPrivacyRecovery(); }, "file");
        Paragraph(Localize(Resource.String.ui_android_2) + Android.OS.Build.VERSION.Release + Localize(Resource.String.ui_64_bit_android_build_access_and_device_capabilities_are), panel);
    }, (footer, dialog) => footer.AddView(Button(Localize(Resource.String.ui_ready_to_use), () => { state.SetupCompleted = true; Save(); dialog.Dismiss(); RenderBody(); }, true), Match()));
    private void ShowPrivacyRecovery() => Sheet(Localize(Resource.String.ui_privacy_recovery), (panel, dialog) =>
    {
        ToggleRow(panel, Localize(Resource.String.ui_hide_protected_app_previews), state.HideProtectedPreviews, value => WithProtectedSettings(() => { state.HideProtectedPreviews = value; Save(); dialog.Dismiss(); ShowPrivacyRecovery(); }));
        Paragraph(Localize(Resource.String.ui_protected_notification_titles_and_text_are_hidden_across_launcher), panel);
        SettingRow(panel, Localize(Resource.String.ui_message_history), state.KeepMessageHistory ? Localize(Resource.String.ui_collecting_30_days_or_5_000_messages) : Localize(Resource.String.ui_not_collecting), () => { dialog.Dismiss(); MessageHistorySettings(); }, "message");
        SettingRow(panel, Localize(Resource.String.ui_export_a_backup), Localize(Resource.String.ui_includes_notes_and_journeys_as_readable_json_keep_it), () => { dialog.Dismiss(); ExportBackup(); }, "file");
        SettingRow(panel, Localize(Resource.String.ui_restore_selected_content), Localize(Resource.String.ui_preview_and_choose_what_to_restore), () => { dialog.Dismiss(); ImportBackup(); }, "file");
        if (store.PreviousAvailable)
            SettingRow(panel, Localize(Resource.String.ui_recover_previous_saved_state), Localize(Resource.String.ui_review_before_restoring), () => { try { var previous = store.LoadPrevious(); dialog.Dismiss(); ShowRestoreOptions(previous); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException) { Toast(Localize(Resource.String.ui_this_recovery_point_could_not_be_read)); } }, "clock");
        Paragraph(store.RecoveryAvailable ? Localize(Resource.String.ui_an_unreadable_state_copy_is_preserved_in_launcher_s) : Localize(Resource.String.ui_saves_replace_the_state_file_atomically_and_keep_a), panel);
        Paragraph(Localize(Resource.String.ui_weather_contacts_open_meteo_enabled_feeds_contact_their_publishers), panel);
    });
}
