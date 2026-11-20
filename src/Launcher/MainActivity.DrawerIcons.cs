using Android.App;
using Android.Views;
using Android.Widget;
using Launcher.Core;
using LauncherApp.UI;
using LauncherApp.Services;
using Android.Graphics;

namespace LauncherApp;

public partial class MainActivity
{
    private View DrawerIconView(Drawer drawer)
    {
        if (drawer.IconPackage.Length > 0 && IconPacks.Get(this, drawer.IconPackage, drawer.IconResource) is { } drawable)
        {
            var image = new ImageView(this);
            image.SetScaleType(ImageView.ScaleType.FitCenter);
            if (drawable is FontIconDrawable font)
                font.SetInk(drawer.Expanded ? palette.Accent : palette.Muted);
            else if (state.IconStyle != "Original")
            {
                using var matrix = new ColorMatrix();
                matrix.SetSaturation(0);
                image.SetColorFilter(new ColorMatrixColorFilter(matrix));
            }
            image.SetImageDrawable(drawable);
            return image;
        }
        return new GlyphView(this, DrawerIcons.All.Contains(drawer.Icon) ? drawer.Icon : DrawerIcons.ForDrawer(drawer.Id), drawer.Expanded ? palette.Accent : palette.Muted);
    }
    private void PickDrawerIcon(Drawer drawer)
    {
        string selected = DrawerIcons.All.Contains(drawer.Icon) ? drawer.Icon : DrawerIcons.ForDrawer(drawer.Id);
        Sheet(drawer.Name, (panel, dialog) =>
        {
            Paragraph("Choose a symbol for this drawer.", panel);
            RootActions(panel, ("Browse icon libraries", () => { dialog.Dismiss(); PickIconPack(selection: (package, name) => { drawer.IconPackage = package; drawer.IconResource = name; }); }
            ));
            var grid = Stack();
            panel.AddView(grid, Match());
            void Rebuild()
            {
                grid.RemoveAllViews();
                for (int row = 0; row < 5; row++)
                {
                    var line = Stack(true);
                    grid.AddView(line, Match());
                    foreach (string icon in DrawerIcons.All.Skip(row * 4).Take(4))
                    {
                        bool active = icon == selected;
                        var cell = new FrameLayout(this) { ContentDescription = "Drawer symbol " + icon };
                        cell.Background = Background(palette.Panel, 12, active ? palette.Accent : null);
                        cell.AddView(new GlyphView(this, icon, active ? palette.Accent : palette.Text), new FrameLayout.LayoutParams(Dp(25), Dp(25), GravityFlags.Center));
                        cell.Click += (_, _) => { selected = icon; Rebuild(); };
                        var lp = new LinearLayout.LayoutParams(0, Dp(66), 1);
                        lp.SetMargins(Dp(4), Dp(4), Dp(4), Dp(4));
                        line.AddView(cell, lp);
                    }
                }
            }
            Rebuild();
        }, (footer, dialog) => footer.AddView(Button("Save changes", () => { drawer.Icon = selected; drawer.IconPackage = ""; drawer.IconResource = ""; Save(); dialog.Dismiss(); Render(); }, true), Match()));
    }
}
