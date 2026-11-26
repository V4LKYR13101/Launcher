using Android.Views;
using Android.Widget;

namespace LauncherApp;

public partial class MainActivity
{
    private const string GroupedDrawerPage = "App drawer";
    private const string AllAppsDrawerPage = "All apps drawer";
    private string lastDrawerPage = GroupedDrawerPage;
    private View? launcherStatusRow;

    private static bool IsDrawerPage(string value) => value is GroupedDrawerPage or AllAppsDrawerPage;
    private bool IsAppsSurface => page == "Tiles" || IsDrawerPage(page);

    private void AddDrawerHeader()
    {
        var header = Stack(true);
        header.SetGravity(GravityFlags.CenterVertical);
        header.SetPadding(Dp(12), Dp(8), Dp(12), 0);
        header.AddView(IconButton("down", Localize(Resource.String.ui_close_app_drawer), () => Navigate("Tiles")), new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        var title = Text(page == AllAppsDrawerPage ? Localize(Resource.String.ui_all_apps) : Localize(Resource.String.ui_apps), 24, true);
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            title.AccessibilityHeading = true;
        title.SetMaxLines(2);
        header.AddView(title, new LinearLayout.LayoutParams(0, -2, 1));
        foreach (var (target, glyph, label) in new[] { (GroupedDrawerPage, "filter", Localize(Resource.String.ui_grouped_apps)), (AllAppsDrawerPage, "grid", Localize(Resource.String.ui_all_apps_grid)) })
        {
            var button = IconButton(glyph, label, () => { if (page != target) Navigate(target); });
            button.Selected = page == target;
            if (button.Selected)
                button.Background = Background(palette.Card, 20);
            header.AddView(button, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        }
        shell.AddView(header, Match());
    }
}
