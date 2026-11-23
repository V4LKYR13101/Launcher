namespace LauncherApp;

public partial class MainActivity
{
    private const string SettingsSearchScrollKey = "Settings search";
    private readonly Stack<string> settingsBackPages = new();

    private static bool IsStyledSettingsPage(string name) => name is "Settings" or "Appearance" or
        "Theme & wallpaper" or "Tiles & icons" or "Motion & depth" or "Navigation & status" or
        "Apps & drawers" or "Notifications & panels" or SearchSettingsPage or "Image search" or DocumentSearchPage or SpeechSettingsPage or "People" or DownloadsSettingsPage;

    private static bool IsSettingsPage(string name) => IsStyledSettingsPage(name) || name is
        "Automatic theme" or "Updates" or "Grayscale setup" or "Integrations" or "Desktop companion" or
        "Drawers" or "Hidden" or "Permissions" or "Root settings" or "Weather";

    private void TrackSettingsNavigation(string target)
    {
        if (target == page)
            return;

        if (target == "Quick panel" && IsSettingsPage(page))
        {
            settingsBackPages.Push(page);
            return;
        }

        if (!IsSettingsPage(target))
        {
            settingsBackPages.Clear();
            return;
        }

        if (!IsSettingsPage(page))
        {
            settingsBackPages.Clear();
            settingsQuery = "";
            homeScrollPositions.Remove("Settings");
            homeScrollPositions.Remove(SettingsSearchScrollKey);
        }

        settingsBackPages.Push(page);
    }

    private bool ReturnFromSettings()
    {
        if (!settingsBackPages.TryPeek(out var previous) ||
            !(IsSettingsPage(page) || page == "Quick panel" && IsSettingsPage(previous)))
            return false;

        settingsBackPages.Pop();
        Navigate(previous, returningFromSettings: true);
        return true;
    }
}
