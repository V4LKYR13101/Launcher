using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.UI;
using Uri = Android.Net.Uri;

namespace LauncherApp;

public partial class MainActivity
{
    private readonly Dictionary<string, Android.Animation.ValueAnimator> drawerAnimations = new();
    private bool drawerTouchActive;
    private void StopDrawerAnimations()
    {
        foreach (var animation in drawerAnimations.Values.ToArray())
            animation.Cancel();
        drawerAnimations.Clear();
        drawerTouchActive = false;
    }
    private void FoldDrawer(Drawer drawer, LinearLayout content, View heading, Action populate, bool? force = null)
    {
        ResetDeviceDepth();
        int from = content.Visibility == ViewStates.Gone ? 0 : content.Height;
        if (drawerAnimations.Remove(drawer.Id, out var previous))
            previous.Cancel();
        bool wasExpanded = drawer.Expanded;
        drawer.Expanded = force ?? !drawer.Expanded;
        Save();
        if (force.HasValue && wasExpanded != drawer.Expanded)
            Haptic(heading, HapticCue.Commit);
        bool expanding = drawer.Expanded;
        heading.ContentDescription = drawer.Name + (expanding ? ", expanded" : ", collapsed");
        if (content.ChildCount == 0)
            populate();
        content.Visibility = ViewStates.Visible;
        int width = body.Width > 0 ? body.Width : Resources!.DisplayMetrics!.WidthPixels - Dp(32);
        content.Measure(View.MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly), View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
        int natural = content.MeasuredHeight, target = expanding ? natural : 0;
        var layout = content.LayoutParameters!;
        layout.Height = from;
        content.LayoutParameters = layout;
        content.PivotY = 0;
        content.SetCameraDistance(Dp(1200));
        heading.Selected = expanding;
        if (!LayerMotionEnabled)
        {
            content.Visibility = expanding ? ViewStates.Visible : ViewStates.Gone;
            content.Alpha = 1;
            content.RotationX = 0;
            PaintDrawerCells(content, 1);
            layout.Height = -2;
            content.LayoutParameters = layout;
            return;
        }
        var animation = Android.Animation.ValueAnimator.OfInt(from, target)!;
        animation.SetDuration(240);
        animation.SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.5f));
        bool cancelled = false;
        animation.AnimationCancel += (_, _) => cancelled = true;
        animation.Update += (_, e) =>
        {
            int height = ((Java.Lang.Integer)e.Animation!.AnimatedValue!).IntValue();
            var lp = content.LayoutParameters!;
            lp.Height = height;
            content.LayoutParameters = lp;
            float fraction = natural > 0 ? Math.Clamp(height / (float)natural, 0, 1) : 1;
            content.Alpha = state.LayeredInterface ? .35f + .65f * fraction : 1;
            content.RotationX = state.LayeredInterface ? -9 * (1 - fraction) : 0;
            PaintDrawerCells(content, fraction);
        };
        animation.AnimationEnd += (_, _) =>
        {
            if (cancelled)
                return;
            content.Visibility = expanding ? ViewStates.Visible : ViewStates.Gone;
            content.Alpha = 1;
            content.RotationX = 0;
            PaintDrawerCells(content, 1);
            var lp = content.LayoutParameters!;
            lp.Height = -2;
            content.LayoutParameters = lp;
            drawerAnimations.Remove(drawer.Id);
        };
        drawerAnimations[drawer.Id] = animation;
        animation.Start();
    }
    private void RenderTiles()
    {
        homeFavoritesDock = null;
        RenderHomeWeather();
        AddMediaCard();
        if (state.Focus)
        {
            var focus = Button("Ⅱ  Focus is on", () => { state.Focus = false; state.FocusEndsAt = 0; Save(); Render(); });
            body.AddView(focus, Match());
            Gap(body, 12);
        }

        body.AddView(new Space(this), new LinearLayout.LayoutParams(-1, 0, 1));
        var visible = AppOrganization.Visible(state, apps).Where(a => !a.PrivateProfile).ToArray();
        AddHomeDock(QuickAccessEntries(visible));
        var open = IconButton("up", "Open app drawer", () => Navigate(lastDrawerPage));
        BindHoldHaptics(open);
        open.LongClick += (_, _) => ShowQuickAccess();
        homeDrawerMarker = open;
        open.LayoutChange += (_, _) => open.Post(() =>
        {
            PositionHomeBattery();
            PositionHomeDownloads();
        });
        var dockHandle = new FrameLayout(this);
        dockHandle.AddView(open, new FrameLayout.LayoutParams(Dp(56), Dp(48), GravityFlags.Center));
        body.AddView(dockHandle, Match());
        AddHomeDownloads();
    }

    private void RenderAppDrawer()
    {
        if (page == GroupedDrawerPage)
            body.SetGravity(state.Alignment switch
            {
                "Bottom" => GravityFlags.Bottom,
                "Center" => GravityFlags.CenterVertical,
                _ => GravityFlags.Top
            });

        if (apps.Count == 0)
        {
            Paragraph(loading ? "Finding your apps…" : "Installed apps will appear here.");
            return;
        }
        var visible = AppOrganization.Visible(state, apps).Where(a => !a.PrivateProfile).ToArray();
        if (page == AllAppsDrawerPage)
            AddTileGrid(body, visible.OrderBy(a => AppOrganization.DisplayName(state, a), StringComparer.CurrentCultureIgnoreCase));
        else
            AddGroupedApps(visible);
        if (visible.Length == 0)
            Paragraph("No apps are visible. Restore hidden apps in Settings.");
        RenderPrivateSpace();
    }

    private void AddGroupedApps(AppEntry[] visible)
    {
        bool hasUsage = AppCatalog.HasUsageAccess(this);
        foreach (var drawer in state.Drawers)
        {
            var contents = visible.Where(a => state.Apps[a.Id].DrawerId == drawer.Id).ToArray();
            if (contents.Length == 0)
                continue;
            var heading = new FrameLayout(this) { ContentDescription = drawer.Name + (drawer.Expanded ? ", expanded" : ", collapsed") };
            int detailWidth = (int)(54 + Math.Max(0, FontScale - 1) * 24), headingInset = FontScale > 1.15f ? detailWidth + 8 : 40;
            var name = Text(drawer.Name, state.CenterHeadings ? 21 : 23, true);
            name.Gravity = state.CenterHeadings ? GravityFlags.Center : GravityFlags.CenterVertical;
            name.SetPadding(state.CenterHeadings ? Dp(headingInset) : Dp(32), 0, state.CenterHeadings ? Dp(headingInset) : Dp(detailWidth + 8), 0);
            name.SetSingleLine(true);
            name.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            heading.AddView(name, new FrameLayout.LayoutParams(-1, -1));
            var drawerIcon = DrawerIcons.All.Contains(drawer.Icon) ? drawer.Icon : DrawerIcons.ForDrawer(drawer.Id);
            heading.AddView(DrawerIconView(drawer), new FrameLayout.LayoutParams(Dp(19), Dp(19), GravityFlags.Left | GravityFlags.CenterVertical));
            bool showDuration = hasUsage && state.ShowUsage;
            var detail = Text(showDuration ? AppOrganization.FormatDuration(contents.Select(a => a.PackageName).Distinct().Sum(p => usage.GetValueOrDefault(p))) : contents.Length.ToString(), 9, true, palette.Muted);
            detail.ContentDescription = showDuration ? "Drawer usage " + detail.Text : Counted(contents.Length, "app");
            if (!showDuration)
            {
                detail.Background = Surface(palette.Card, 10);
                detail.SetPadding(Dp(6), 0, Dp(6), 0);
            }
            detail.SetSingleLine(true);
            detail.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            detail.Gravity = GravityFlags.Right | GravityFlags.CenterVertical;
            heading.AddView(detail, new FrameLayout.LayoutParams(Dp(detailWidth), showDuration ? -1 : Dp(Math.Max(28, 9 * FontScale * 1.3f + 10)), GravityFlags.Right | GravityFlags.CenterVertical));
            heading.Selected = drawer.Expanded;
            TouchFeedback(heading, 10);
            var drawerContent = Stack();
            drawerContent.SetClipChildren(true);
            void Populate()
            {
                AddTileGrid(drawerContent, contents);
                Gap(drawerContent, 12);
            }
            heading.Click += (_, _) => FoldDrawer(drawer, drawerContent, heading, Populate);
            BindHoldHaptics(heading);
            heading.LongClick += (_, _) => EditDrawer(drawer);
            int dragStart = 0, naturalHeight = 0;
            bool startedExpanded = false;
            if (state.DragDrawerHeadings)
                heading.SetOnTouchListener(new DrawerPullListener(Dp(8), () => drawer.Expanded, () =>
            {
                if (drawerAnimations.Remove(drawer.Id, out var running))
                    running.Cancel();
                startedExpanded = drawer.Expanded;
                dragStart = drawerContent.Visibility == ViewStates.Gone ? 0 : drawerContent.Height;
                if (drawerContent.ChildCount == 0)
                    Populate();
                drawerContent.Visibility = ViewStates.Visible;
                var lp = drawerContent.LayoutParameters!;
                lp.Height = -2;
                drawerContent.LayoutParameters = lp;
                drawerContent.Measure(View.MeasureSpec.MakeMeasureSpec(body.Width, MeasureSpecMode.Exactly), View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
                naturalHeight = drawerContent.MeasuredHeight;
            }, dy =>
            {
                int height = (int)Math.Clamp(dragStart + dy, 0, naturalHeight);
                var lp = drawerContent.LayoutParameters!;
                lp.Height = height;
                drawerContent.LayoutParameters = lp;
                float fraction = height / (float)Math.Max(1, naturalHeight);
                drawerContent.Alpha = LayerMotionEnabled ? .35f + .65f * fraction : 1;
                drawerContent.PivotY = 0;
                drawerContent.RotationX = LayerMotionEnabled ? -7 * (1 - fraction) : 0;
            }, (dy, cancel) =>
            {
                bool open = cancel ? startedExpanded : Math.Abs(dy) > Dp(32) ? dy > 0 : startedExpanded;
                FoldDrawer(drawer, drawerContent, heading, Populate, open);
            }, active => drawerTouchActive = active));
            var section = Stack();
            section.Tag = "drawer:" + drawer.Id;
            depthHeadings.Add(heading);
            section.AddView(heading, Match(Math.Max(Dp(48), Dp(23 * FontScale * 1.3f))));
            section.AddView(drawerContent, Match());
            body.AddView(section, Match());
            if (drawer.Expanded)
                Populate();
            else
                drawerContent.Visibility = ViewStates.Gone;
        }
    }

    private void AddHomeDock(AppEntry[] entries)
    {
        if (entries.Length > 0)
        {
            var dock = Stack(true);
            homeFavoritesDock = dock;
            dock.LayoutChange += (_, _) => dock.Post(PositionHomeDownloads);
            dock.SetGravity(GravityFlags.Center);
            var ink = AccentGlowDrawable.Blend(palette.Muted, palette.Accent, .42f);
            int cellWidth = Math.Clamp((Resources!.Configuration!.ScreenWidthDp - 32) / 5, 40, 54);
            foreach (var app in entries)
            {
                var icon = new FrameLayout(this)
                {
                    ContentDescription = AppOrganization.DisplayName(state, app) + (state.Apps[app.Id].Pinned ? ", pinned" : ""),
                    Tag = "app-tile:" + app.Id
                };
                var tile = new FrameLayout(this);
                if (ApplyGlassCard(tile, 12) && PackageLocked(app.PackageName))
                    tile.Background = new FingerprintDrawable(Color.Transparent, palette.Accent, Dp(12));
                var art = AppIcon(app, ink);
                if (state.IconStyle == "Monochrome" && art is ImageView image)
                    image.ImageTintList = Android.Content.Res.ColorStateList.ValueOf(ink);
                tile.AddView(art, new FrameLayout.LayoutParams(Dp(20), Dp(20), GravityFlags.Center));
                AddBadge(tile, app.PackageName, compact: true);
                icon.AddView(tile, new FrameLayout.LayoutParams(Dp(36), Dp(36), GravityFlags.Center));
                TouchFeedback(icon, 12);
                BindHoldHaptics(icon);
                icon.Click += (_, _) => Launch(app);
                icon.LongClick += (_, _) => InspectAppTile(app, icon);
                dock.AddView(icon, new LinearLayout.LayoutParams(Dp(cellWidth), Dp(48)));
            }
            body.AddView(dock, Match());
        }
    }

    private int EffectiveColumns => Math.Min(state.Columns, FontScale >= 1.8f ? 2 : FontScale >= 1.3f ? 3 : state.Columns);
    private int RequestedTileIconSize(int percent, bool wide = false) => Math.Min((int)Math.Round((wide ? 44 : EffectiveColumns >= 6 ? 38 : EffectiveColumns == 5 ? 44 : 52) * percent / 100d),
        Math.Max(24, wide ? (Math.Min(720, Resources!.Configuration!.ScreenWidthDp) - 32) * 2 / EffectiveColumns - 72 : (Math.Min(720, Resources!.Configuration!.ScreenWidthDp) - 32) / EffectiveColumns - 20));
    private int TileIconSize => RequestedTileIconSize(state.TileIconScale);
    private Action<int> FitTileIcon(FrameLayout slot, View icon, int size)
    {
        int desired = size;
        slot.AddView(icon, new FrameLayout.LayoutParams(Dp(size), Dp(size), GravityFlags.Center));
        void Resize()
        {
            int available = slot.Width > 0 && slot.Height > 0 ? Math.Min(slot.Width, slot.Height) : Dp(desired);
            int fit = Math.Min(Dp(desired), available);
            if (fit <= 0)
                return;
            if (icon.LayoutParameters is FrameLayout.LayoutParams lp && (lp.Width != fit || lp.Height != fit))
            {
                lp.Width = lp.Height = fit;
                icon.LayoutParameters = lp;
            }
            if (icon is TextView initials)
                initials.SetTextSize(Android.Util.ComplexUnitType.Px, fit * .5f);
        }
        slot.LayoutChange += (_, _) => Resize();
        return value => { desired = value; Resize(); };
    }
    private float DisplayTileHeight
    {
        get
        {
            if (FontScale <= 1.1f)
                return state.TileHeight;
            int icon = RequestedTileIconSize(100);
            float text = (state.ShowLabels ? (EffectiveColumns >= 5 ? 10 : 11) * 2 * FontScale * 1.2f : 0) + (state.ShowUsage ? 9 * FontScale * 1.2f + 3 : 0);
            return Math.Max(state.TileHeight + (FontScale - 1) * ((state.ShowLabels ? 26 : 0) + (state.ShowUsage ? 12 : 0)), icon + 12 + text);
        }
    }
    private void AddTileGrid(LinearLayout parent, IEnumerable<AppEntry> entries)
    {
        LinearLayout? row = null;
        int used = 0;
        void Finish()
        {
            if (row is null)
                return;
            if (used < EffectiveColumns)
                row.AddView(new View(this), new LinearLayout.LayoutParams(0, 1, EffectiveColumns - used));
        }
        foreach (var app in entries)
        {
            int span = state.Apps[app.Id].Wide ? 2 : 1;
            if (row is null || used + span > EffectiveColumns)
            {
                Finish();
                row = Stack(true);
                row.WeightSum = EffectiveColumns;
                parent.AddView(row, Match());
                used = 0;
            }
            var cell = new FrameLayout(this);
            var inner = new FrameLayout.LayoutParams(-1, -1);
            inner.SetMargins(Dp(4), Dp(4), Dp(4), Dp(4));
            cell.AddView(AppTile(app), inner);
            row.AddView(cell, new LinearLayout.LayoutParams(0, Dp(DisplayTileHeight + 8), span));
            used += span;
        }
        Finish();
    }

    private View AppIcon(AppEntry app, Color color)
    {
        var icon = new ImageView(this);
        icon.SetScaleType(ImageView.ScaleType.FitCenter);
        try
        {
            if (!iconCache.TryGetValue(app.Id, out var cached))
            {
                cached = (app.ShortcutId.Length > 0 || app.ProfileSerial >= 0 ? ProfileCatalog.Icon(this, app) : null) ?? IconPacks.ForApp(this, state, app) ?? PackageManager!.GetActivityIcon(new ComponentName(app.PackageName, app.ActivityName));
                iconCache[app.Id] = cached;
                iconQualityCache[app.Id] = (OperatingSystem.IsAndroidVersionAtLeast(33) && cached is AdaptiveIconDrawable a && a.Monochrome is { } mono && IconQuality.UsableMonochrome(mono), cached is not FontIconDrawable && state.Apps[app.Id].IconPackage.Length == 0 && IconQuality.Blank(cached));
            }
            var actual = cached.GetConstantState()?.NewDrawable(Resources)?.Mutate() ?? cached.Mutate();
            if (actual is FontIconDrawable fontIcon)
            {
                fontIcon.SetInk(color);
                icon.SetImageDrawable(fontIcon);
                return icon;
            }
            if (state.IconStyle == "Monochrome" && OperatingSystem.IsAndroidVersionAtLeast(33)
                && actual is AdaptiveIconDrawable adaptive && adaptive.Monochrome is { } monochrome && iconQualityCache.GetValueOrDefault(app.Id).Mono)
            {
                var drawable = monochrome.Mutate()!;
                drawable.SetTint(color);
                icon.SetImageDrawable(OpticallySized(app.Id + ":mono", drawable));
                return icon;
            }
            if (iconQualityCache.GetValueOrDefault(app.Id).Blank)
            {
                var initials = Text(string.Concat(app.Label.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => w[0])).ToUpperInvariant(), 23, true, color);
                initials.Gravity = GravityFlags.Center;
                return initials;
            }
            icon.SetImageDrawable(OpticallySized(app.Id + ":full", actual));
            if (state.IconStyle != "Original")
            {
                using var matrix = new ColorMatrix();
                matrix.SetSaturation(0);
                icon.SetColorFilter(new ColorMatrixColorFilter(matrix));
            }
        }
        catch (Android.Content.PM.PackageManager.NameNotFoundException)
        {
            var initials = string.Concat(app.Label.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => w[0])).ToUpperInvariant();
            var fallback = Text(initials, 16, true, color);
            fallback.Gravity = GravityFlags.Center;
            return fallback;
        }
        return icon;
    }

    private Drawable OpticallySized(string key, Drawable drawable)
    {
        if (!iconGeometryCache.TryGetValue(key, out var geometry))
            iconGeometryCache[key] = geometry = IconGeometry.Measure(drawable);
        return new OpticalIconDrawable(drawable, geometry);
    }

    private View AppTile(AppEntry app)
    {
        var rule = state.Apps[app.Id];
        var colours = TileColours(rule);
        var foreground = colours.Ink;
        bool locked = PackageLocked(app.PackageName);
        bool outlined = colours.Outline;
        var tile = Stack();
        var surface = colours.Surface;
        var border = outlined && !rule.Highlight ? palette.Muted : (Color?)null;
        bool customTint = rule.TileStyle is "Light" or "Dark" or "Accent" || rule.TileStyle == "Default" && rule.Highlight;
        bool glass = ApplyGlassCard(tile, state.CornerRadius, customTint && !locked ? surface : null);
        if (glass && (!customTint || locked))
            foreground = palette.Text;
        tile.Background = locked
            ? new FingerprintDrawable(glass ? Color.Transparent : surface,
                glass || !rule.Highlight ? palette.Accent : palette.OnAccent, Dp(state.CornerRadius), border)
            : outlined ? Background(glass ? Color.Transparent : surface, state.CornerRadius, border)
            : glass ? null : Surface(surface, state.CornerRadius, rule.Highlight);

        tile.SetPadding(Dp(6), Dp(6), Dp(6), Dp(10));

        tile.SetGravity(GravityFlags.Center);

        tile.ContentDescription =
            AppOrganization.DisplayName(state, app)
            + (locked ? ", locked" : "")
            + (state.LayeredInterface ? ", hold for app actions" : ", hold to customize");
        if (rule.Wide)
        {
            var top = Stack(true);
            top.SetGravity(GravityFlags.CenterVertical);
            var wideIcon = new FrameLayout(this);
            FitTileIcon(wideIcon, AppIcon(app, foreground), RequestedTileIconSize(state.TileIconScale, true));
            top.AddView(wideIcon, new LinearLayout.LayoutParams(Dp(RequestedTileIconSize(state.TileIconScale, true)), -1));
            var labels = Stack();
            labels.SetPadding(Dp(10), 0, 0, 0);
            var label = Text(AppOrganization.DisplayName(state, app), 12, true, foreground);
            label.SetMaxLines(2);
            labels.AddView(label, Match());
            if (state.ShowUsage && AppCatalog.HasUsageAccess(this))
                labels.AddView(Text(AppOrganization.FormatDuration(usage.GetValueOrDefault(app.PackageName)), 9, color: foreground), Match());
            top.AddView(labels, new LinearLayout.LayoutParams(0, -2, 1));
            tile.AddView(top, new LinearLayout.LayoutParams(-1, 0, 1));
            var actions = Stack(true);
            foreach (var (name, action) in new (string, Action)[] { ("Open", () => Launch(app)), ("Info", () => AppInfo(app)) })
            {
                var chip = Text(name, 10, color: foreground);
                chip.Gravity = GravityFlags.Center;
                chip.Background = Background(Color.Transparent, 24, foreground);
                TouchFeedback(chip, 24);
                chip.Click += (_, _) => action();
                var cp = new LinearLayout.LayoutParams(0, Dp(48), 1);
                cp.SetMargins(Dp(2), Dp(4), Dp(2), 0);
                actions.AddView(chip, cp);
            }
            tile.AddView(actions, Match());
        }
        else
        {
            var iconBox = new FrameLayout(this);
            int iconSize = TileIconSize;
            FitTileIcon(iconBox, AppIcon(app, foreground), iconSize);
            tile.AddView(iconBox, new LinearLayout.LayoutParams(-1, 0, 1));
            if (state.ShowLabels)
            {
                var label = Text(AppOrganization.DisplayName(state, app), EffectiveColumns >= 5 ? 10 : 11, color: foreground);
                label.Gravity = GravityFlags.Center;
                label.SetMaxLines(AppLabelLines(AppOrganization.DisplayName(state, app)));
                label.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                tile.AddView(label, Match());
            }
            if (state.ShowUsage && AppCatalog.HasUsageAccess(this))
            {
                var time = Text(AppOrganization.FormatDuration(usage.GetValueOrDefault(app.PackageName)), 9, color: rule.Highlight ? foreground : new Color(foreground.R, foreground.G, foreground.B, (byte)175));
                time.Gravity = GravityFlags.Center;
                time.SetPadding(0, Dp(2), 0, 0);
                tile.AddView(time, Match());
            }
        }
        var wrapped = WithBadge(tile, app);
        wrapped.Tag = "app-tile:" + app.Id;
        TouchFeedback(tile, state.CornerRadius);
        BindHoldHaptics(tile);
        tile.Click += (_, _) => Launch(app);
        tile.LongClick += (_, _) => InspectAppTile(app, wrapped);
        return wrapped;
    }

    private View NotificationIcon(string package)
    {
        var app = apps.FirstOrDefault(a => a.PackageName == package);
        if (app is not null)
            return AppIcon(app, palette.Text);
        try
        {
            var raw = PackageManager!.GetApplicationIcon(package);
            var icon = new ImageView(this);
            icon.SetScaleType(ImageView.ScaleType.FitCenter);
            if (state.IconStyle == "Monochrome" && OperatingSystem.IsAndroidVersionAtLeast(33) && raw is AdaptiveIconDrawable adaptive && adaptive.Monochrome is { } mono)
            {
                var drawable = mono.Mutate()!;
                drawable.SetTint(palette.Text);
                icon.SetImageDrawable(OpticallySized(package + ":notification-mono", drawable));
            }
            else
            {
                icon.SetImageDrawable(OpticallySized(package + ":notification-full", raw));
                if (state.IconStyle != "Original")
                {
                    using var matrix = new ColorMatrix();
                    matrix.SetSaturation(0);
                    icon.SetColorFilter(new ColorMatrixColorFilter(matrix));
                }
            }
            return icon;
        }
        catch (Android.Content.PM.PackageManager.NameNotFoundException)
        {
            var initial = Text(package.Split('.').Last()[..1].ToUpperInvariant(), 12, true, palette.Muted);
            initial.Gravity = GravityFlags.Center;
            return initial;
        }
    }

    private View WithBadge(View content, AppEntry app)
    {
        var frame = new FrameLayout(this);
        frame.AddView(content, new FrameLayout.LayoutParams(-1, -1));
        AddBadge(frame, app.PackageName);
        return frame;
    }
    private TextView AddBadge(FrameLayout frame, string package, bool compact = false)
    {
        var badge = Text("", compact ? 8 : 9, true, palette.OnAccent);
        badge.Gravity = GravityFlags.Center;
        int sidePadding = Dp(compact ? 3 : 5);
        badge.SetPadding(sidePadding, 0, sidePadding, 0);
        badge.SetMinimumWidth(Dp(compact ? 13 : 19));
        if (compact)
        {
            badge.SetIncludeFontPadding(false);
            badge.SetMinimumHeight(Dp(13));
        }
        badge.Background = Background(palette.Accent, 20);
        var lp = new FrameLayout.LayoutParams(-2, compact ? -2 : Dp(19), GravityFlags.Top | GravityFlags.Right);
        lp.SetMargins(0, Dp(compact ? 2 : 6), Dp(compact ? 1 : 6), 0);
        frame.AddView(badge, lp);
        badgeViews.Add((badge, package));
        UpdateBadge(badge, package);
        return badge;
    }
    private void UpdateBadge(TextView badge, string package)
    {
        int count = badgeCounts.GetValueOrDefault(package);
        bool muted = state.Apps.Any(p => p.Key.StartsWith(package + "/", StringComparison.Ordinal) && p.Value.MuteBadge);
        badge.Visibility = state.ShowNotificationBadges && !muted && count > 0 ? ViewStates.Visible : ViewStates.Gone;
        badge.Text = NotificationBadges.Label(count);
        badge.ContentDescription = $"{count} unread notifications";
    }

    private void RenderSearchResults()
    {
        if (page == "Root")
        {
            RenderRootCommands();
            return;
        }
        if (page == "Conversations")
        {
            RenderConversations();
            return;
        }
        RenderAppSearch();
    }

    private void AppInfo(AppEntry app) => OpenIntent(new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings, Uri.Parse("package:" + app.PackageName)));

    private Dialog Sheet(string title, Action<LinearLayout, Dialog> build, Action<LinearLayout, Dialog>? footer = null,
        Action<LinearLayout, Dialog>? fixedHeader = null, AccentGlowDrawable? backgroundGlow = null)
    {
        var dialog = new TouchGlowDialog(this);
        var overlay = new FrameLayout(this);
        overlay.SetOnApplyWindowInsetsListener(new InsetListener());
        var outside = new View(this);
        outside.Click += (_, _) => dialog.Dismiss();
        overlay.AddView(outside, new FrameLayout.LayoutParams(-1, -1));
        var container = new MaxHeightStack(this, (int)(Resources!.DisplayMetrics!.HeightPixels * .87));
        var shape = (GradientDrawable)Surface(palette.Sheet, 24);
        shape.SetCornerRadii([Dp(24), Dp(24), Dp(24), Dp(24), 0, 0, 0, 0]);
        container.Background = backgroundGlow is null ? shape : new LayerDrawable([shape, backgroundGlow]);
        container.ClipToOutline = backgroundGlow is not null;
        if (LayerMotionEnabled)
            container.Elevation = Dp(8);
        container.SetPadding(Dp(20), 0, Dp(20), Dp(16));
        var handle = new FrameLayout(this) { ContentDescription = "Drag down to close sheet" };
        var line = new View(this) { Background = Background(palette.Muted, 4) };
        handle.AddView(line, new FrameLayout.LayoutParams(Dp(38), Dp(4), GravityFlags.Center));
        handle.SetOnTouchListener(new SheetDragListener(container, dialog, Dp(100), view => Haptic(view, HapticCue.Commit)));
        container.AddView(handle, Match(Dp(32)));
        handle.ImportantForAccessibility = Android.Views.ImportantForAccessibility.No;
        var titleRow = Stack(true);
        titleRow.SetGravity(GravityFlags.CenterVertical);
        var heading = Text(title, 24, true);
        heading.SetPadding(0, Dp(4), Dp(8), Dp(16));
        heading.SetMaxLines(2);
        heading.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            heading.AccessibilityHeading = true;
        heading.SetOnTouchListener(new SheetDragListener(container, dialog, Dp(100), view => Haptic(view, HapticCue.Commit)));
        titleRow.AddView(heading, new LinearLayout.LayoutParams(0, -2, 1));
        titleRow.AddView(IconButton("close", "Close " + title, dialog.Dismiss), new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        container.AddView(titleRow, Match());
        if (fixedHeader is not null)
        {
            var controls = Stack();
            controls.SetPadding(0, 0, 0, Dp(10));
            controls.FocusableInTouchMode = true;
            fixedHeader(controls, dialog);
            container.AddView(controls, Match());
            controls.RequestFocus();
        }
        var content = Stack();
        build(content, dialog);
        var scroller = new ScrollView(this) { VerticalScrollBarEnabled = false, FillViewport = false, OverScrollMode = OverScrollMode.Never };
        scroller.AddView(content);
        container.AddView(scroller, new LinearLayout.LayoutParams(-1, 0, 1));
        if (footer is not null)
        {
            var actions = Stack();
            actions.SetPadding(0, Dp(12), 0, 0);
            actions.AddView(new View(this) { Background = new ColorDrawable(palette.Divider) }, Match(Dp(1)));
            Gap(actions, 12);
            footer(actions, dialog);
            container.AddView(actions, Match());
        }
        overlay.AddView(container, new FrameLayout.LayoutParams(-1, -2, GravityFlags.Bottom | GravityFlags.CenterHorizontal));
        dialog.SetContentView(overlay);
        dialog.Window!.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
        dialog.Window.SetSoftInputMode(SoftInput.AdjustResize);
        dialog.Show();
        activeSheets.Add(dialog);
        openSheetCount++;
        SyncDepthMotion();
        dialog.DismissEvent += (_, _) => { activeSheets.Remove(dialog); openSheetCount = Math.Max(0, openSheetCount - 1); SyncDepthMotion(); };
        dialog.Window.DecorView.SetPadding(0, 0, 0, 0);
        dialog.Window.SetLayout(-1, -1);
        dialog.Window.SetGravity(GravityFlags.Fill);
        dialog.Window.AddFlags(WindowManagerFlags.DimBehind);
        dialog.Window.SetDimAmount(.4f);
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            dialog.Window.InsetsController?.SetSystemBarsAppearance(palette.Light ? 24 : 0, 24);
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && state.MinimalStatusBar && IsHomePage)
            dialog.Window.InsetsController?.Hide(WindowInsets.Type.StatusBars());
        if (LayerMotionEnabled)
        {
            container.TranslationY = Dp(32);
            container.Alpha = 0;
            container.Animate()!.TranslationY(0).Alpha(1).SetDuration(220).SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.5f))!.Start();
        }
        overlay.RequestApplyInsets();
        return dialog;
    }

    private sealed class MaxHeightStack(Android.Content.Context context, int maxHeight) : LinearLayout(context)
    {
        public LiquidGlass? SearchGlass { get; set; }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            Orientation = Orientation.Vertical;
            int available = MeasureSpec.GetSize(heightMeasureSpec);
            int maxWidth = (int)(640 * Resources!.DisplayMetrics!.Density + .5f);
            int width = Math.Min(MeasureSpec.GetSize(widthMeasureSpec), maxWidth);
            base.OnMeasure(MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly), MeasureSpec.MakeMeasureSpec(Math.Min(maxHeight, available), MeasureSpecMode.AtMost));
        }
    }

    private void EditApp(AppEntry app) => WithAppUnlock(app.PackageName, () => EditAppUnlocked(app));
    private void EditAppUnlocked(AppEntry app)
    {
        var rule = state.Apps[app.Id];
        Sheet(AppOrganization.DisplayName(state, app), (panel, dialog) =>
        {
            Paragraph(app.PackageName, panel);
            if (AppCatalog.HasUsageAccess(this))
            {
                var used = usage.GetValueOrDefault(app.PackageName);
                var card = Stack();
                card.SetPadding(Dp(16), Dp(14), Dp(16), Dp(14));
                card.Background = Background(palette.Panel);
                card.AddView(Text("TODAY", 10, true, palette.Muted), Match());
                card.AddView(Text(AppOrganization.FormatDuration(used), 32, true), Match());
                Paragraph(rule.DailyGoalMinutes > 0 ? $"Daily goal · {rule.DailyGoalMinutes} minutes" : "Set a daily goal below for a mindful launch reminder.", card);
                panel.AddView(card, Match());
                Gap(panel);
            }
            void Change(Action action)
            {
                action();
                Save();
                dialog.Dismiss();
                Render();
            }
            SectionCaption(panel, "Appearance");
            SettingRow(panel, "App icon", rule.UseOriginalIcon ? "App’s own icon" : rule.IconPackage.Length > 0 ? rule.IconResource.Replace('_', ' ').Replace('-', ' ') : "Follow the global selection", () => { dialog.Dismiss(); PickIconPack(app); }, "photo");
            SettingRow(panel, "Tile appearance", rule.TileStyle, () => PickTileStyle(app, () => { dialog.Dismiss(); Render(); }), "photo");
            SectionCaption(panel, "Notifications");
            ToggleRow(panel, "Mute notification badge", rule.MuteBadge, v => Change(() => { foreach (var p in state.Apps.Where(p => p.Key.StartsWith(app.PackageName + "/", StringComparison.Ordinal))) p.Value.MuteBadge = v; }));
            SettingRow(panel, "App dashboard", "Usage charts, version, storage & data", () => { dialog.Dismiss(); ShowAppDashboard(app); }, "clock");
            SectionCaption(panel, "App behaviour");
            SettingRow(panel, "Biometric lock", PackageLocked(app.PackageName) ? "On · authenticate to open" : "Off", () =>
            {
                dialog.Dismiss();
                SetAppLock(app, !PackageLocked(app.PackageName));
            }, "lock");
            SettingRow(panel, "Open app in grayscale", rule.OpenInGrayscale ? "On · app display, not icon colour" : "Off · keep app colours", () =>
            {
                Choose("App display colour", ["Colour", "Grayscale", "Grayscale setup"], i =>
                {
                    if (i == 2)
                    {
                        dialog.Dismiss();
                        Navigate("Grayscale setup");
                        return;
                    }
                    Change(() => { foreach (var p in state.Apps.Where(p => p.Key.StartsWith(app.PackageName + "/", StringComparison.Ordinal))) p.Value.OpenInGrayscale = i == 1; });
                    if (i == 1 && !Services.GrayscaleService.Ready(this))
                        Navigate("Grayscale setup");
                }, rule.OpenInGrayscale ? 1 : 0);
            }, "photo");
            Paragraph("App lock protects launches and app editing through Launcher. Android notifications, Recents and other launchers remain controlled by Android.", panel);
            SectionCaption(panel, "Layout");
            SettingRow(panel, "Drawer", state.Drawers.First(d => d.Id == rule.DrawerId).Name, () =>
                Choose("Move to drawer", state.Drawers.Select(d => d.Name).ToArray(), i => Change(() => rule.DrawerId = state.Drawers[i].Id), state.Drawers.FindIndex(d => d.Id == rule.DrawerId)));
            SettingRow(panel, "Rename", rule.Label ?? app.Label, () => Prompt("App name", rule.Label ?? app.Label, s => Change(() => rule.Label = LauncherState.CleanName(s, app.Label))));
            SettingRow(panel, "Search aliases", rule.SearchAliases.Length == 0 ? "Find this app using your own words" : rule.SearchAliases, () => Prompt("Search aliases", rule.SearchAliases, s => Change(() => rule.SearchAliases = s.Trim()[..Math.Min(s.Trim().Length, 240)])));
            ToggleRow(panel, "Expanded tile", rule.Wide, v => Change(() => rule.Wide = v));
            ToggleRow(panel, "Pin to favorites", rule.Pinned, v => Change(() => rule.Pinned = v));
            SettingRow(panel, "Daily usage goal", rule.DailyGoalMinutes == 0 ? "Off" : rule.DailyGoalMinutes + " min", () =>
                Choose("Daily goal", ["Off", "15 minutes", "30 minutes", "45 minutes", "60 minutes", "90 minutes", "120 minutes"], i => Change(() => rule.DailyGoalMinutes = new[] { 0, 15, 30, 45, 60, 90, 120 }[i]), Array.IndexOf(new[] { 0, 15, 30, 45, 60, 90, 120 }, rule.DailyGoalMinutes)));
            var move = Stack(true);
            RootActions(panel, ("Move earlier", () => Change(() => MoveApp(app, -1))), ("Move later", () => Change(() => MoveApp(app, 1))));
            panel.AddView(Button(rule.Hidden ? "Show app" : "Hide from Launcher", () => Change(() => rule.Hidden = !rule.Hidden)), Match());
            Gap(panel);
            panel.AddView(Button("Android app info", () => { dialog.Dismiss(); AppInfo(app); }), Match());
            Gap(panel);
            if (app.ShortcutId.Length > 0)
            {
                panel.AddView(Button("Remove shortcut", () => Confirm("Remove shortcut?", "Remove this shortcut from Launcher? The app stays installed.", "Remove shortcut", () => { RemovePinnedShortcut(app); dialog.Dismiss(); })), Match());
                Gap(panel);
            }
            panel.AddView(Button("Done", () => dialog.Dismiss(), true), Match());
        });
    }

    private void MoveApp(AppEntry app, int direction)
    {
        var rule = state.Apps[app.Id];
        var siblings = apps.Where(a => !a.PrivateProfile && state.Apps[a.Id].DrawerId == rule.DrawerId && state.Apps[a.Id].Pinned == rule.Pinned).OrderBy(a => state.Apps[a.Id].Order).ToList();
        int index = siblings.FindIndex(a => a.Id == app.Id), target = index + direction;
        if (target < 0 || target >= siblings.Count)
            return;
        var other = state.Apps[siblings[target].Id];
        (other.Order, rule.Order) = (rule.Order, other.Order);
    }
}
