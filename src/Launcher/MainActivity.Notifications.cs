using Android.Views;
using Android.Widget;
using Android.App;
using Android.Graphics;
using LauncherApp.Services;
using LauncherApp.UI;
namespace LauncherApp;

public partial class MainActivity
{
    private readonly Dictionary<string, string> packageLabels = [];
    private readonly HashSet<string> expandedNotificationApps = [];
    private LiquidGlass? liquidGlass;
    private Task<Bitmap?>? glassWallpaper;

    private void ConfigureLiquidGlass()
    {
        liquidGlass = null;
        if (!OperatingSystem.IsAndroidVersionAtLeast(33) || !state.LiquidGlassCards
            || (!IsHomePage && page is not ("Quick panel" or "Settings")))
            return;
        liquidGlass = new LiquidGlass(scene, palette, homeAccentGlow);
        scene.Background = liquidGlass;
        LoadGlassWallpaper(liquidGlass);
    }

    private void RefreshLiquidGlassWallpaper()
    {
        glassWallpaper = null;
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && liquidGlass is { } glass)
            LoadGlassWallpaper(glass);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android33.0")]
    private async void LoadGlassWallpaper(LiquidGlass glass)
    {
        int dim = IsWallpaperHome ? 0 : state.WallpaperDim;
        bool visible = state.UseWallpaper || IsWallpaperHome;
        if (!visible || !SharedStorage.CanRead(this))
        {
            glass.SetWallpaper(null, dim, visible);
            return;
        }
        var task = glassWallpaper ??= Task.Run(ReadGlassWallpaper);
        var image = await task;
        if (!IsDestroyed && ReferenceEquals(glass, liquidGlass) && ReferenceEquals(task, glassWallpaper))
            glass.SetWallpaper(image, dim, visible);
    }

