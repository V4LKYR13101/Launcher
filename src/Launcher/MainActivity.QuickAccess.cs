using Android.Views;
using Android.Widget;
using Launcher.Core;

namespace LauncherApp;

public partial class MainActivity
{
    private AppEntry[] QuickAccessEntries(AppEntry[] visible)
    {
        if (state.QuickAccessApps is { } saved)
        {
            var entries = visible.ToDictionary(app => app.Id);
            return saved.Where(entries.ContainsKey).Select(id => entries[id]).ToArray();
        }

        var favorites = visible.Where(app => state.Apps[app.Id].Pinned).Take(5).ToArray();
        return favorites.Length > 0 ? favorites : visible.Where(app => state.Apps[app.Id].DrawerId == "essentials").Take(5).ToArray();
    }

    private void ShowQuickAccess()
    {
        var available = AppOrganization.Visible(state, apps).Where(app => !app.PrivateProfile).ToArray();
        var selected = QuickAccessEntries(available).Select(app => app.Id).ToList();
        var ordered = available.OrderBy(app => AppOrganization.DisplayName(state, app), StringComparer.CurrentCultureIgnoreCase).ToArray();
        EditText search = null!;
        TextView count = null!;
        Sheet(Localize(Resource.String.ui_quick_access), (list, dialog) =>
        {
            void RefreshChoices()
            {
                list.RemoveAllViews();
                string query = search.Text?.Trim() ?? "";
                var matches = ordered.Where(app => AppOrganization.DisplayName(state, app).Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
                if (matches.Length == 0)
                    Paragraph(query.Length == 0 ? Localize(Resource.String.ui_no_apps_are_available) : Localize(Resource.String.ui_no_matching_apps), list);
                foreach (var app in matches)
                {
                    var toggle = new Switch(this)
                    {
                        Text = AppOrganization.DisplayName(state, app),
                        Checked = selected.Contains(app.Id),
                        TextSize = 14,
                        Typeface = regular,
                        ThumbTintList = AccentTint(),
                        TrackTintList = SwitchTrackTint()
                    };
                    toggle.SetTextColor(palette.Text);
                    toggle.SetMinimumHeight(Dp(52));
                    BindToggleHaptics(toggle);
                    toggle.CheckedChange += (_, e) =>
                    {
                        if (e.IsChecked)
                        {
                            if (selected.Count >= 5)
                            {
                                toggle.Checked = false;
                                Toast(Localize(Resource.String.ui_remove_an_app_before_adding_another));
                                return;
                            }
                            selected.Add(app.Id);
                        }
                        else if (!selected.Remove(app.Id))
                            return;

                        state.QuickAccessApps = [.. selected];
                        count.Text = Localize(Resource.String.ui_value_of_5_selected, selected.Count);
                        Save();
                    };
                    list.AddView(toggle, Match());
                }
            }
            search.TextChanged += (_, _) => RefreshChoices();
            RefreshChoices();
            dialog.DismissEvent += (_, _) =>
            {
                if (page == "Tiles" && !searching && !IsDestroyed)
                    RenderBody();
            };
        }, (footer, dialog) => footer.AddView(Button(Localize(Resource.String.ui_done), dialog.Dismiss, true), Match()),
        fixedHeader: (header, _) =>
        {
            search = new EditText(this) { Hint = Localize(Resource.String.ui_find_an_app), TextSize = 15, ContentDescription = Localize(Resource.String.ui_find_an_app) };
            search.SetSingleLine(true);
            StyleSearchEntry(search, 14, inSheet: true);
            search.SetMinimumHeight(Dp(48));
            header.AddView(search, Match());
            count = Text(Localize(Resource.String.ui_value_of_5_selected, selected.Count), 12, color: palette.Muted);
            count.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
            header.AddView(count, Match());
        });
    }
}
