using Android.App;
using Android.Content;
using Android.Widget;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private void ShowFeatureHealth() => Sheet("Your home, ready", (panel, dialog) =>
    {
        var capabilities = new DeviceCapabilities(this);
        Paragraph("Core launching works without optional access. Enable only the features you want. Unavailable hardware and battery-saving restrictions turn off depth effects automatically.", panel);
        bool home = PackageManager?.ResolveActivity(new Intent(Intent.ActionMain).AddCategory(Intent.CategoryHome), 0)?.ActivityInfo?.PackageName == PackageName;
        SettingRow(panel, "Default home", home ? "Ready" : "Choose Launcher in Android settings", () => { dialog.Dismiss(); Navigate("Permissions"); }, "root");
        SettingRow(panel, "Profiles & private space", "Work apps and supported private profiles", () => { dialog.Dismiss(); ShowProfiles(); }, "lock");
        SettingRow(panel, "Notifications & media", ConversationService.Enabled(this) ? "Connected" : "Optional · access not connected", () => { dialog.Dismiss(); Navigate("Permissions"); }, "message");
        SettingRow(panel, "Timer alerts", capabilities.Notifications ? (capabilities.ExactTimers ? "Notifications and exact alarms ready" : "Alerts enabled · exact timing needs access") : "Notification permission required", OpenTimerPermissions, "clock");
        SettingRow(panel, "App protection", capabilities.SecureLock ? "Screen-lock authentication available" : "Unavailable · set an Android screen lock", () => OpenIntent(new Intent(Android.Provider.Settings.ActionSecuritySettings)), "lock");
        SettingRow(panel, "Depth & motion", capabilities.LowMemoryDevice ? "Off · low-memory device" : capabilities.PowerSaving ? "Off while battery saver is active" : state.LayeredInterface ? "Ready · follows Android animation settings" : "Off in Appearance", () => { dialog.Dismiss(); Navigate("Appearance"); }, "photo");
        SettingRow(panel, "Ambient theme", ambient.Available ? "Light sensor available" : "Unavailable · no light sensor", () => { dialog.Dismiss(); Navigate("Automatic theme"); }, "sun");
        SettingRow(panel, "Tilt depth", depthMotion.Available ? "Rotation sensor available" : "Unavailable · no rotation sensor", () => { dialog.Dismiss(); Navigate("Appearance"); }, "phone");
        SettingRow(panel, "Weather", string.IsNullOrEmpty(state.Weather.City) ? "Choose a city" : WeatherSummary(), () => { dialog.Dismiss(); Navigate("Weather"); }, "cloud");
        SettingRow(panel, "Agenda", AgendaReader.Allowed(this) ? "Connected · choose calendars" : "Optional · calendar access not connected", ShowCalendarChoices, "calendar");
        SettingRow(panel, "Recovery", store.RecoveryAvailable ? "A recovery copy is retained on this phone" : store.PreviousAvailable ? "Previous saved state available" : "A recovery point is created after saving", () => { dialog.Dismiss(); ShowPrivacyRecovery(); }, "file");
        Paragraph("Android " + Android.OS.Build.VERSION.Release + " · 64-bit Android build. Access and device capabilities are checked again when Launcher resumes.", panel);
    }, (footer, dialog) => footer.AddView(Button("Ready to use", () => { state.SetupCompleted = true; Save(); dialog.Dismiss(); RenderBody(); }, true), Match()));
    private void ShowPrivacyRecovery() => Sheet("Privacy & recovery", (panel, dialog) =>
    {
        ToggleRow(panel, "Hide protected app previews", state.HideProtectedPreviews, value => WithProtectedSettings(() => { state.HideProtectedPreviews = value; Save(); dialog.Dismiss(); ShowPrivacyRecovery(); }));
        Paragraph("Protected notification titles and text are hidden across Launcher and its system panel. Protected apps are excluded from new message-history collection while this is enabled. Existing history opens through authentication.", panel);
        SettingRow(panel, "Message history", state.KeepMessageHistory ? "Collecting · 30 days or 5,000 messages" : "Not collecting", () => { dialog.Dismiss(); MessageHistorySettings(); }, "message");
        SettingRow(panel, "Export a backup", "Includes notes and journeys as readable JSON. Keep it somewhere private.", () => { dialog.Dismiss(); ExportBackup(); }, "file");
        SettingRow(panel, "Restore selected content", "Preview and choose what to restore", () => { dialog.Dismiss(); ImportBackup(); }, "file");
        if (store.PreviousAvailable)
            SettingRow(panel, "Recover previous saved state", "Review before restoring", () => { try { var previous = store.LoadPrevious(); dialog.Dismiss(); ShowRestoreOptions(previous); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException) { Toast("This recovery point could not be read."); } }, "clock");
        Paragraph(store.RecoveryAvailable ? "An unreadable-state copy is preserved in Launcher's private storage. Export your current recovered content before making further changes." : "Saves replace the state file atomically and keep a previous valid copy. Layout backups exclude API keys, pairing credentials and message history.", panel);
        Paragraph("Weather contacts Open-Meteo. Enabled feeds contact their publishers and image hosts. Online search contacts the provider you configure. The optional desktop connection shares notes and the selected folder with your paired computer.", panel);
    });
}