    private bool ApplyGlassCard(View card, float cornerDp = 22, Color? surface = null, NotificationGroup.StackDepth? stack = null,
        bool persistent = false)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33) || liquidGlass is not { } glass)
            return false;
        glass.AddPanel(card, cornerDp, surface, stack, persistent);
        return true;
    }

    private Bitmap? ReadGlassWallpaper()
    {
        try
        {
            var manager = WallpaperManager.GetInstance(ApplicationContext!);
            using var probe = manager?.GetWallpaperFile(WallpaperManagerFlags.System);
            if (probe is null)
                return null;
            using var options = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFileDescriptor(probe.FileDescriptor, null, options);
            int sample = 1;
            while (Math.Max(options.OutWidth, options.OutHeight) / sample > 1600)
                sample *= 2;
            using var file = manager?.GetWallpaperFile(WallpaperManagerFlags.System);
            options.InJustDecodeBounds = false;
            options.InSampleSize = sample;
            return file is null ? null : BitmapFactory.DecodeFileDescriptor(file.FileDescriptor, null, options);
        }
        catch (Java.Lang.RuntimeException) { return null; }
        catch (Java.IO.IOException) { return null; }
    }
    private string PackageLabel(string package)
    {
        if (apps.FirstOrDefault(a => a.PackageName == package) is { } entry)
            return entry.Label;
        if (packageLabels.TryGetValue(package, out string? label))
            return label;
        try
        {
            var info = PackageManager!.GetApplicationInfo(package, Android.Content.PM.PackageInfoFlags.MetaData);
            label = PackageManager.GetApplicationLabel(info);
        }
        catch (Android.Content.PM.PackageManager.NameNotFoundException) { label = package.Split('.').Last(); }
        return packageLabels[package] = string.IsNullOrWhiteSpace(label) ? package : label;
    }
    private void AddNotificationGroups(LinearLayout feed, IEnumerable<Conversation> notifications, bool actions = false, bool swipe = false)
    {
        // GroupBy keeps the order of first occurrence, so the latest item sets the app's position.
        foreach (var group in notifications.OrderByDescending(item => item.Time).GroupBy(item => item.Package))
        {
            string label = PackageLabel(group.Key);
            var items = group.ToArray();
            var section = Stack();
            section.Tag = "notification-group:" + group.Key;
            bool glass = liquidGlass is not null;
            if (glass)
                section.SetPaddingRelative(Dp(11), Dp(4), Dp(11), Dp(6));
            var header = Stack(true);
            header.SetGravity(GravityFlags.CenterVertical);
            header.SetMinimumHeight(Dp(48));
            header.SetPaddingRelative(Dp(4), Dp(6), Dp(4), Dp(6));
            var icon = NotificationIcon(group.Key);
            icon.ImportantForAccessibility = ImportantForAccessibility.No;
            header.AddView(icon, new LinearLayout.LayoutParams(Dp(24), Dp(24)));
            var name = Text(label, 14, true, palette.Accent);
            name.SetPaddingRelative(Dp(10), 0, Dp(8), 0);
            name.SetMaxLines(2);
            name.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            header.AddView(name, new LinearLayout.LayoutParams(0, -2, 1));
            int total = items.Length;
            View? chevron = null;
            if (total > 1)
            {
                chevron = new GlyphView(this, "down", palette.Accent) { ImportantForAccessibility = ImportantForAccessibility.No };
                header.AddView(chevron, new LinearLayout.LayoutParams(Dp(18), Dp(18)) { MarginEnd = Dp(8) });
                var count = Text(total.ToString(), 12, true, palette.Accent);
                count.ImportantForAccessibility = ImportantForAccessibility.No;
                header.AddView(count, new LinearLayout.LayoutParams(-2, -2));
                TouchFeedback(header);
            }
            section.AddView(header, Match());
            var stack = NotificationGroup.AddRows(section, header, chevron, label, items, expandedNotificationApps,
                item => NotificationCard(item, label, actions, swipe), palette.Muted, glass,
                () => LayerMotionEnabled && state.PageTransitions);
            ApplyGlassCard(section, stack: stack);
            var layout = Match();
            layout.BottomMargin = Dp(HomeCardSpacingDp);
            feed.AddView(section, layout);
        }
    }

    private View NotificationCard(Conversation item, string app, bool actions, bool swipe)
    {
        bool concealed = ConcealPreview(item.Package);
        var card = Stack();
        card.SetMinimumHeight(Dp(48));
        card.SetPaddingRelative(Dp(38), Dp(4), Dp(4), Dp(7));
        card.Tag = "notification:" + item.Key;
        TouchFeedback(card);
        var summary = Stack(true);
        summary.BaselineAligned = false;
        var title = Text(concealed ? "Protected notification" : item.Title, 15, true);
        title.SetMaxLines(2);
        title.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        summary.AddView(title, new LinearLayout.LayoutParams(0, -2, 1));
        var posted = DateTimeOffset.FromUnixTimeMilliseconds(item.Time).ToLocalTime();
        var time = Text(posted.ToString(posted.Date == DateTime.Today ? "HH:mm" : "d MMM"), 11, color: palette.Muted);
        time.SetPaddingRelative(Dp(8), Dp(2), 0, 0);
        time.ContentDescription = posted.ToString("f");
        summary.AddView(time);
        card.AddView(summary, Match());
        if (item.Text.Length > 0 || concealed)
        {
            var preview = Text(concealed ? "Unlock to open this notification." : item.Text, 13, color: palette.Muted);
            preview.SetLineSpacing(Dp(2), 1);
            preview.SetMaxLines(3);
            preview.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            preview.SetPadding(0, Dp(3), 0, 0);
            card.AddView(preview, Match());
        }
        if (actions)
        {
            var buttons = new List<(string Title, Action Action)>();

            if (ConversationService.CanReply(item.Key))
            {
                buttons.Add((
                    "Reply",
                    () => ReplyTo(item)
                ));
            }

            if (state.KeepMessageHistory)
            {
                buttons.Add((
                    "History",
                    () => WithAppUnlock(
                        item.Package,
                        () => ShowMessageHistory(
                            item.Package,
                            ConversationService.Thread(item)))
                ));
            }

            if (buttons.Count > 0)
            {
                var controls = Stack(true);
                controls.SetGravity(GravityFlags.Right);

                foreach (var button in buttons)
                {
                    var action = RootAction(
                        button.Title,
                        button.Action);

                    action.TextSize = 12;

                    controls.AddView(
                        action,
                        FontScale >= 1.4f ? new LinearLayout.LayoutParams(0, -2, 1) : new LinearLayout.LayoutParams(-2, -2));
                }

                card.AddView(controls, Match());
            }
        }
        card.Click += (_, _) => OpenNotification(item);
        BindHoldHaptics(card);
        card.LongClick += (_, _) =>
        {
            var menu = new List<(string Label, Action Run)>
            {
                ("Open notification", () => OpenNotification(item)),
                item.Dismissible
                    ? ("Dismiss notification", () => ConversationService.Dismiss(item.Key))
                    : ("Persistent notification", () => Toast("Android keeps this notification while its service is active."))
            };
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                menu.Add(("Snooze for one hour", () =>
                {
                    if (!ConversationService.Snooze(item.Key, 3600000))
                        Toast("Android could not snooze this notification.");
                }));
            if (actions)
                menu.Add(("Read aloud", () => ShowNotificationSpeech(item)));
            Choose(app, menu.Select(option => option.Label).ToArray(), i => menu[i].Run());
        };
        if (swipe)
            card.SetOnTouchListener(new LauncherApp.UI.NotificationSwipeListener(Dp(90), item.Dismissible, () => ConversationService.Dismiss(item.Key), () => Toast("Android keeps this notification while its service is active."), (view, success) => Haptic(view, success ? HapticCue.Commit : HapticCue.Reject)));
        return card;
    }
}
