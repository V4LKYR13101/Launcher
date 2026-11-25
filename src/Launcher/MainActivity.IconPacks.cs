using Android.Widget;
using Android.Views;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.UI;

namespace LauncherApp;

public partial class MainActivity
{
    private void PickTileStyle(AppEntry app, Action done)
    {
        Sheet(Localize(Resource.String.ui_tile_appearance), (panel, dialog) =>
        {
            Paragraph(Localize(Resource.String.ui_choose_a_surface_for_this_app_focus_and_sun), panel);
            var styles = new[] { "Default", "Accent", "Light", "Dark", "Outline" };
            LinearLayout? row = null;
            for (int i = 0; i < styles.Length; i++)
            {
                string style = styles[i];
                if (i % 2 == 0)
                {
                    row = Stack(true);
                    panel.AddView(row, Match());
                }
                var colours = TileColours(new()
                {
                    TileStyle = style,
                    Highlight = style == "Accent"
                });
                bool selected = state.Apps[app.Id].TileStyle == style;
                var tile = Stack();
                tile.SetGravity(GravityFlags.Center);
                tile.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
                tile.Background = Background(colours.Surface, 12, selected ? palette.Action : colours.Outline ? palette.Muted : palette.Divider);
                TouchFeedback(tile);
                tile.AddView(AppIcon(app, colours.Ink), new LinearLayout.LayoutParams(Dp(30), Dp(30)));
                var label = Text(style, 12, true, colours.Ink);
                label.SetPadding(0, Dp(8), 0, 0);
                tile.AddView(label);
                tile.Click += (_, _) => { var rule = state.Apps[app.Id]; rule.TileStyle = style; rule.Highlight = style == "Accent"; Save(); dialog.Dismiss(); done(); };
                var lp = new LinearLayout.LayoutParams(0, Dp(96), 1);
                lp.SetMargins(i % 2 == 0 ? 0 : Dp(5), 0, i % 2 == 0 ? Dp(5) : 0, Dp(10));
                row!.AddView(tile, lp);
            }
            if (styles.Length % 2 != 0)
                row!.AddView(new View(this), new LinearLayout.LayoutParams(0, 1, 1));
        });
    }
    private void PickIconPack(AppEntry? app = null, Action<string, string>? selection = null)
    {
        var packs = IconPacks.Installed(this);
        Sheet(selection is not null ? Localize(Resource.String.ui_drawer_icon) : app is null ? Localize(Resource.String.ui_icon_pack) : Localize(Resource.String.ui_app_icon), (panel, dialog) =>
        {
            panel.AddView(Button(selection is not null ? Localize(Resource.String.ui_use_geometric_symbol) : app is null ? Localize(Resource.String.ui_use_apps_own_icons) : Localize(Resource.String.ui_restore_original_icon), () =>
            {
                if (selection is not null)
                    selection("", "");
                else if (app is null)
                    state.IconPack = "";
                else
                {
                    state.Apps[app.Id].UseOriginalIcon = true;
                    state.Apps[app.Id].IconPackage = "";
                    state.Apps[app.Id].IconResource = "";
                }
                iconCache.Clear();
                iconGeometryCache.Clear();
                iconQualityCache.Clear();
                Save();
                dialog.Dismiss();
                Render();
            }), Match());
            if (app is not null && selection is null && state.IconPack.Length > 0)
                RootActions(panel, (Localize(Resource.String.ui_use_global_selection), () => { var rule = state.Apps[app.Id]; rule.UseOriginalIcon = false; rule.IconPackage = ""; rule.IconResource = ""; iconCache.Clear(); iconGeometryCache.Clear(); iconQualityCache.Clear(); Save(); dialog.Dismiss(); Render(); }
                ));
            SectionCaption(panel, Localize(Resource.String.ui_built_in_libraries));
            foreach (string family in new[] { "Google Material", "Phosphor", "Bootstrap" })
            {
                var variants = BuiltinIcons.Libraries.Where(l => l.Label.StartsWith(family, StringComparison.Ordinal)).ToArray();
                SettingRow(panel, family, family == "Google Material" ? Localize(Resource.String.ui_outlined_rounded_filled_sharp) : family == "Phosphor" ? Localize(Resource.String.ui_light_thin_regular_bold_fill) : Localize(Resource.String.ui_outline_filled_brand_icons), () =>
                {
                    dialog.Dismiss();
                    void Select(IconPack pack)
                    {
                        if (app is null && selection is null)
                        {
                            state.IconPack = pack.Package;
                            iconCache.Clear();
                            iconGeometryCache.Clear();
                            iconQualityCache.Clear();
                            Save();
                            Render();
                        }
                        else
                            PickPackIcon(app, pack, selection);
                    }
                    if (variants.Length == 1)
                        Select(variants[0]);
                    else
                        Choose(family + Localize(Resource.String.ui_style), variants.Select(v => v.Label.Split('·').Last().Trim()).ToArray(), i => Select(variants[i]));
                }, "photo");
            }
            SectionCaption(panel, Localize(Resource.String.ui_installed_icon_packs));
            if (packs.Length == 0)
                Paragraph(Localize(Resource.String.ui_installed_adw_nova_or_go_compatible_packs_appear_here), panel);
            foreach (var pack in packs)
                SettingRow(panel, pack.Label, pack.Package, () =>
            {
                dialog.Dismiss();
                if (app is null && selection is null)
                {
                    state.IconPack = pack.Package;
                    iconCache.Clear();
                    iconGeometryCache.Clear();
                    iconQualityCache.Clear();
                    Save();
                    Render();
                }
                else
                    PickPackIcon(app, pack, selection);
            }, "photo");
            Paragraph(selection is not null ? Localize(Resource.String.ui_choose_any_glyph_for_this_drawer) : app is null ? Localize(Resource.String.ui_libraries_use_recognised_app_matches_unmatched_apps_keep_their) : Localize(Resource.String.ui_choose_an_icon_from_any_library_or_installed_pack), panel);
        });
    }
    private void PickPackIcon(AppEntry? app, IconPack pack, Action<string, string>? selection = null)
    {
        string[] names;
        try
        {
            names = IconPacks.Names(this, pack.Package);
        }
        catch (Exception e) when (e is Java.Lang.RuntimeException or Android.Content.PM.PackageManager.NameNotFoundException or IOException or System.Xml.XmlException) { Toast(Localize(Resource.String.ui_this_pack_does_not_expose_a_readable_icon_list)); return; }
        string[] common = ["home", "house", "search", "magnifying-glass", "settings", "gear", "person", "user", "phone", "call", "telephone", "chat", "chat-circle", "mail", "envelope", "camera_alt", "camera", "calendar_month", "calendar", "folder_open", "folder-open", "folder", "image", "music_note", "music-notes", "favorite_border", "heart"];
        string? suggested = app is not null && BuiltinIcons.IsBuiltin(pack.Package) ? BuiltinIcons.Match(this, pack.Package, app) : null;
        names = names.OrderBy(n => n == suggested ? -1 : Array.IndexOf(common, n) is var rank && rank >= 0 ? rank : common.Length).ThenBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
        EditText? search = null;
        TextView? count = null;
        Action? draw = null, pending = null;
        int limit = 60;
        var sheet = Sheet(pack.Label, (panel, dialog) =>
        {
            var grid = Stack();
            panel.AddView(grid, Match());
            draw = () =>
            {
                grid.RemoveAllViews();
                var terms = (search?.Text ?? "").Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                var found = names.Where(n => terms.All(t => n.Contains(t, StringComparison.OrdinalIgnoreCase))).ToArray();
                if (count is not null)
                    count.Text = found.Length + Localize(Resource.String.ui_icons) + (terms.Length == 0 ? Localize(Resource.String.ui_common_choices_first) : Localize(Resource.String.ui_matching_your_search));
                if (found.Length == 0)
                {
                    Paragraph(names.Length == 0 ? Localize(Resource.String.ui_this_pack_has_no_browsable_icons_choose_another_library) : Localize(Resource.String.ui_no_icons_match_try_a_shorter_name), grid);
                    return;
                }
                LinearLayout? row = null;
                foreach (var name in found.Take(limit).Select((name, index) => (name, index)))
                {
                    if (name.index % 4 == 0)
                    {
                        row = Stack(true);
                        row.WeightSum = 4;
                        grid.AddView(row, Match());
                    }
                    bool active = app is not null && state.Apps[app.Id].IconPackage == pack.Package && state.Apps[app.Id].IconResource == name.name;
                    var cell = Stack();
                    cell.SetGravity(GravityFlags.Center);
                    cell.SetPadding(Dp(3), Dp(4), Dp(3), Dp(4));
                    cell.ContentDescription = name.name.Replace('_', ' ').Replace('-', ' ') + ", " + pack.Label;
                    cell.Background = Background(active ? palette.Panel : Android.Graphics.Color.Transparent, 10, active ? palette.Action : null);
                    var icon = new ImageView(this);
                    icon.SetScaleType(ImageView.ScaleType.FitCenter);
                    var drawable = IconPacks.Get(this, pack.Package, name.name);
                    if (drawable is FontIconDrawable font)
                        font.SetInk(active ? palette.Action : palette.Text);
                    icon.SetImageDrawable(drawable);
                    cell.AddView(icon, new LinearLayout.LayoutParams(Dp(34), Dp(34)));
                    var label = Text(name.name.Replace('_', ' ').Replace('-', ' '), 9, color: active ? palette.Action : palette.Muted);
                    label.Gravity = GravityFlags.Center;
                    label.SetPadding(0, Dp(4), 0, 0);
                    label.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                    label.SetMaxLines(2);
                    cell.AddView(label, Match(Math.Max(Dp(26), Dp(9 * FontScale * 2.5f))));
                    TouchFeedback(cell, 10);
                    cell.Click += (_, _) => { if (selection is not null) selection(pack.Package, name.name); else { var rule = state.Apps[app!.Id]; rule.UseOriginalIcon = false; rule.IconPackage = pack.Package; rule.IconResource = name.name; } iconCache.Clear(); iconGeometryCache.Clear(); iconQualityCache.Clear(); Save(); dialog.Dismiss(); Render(); };
                    var lp = new LinearLayout.LayoutParams(0, Math.Max(Dp(76), Dp(46 + 9 * FontScale * 2.5f)), 1);
                    lp.SetMargins(Dp(2), 0, Dp(2), Dp(4));
                    row!.AddView(cell, lp);
                }
                if (found.Length > limit)
                {
                    Gap(grid, 8);
                    grid.AddView(Button(Localize(Resource.String.ui_show_more_icons), () => { limit += 60; draw?.Invoke(); }), Match());
                }
            };
            draw();
        }, fixedHeader: (header, dialog) =>
        {
            search = new EditText(this) { Hint = Localize(Resource.String.ui_search_icons_by_name), TextSize = 15, Typeface = regular };
            search.SetTextColor(palette.Text);
            search.SetHintTextColor(palette.Muted);
            search.Background = Background(palette.Card, 12, palette.Divider);
            search.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            search.SetSingleLine(true);
            StyleSearchEntry(search, 12, inSheet: true);
            header.AddView(search, Match(Math.Max(Dp(48), Dp(15 * FontScale * 1.3f + 20))));
            count = Text(names.Length + Localize(Resource.String.ui_icons_common_choices_first), 11, color: palette.Muted);
            count.SetPadding(Dp(2), Dp(10), Dp(2), 0);
            header.AddView(count, Match());
            search.TextChanged += (_, _) => { limit = 60; if (pending is not null) handler.RemoveCallbacks(pending); pending = () => draw?.Invoke(); handler.PostDelayed(pending, 90); };
        });
        sheet.DismissEvent += (_, _) => { if (pending is not null) handler.RemoveCallbacks(pending); draw = null; };
    }
    private (Android.Graphics.Color Surface, Android.Graphics.Color Ink, bool Outline) TileColours(AppRule rule)
    {
        string style = rule.TileStyle == "Default" && rule.Highlight ? "Accent" : rule.TileStyle;
        var surface = style switch
        {
            "Accent" => palette.Accent,
            "Light" => Android.Graphics.Color.ParseColor("#EEEEEA"),
            "Dark" => Android.Graphics.Color.ParseColor("#171717"),
            _ => palette.Card
        };
        bool outline = style == "Outline" || state.Focus || state.Theme == "Sun";
        if (outline && style != "Accent")
            surface = palette.Base;
        var ink = ColorContrast.UseDarkText($"#{surface.R:X2}{surface.G:X2}{surface.B:X2}") ? Android.Graphics.Color.Black : Android.Graphics.Color.White;
        return (surface, ink, outline);
    }
}
