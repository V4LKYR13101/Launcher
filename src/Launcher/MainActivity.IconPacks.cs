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
        Sheet("Tile appearance", (panel, dialog) =>
        {
            Paragraph("Choose a surface for this app. Focus and Sun can apply their outlined treatment.", panel);
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
        Sheet(selection is not null ? "Drawer icon" : app is null ? "Icon pack" : "App icon", (panel, dialog) =>
        {
            panel.AddView(Button(selection is not null ? "Use geometric symbol" : app is null ? "Use apps’ own icons" : "Restore original icon", () =>
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
                RootActions(panel, ("Use global selection", () => { var rule = state.Apps[app.Id]; rule.UseOriginalIcon = false; rule.IconPackage = ""; rule.IconResource = ""; iconCache.Clear(); iconGeometryCache.Clear(); iconQualityCache.Clear(); Save(); dialog.Dismiss(); Render(); }
                ));
            SectionCaption(panel, "Built-in libraries");
            foreach (string family in new[] { "Google Material", "Phosphor", "Bootstrap" })
            {
                var variants = BuiltinIcons.Libraries.Where(l => l.Label.StartsWith(family, StringComparison.Ordinal)).ToArray();
                SettingRow(panel, family, family == "Google Material" ? "Outlined, rounded, filled & sharp" : family == "Phosphor" ? "Light, thin, regular, bold & fill" : "Outline, filled & brand icons", () =>
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
                        Choose(family + " style", variants.Select(v => v.Label.Split('·').Last().Trim()).ToArray(), i => Select(variants[i]));
                }, "photo");
            }
            SectionCaption(panel, "Installed icon packs");
            if (packs.Length == 0)
                Paragraph("Installed ADW, Nova or Go-compatible packs appear here. Reopen this picker after installing a pack.", panel);
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
            Paragraph(selection is not null ? "Choose any glyph for this drawer." : app is null ? "Libraries use recognised app matches. Unmatched apps keep their own icon; hold a tile to choose a specific icon." : "Choose an icon from any library or installed pack. Your selection applies only to this app.", panel);
        });
    }
    private void PickPackIcon(AppEntry? app, IconPack pack, Action<string, string>? selection = null)
    {
        string[] names;
        try
        {
            names = IconPacks.Names(this, pack.Package);
        }
        catch (Exception e) when (e is Java.Lang.RuntimeException or Android.Content.PM.PackageManager.NameNotFoundException or IOException or System.Xml.XmlException) { Toast("This pack does not expose a readable icon list."); return; }
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
                    count.Text = found.Length + " icons" + (terms.Length == 0 ? " · common choices first" : " · matching your search");
                if (found.Length == 0)
                {
                    Paragraph(names.Length == 0 ? "This pack has no browsable icons. Choose another library or pack." : "No icons match. Try a shorter name.", grid);
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
                    grid.AddView(Button("Show more icons", () => { limit += 60; draw?.Invoke(); }), Match());
                }
            };
            draw();
        }, fixedHeader: (header, dialog) =>
        {
            search = new EditText(this) { Hint = "Search icons by name", TextSize = 15, Typeface = regular };
            search.SetTextColor(palette.Text);
            search.SetHintTextColor(palette.Muted);
            search.Background = Background(palette.Card, 12, palette.Divider);
            search.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            search.SetSingleLine(true);
            StyleSearchEntry(search, 12, inSheet: true);
            header.AddView(search, Match(Math.Max(Dp(48), Dp(15 * FontScale * 1.3f + 20))));
            count = Text(names.Length + " icons · common choices first", 11, color: palette.Muted);
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
