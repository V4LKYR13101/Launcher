using Android.Widget;

namespace LauncherApp;

public partial class MainActivity
{
    private void AddLanguageSetting(LinearLayout parent)
    {
        var selected = LanguageOverride(this);
        var name = Languages.FirstOrDefault(language => language.Tag.Equals(selected, StringComparison.OrdinalIgnoreCase))?.Name;
        SettingRow(parent, Localize(Resource.String.ui_language), name ?? Localize(Resource.String.ui_system_default), ShowLanguageChoice, "settings", Localize(Resource.String.ui_language_search_terms));
    }

    private void ShowLanguageChoice()
    {
        string current = LanguageOverride(this);
        string[] tags = ["", .. Languages.Select(language => language.Tag)];
        string[] names = [Localize(Resource.String.ui_system_default), .. Languages.Select(language => language.Name)];
        int selected = Math.Max(0, Array.FindIndex(tags, tag => tag.Equals(current, StringComparison.OrdinalIgnoreCase)));
        Choose(Localize(Resource.String.ui_language), names, index =>
        {
            if (index == selected)
                return;

            SetLanguageOverride(this, tags[index]);
            settingsQuery = "";
            homeScrollPositions.Remove(SettingsSearchScrollKey);
            Render();
        }, selected);
    }

    private static string PromptError(ArgumentException exception) => exception.Message switch
    {
        "Use a duration such as 10 min, 30s or 1h." => Localize(Resource.String.ui_use_a_duration_such_as_10_min_30s_or),
        "Use the companion address, such as https://192.168.1.10:17842" => Localize(Resource.String.ui_use_desktop_address),
        "Use a private Wi-Fi network address." => Localize(Resource.String.ui_use_private_network_address),
        _ => exception.Message
    };

    private static string DrawerName(Launcher.Core.Drawer drawer) => (drawer.Id, drawer.Name) switch
    {
        ("essentials", "Essentials") => Localize(Resource.String.ui_essentials),
        ("creativity", "Creativity") => Localize(Resource.String.ui_creativity),
        ("messaging", "Messaging") => Localize(Resource.String.ui_messaging),
        ("productivity", "Productivity") => Localize(Resource.String.ui_productivity),
        ("lifestyle", "Lifestyle") => Localize(Resource.String.ui_lifestyle),
        ("entertainment", "Entertainment") => Localize(Resource.String.ui_entertainment),
        ("social", "Social") => Localize(Resource.String.ui_social),
        ("finance", "Finance") => Localize(Resource.String.ui_finance),
        ("other", "App box") => Localize(Resource.String.ui_app_box),
        _ => drawer.Name
    };

    private static string AppearanceValue(string value) => value switch
    {
        "Dark" => Localize(Resource.String.ui_dark),
        "Light" => Localize(Resource.String.ui_light),
        "Sun" => Localize(Resource.String.ui_sun),
        "Monochrome" => Localize(Resource.String.ui_monochrome),
        "Grayscale" => Localize(Resource.String.ui_grayscale),
        "Original" => Localize(Resource.String.ui_original),
        "Top" => Localize(Resource.String.ui_top),
        "Center" => Localize(Resource.String.ui_center),
        "Bottom" => Localize(Resource.String.ui_bottom),
        "Classic" => Localize(Resource.String.ui_classic),
        "Studio" => Localize(Resource.String.ui_studio),
        "Follow Launcher" => Localize(Resource.String.ui_follow_launcher),
        "Left" => Localize(Resource.String.ui_left),
        "Right" => Localize(Resource.String.ui_right),
        _ => value
    };
}
