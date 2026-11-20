using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Views;
using Android.Views.Accessibility;
using Android.Widget;
using Launcher.Core;
using LauncherApp.UI;
using Orientation = Android.Widget.Orientation;
using Uri = Android.Net.Uri;
namespace LauncherApp.Services;

// A separate, opt-in service. It never reads view hierarchies or intercepts global gestures.
[Service(Name = "com.launcher.home.SystemPanelService", Label = "Launcher system panel", Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE", Exported = true)]
[IntentFilter(["android.accessibilityservice.AccessibilityService"])]
[MetaData("android.accessibilityservice", Resource = "@xml/system_panel_service")]
public sealed class SystemPanelService : AccessibilityService
{
    private static SystemPanelService? current;
    private readonly Handler handler = new(Looper.MainLooper!);
    private LauncherState state = new();
    private Palette palette = null!;
    private IWindowManager windows = null!;
    private readonly HashSet<View> ownedWindows = [];
    private View? strip, handle;
    private LinearLayout? panel, feed, mediaHost, controlHost;
    private TextView? panelClock, panelDate;
    private bool replying;
    private readonly HashSet<string> expanded = [];
    private int notificationLimit = 20;
    private FrameLayout? shade;
    private TextView? clock, batteryLabel;
    private LinearLayout? indicators;
    private SeekBar? volumeSlider;
    private BatteryView? battery;
    private ConnectionMonitor? connections;
    private MediaMonitor? media;
    private Events? events;
    private bool listening, open, dead, refreshQueued, screenOn = true, suspended;
    private string foreground = "com.launcher.home";
    private int percent = 100;
    private bool charging;
    private Typeface regular = Typeface.Default!, bold = Typeface.DefaultBold!;
    private System.Action? progressTick;
    private SeekBar? progress;
    private TextView? progressLabel;
    private bool seeking;
    private Android.Window.IOnBackInvokedCallback? back;
    private Android.Window.IOnBackInvokedDispatcher? backDispatcher;
    private long lastPulse;
    public static bool IsEnabled(Context context) => (Settings.Secure.GetString(context.ContentResolver, Settings.Secure.EnabledAccessibilityServices) ?? "").Split(':').Any(s => ComponentName.UnflattenFromString(s)?.ClassName == "com.launcher.home.SystemPanelService");
    public static void Refresh()
    {
        var service = current;
        service?.handler.Post(() => { if (!service.dead) service.Apply(); });
    }
    public static bool Open()
    {
        var service = current;
        if (service is null || !new StateStore(service).Load().SystemPanelEnabled)
            return false;
        service.handler.Post(service.Show);
        return true;
    }
    public static void Disconnect()
    {
        var service = current;
        service?.handler.Post(() => { service.Stop(); service.DisableSelf(); });
    }
    private int D(float n) => (int)(n * (Resources?.DisplayMetrics?.Density ?? 1) + .5f);
    private bool Locked => ((KeyguardManager?)GetSystemService(KeyguardService))?.IsDeviceLocked == true;
    private bool Allowed => state.SystemPanelEnabled && screenOn && !Locked && !suspended;
    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        current = this;
        windows = GetSystemService(WindowService)!.JavaCast<IWindowManager>()!;
        try
        {
            regular = Typeface.CreateFromAsset(Assets, "Outfit-Regular.ttf")!;
            bold = Typeface.CreateFromAsset(Assets, "Outfit-Bold.ttf")!;
        }
        catch (Java.Lang.RuntimeException) { }
        screenOn = ((PowerManager?)GetSystemService(PowerService))?.IsInteractive != false;
        Apply();
    }
    private void Apply()
    {
        state = new StateStore(this).Load();
        Android.Util.Log.Debug("LauncherSystemPanel", $"apply enabled={state.SystemPanelEnabled} listening={listening} screen={screenOn}");
        // Imported layouts cannot introduce invalid window sizes, alpha or control lists.
        state.SystemPanelOpacity = Math.Clamp(state.SystemPanelOpacity, 90, 100);
        if (state.SystemPanelTheme is "Light" or "Dark")
            state.Theme = state.SystemPanelTheme;
        palette = new(state);
        if (!state.SystemPanelEnabled)
        {
            Stop();
            return;
        }
        if (!listening)
        {
            listening = true;
            events = new Events(this);
            var filter = new IntentFilter();
            foreach (var a in new[] { Intent.ActionBatteryChanged, Intent.ActionTimeTick, Intent.ActionTimeChanged, Intent.ActionTimezoneChanged, Intent.ActionScreenOff, Intent.ActionScreenOn, Intent.ActionUserPresent, "android.media.VOLUME_CHANGED_ACTION" })
                filter.AddAction(a);
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                RegisterReceiver(events, filter, ReceiverFlags.NotExported);
            else
                RegisterReceiver(events, filter);
            ConversationService.Changed += Changed;
            connections = new(this, Changed);
            if (state.SystemPanelStatus)
                connections.Start();
            media = new(this, Changed);
        }
        else
        {
            if (state.SystemPanelStatus || open)
                connections?.Start();
            else
                connections?.Stop();
            if (open)
                media?.Start();
            else
                media?.Stop();
        }
        bool reopen = open;
        ClosePanel(false);
        Remove(ref strip);
        Remove(ref handle);
        UpdateStrip();
        if (reopen && Allowed)
            Show();
    }
    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
        if (!listening || e?.EventType != EventTypes.WindowStateChanged)
            return;

        string package = e.PackageName?.ToString() ?? "";
        string keyboard = (Settings.Secure.GetString(ContentResolver, Settings.Secure.DefaultInputMethod) ?? "").Split('/')[0];
        if (package.Length == 0 || package == keyboard)
            return;
        // Overlay windows also emit events under our own package. They are not
        // evidence that the user has returned to the Launcher activity.
        if (package == PackageName && (open || !(e.ClassName?.ToString() ?? "").Contains("MainActivity", StringComparison.Ordinal) && !(e.ClassName?.ToString() ?? "").Contains("Dialog", StringComparison.Ordinal)))
            return;
        foreground = package;
        suspended = package == "com.android.systemui" || package.Contains("permissioncontroller", StringComparison.Ordinal) || package.Contains("packageinstaller", StringComparison.Ordinal) || package.Contains("biometric", StringComparison.OrdinalIgnoreCase) || package == "com.android.settings";
        if (Locked || suspended)
        {
            Close();
            Remove(ref strip);
            Remove(ref handle);
            return;
        }
        if (!open)
            UpdateStrip();
    }
    private void Stop()
    {
        ClosePanel(false);
        Remove(ref strip);
        Remove(ref handle);
        foreach (var window in ownedWindows.ToArray())
        {
            View? target = window;
            Remove(ref target);
        }
        if (listening)
        {
            ConversationService.Changed -= Changed;
            if (events is not null)
                UnregisterReceiver(events);
            events = null;
            connections?.Stop();
            connections = null;
            media?.Stop();
            media = null;
            listening = false;
        }
        handler.RemoveCallbacksAndMessages(null);
        refreshQueued = false;
        clock = null;
        battery = null;
        batteryLabel = null;
        indicators = null;
    }
    public override void OnInterrupt()
    {
        ClosePanel(false);
        Remove(ref strip);
        Remove(ref handle);
    }
    public override bool OnUnbind(Intent? intent)
    {
        Stop();
        if (current == this)
            current = null;
        return base.OnUnbind(intent);
    }
    public override void OnDestroy()
    {
        dead = true;
        Stop();
        if (current == this)
            current = null;
        base.OnDestroy();
    }
    public override void OnConfigurationChanged(Configuration? newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        if (listening)
            Apply();
    }
    private void Changed()
    {
        handler.Post(() =>
        {
            if (dead || !listening || refreshQueued)
                return;
            refreshQueued = true;
            handler.PostDelayed(() => { refreshQueued = false; if (!Allowed) return; if (open) { if (controlHost is not null) { controlHost.RemoveAllViews(); Controls(controlHost); } RenderNotifications(); RenderMedia(); } UpdateStatus(); }, 120);
        });
    }
    private WindowManagerLayoutParams WindowParams(int width, int height, bool touch, bool focus = false)
    {
        var flags = WindowManagerFlags.LayoutInScreen | WindowManagerFlags.NotTouchModal;
        if (!focus)
            flags |= WindowManagerFlags.NotFocusable;
        if (!touch)
            flags |= WindowManagerFlags.NotTouchable;
        var p = new WindowManagerLayoutParams(width, height, WindowManagerTypes.AccessibilityOverlay, flags, Format.Translucent) { Gravity = GravityFlags.Top | GravityFlags.Left, Title = "Launcher system panel" };
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            p.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
        p.SoftInputMode = SoftInput.AdjustResize;
        return p;
    }
    private int NavigationBottom
    {
        get
        {
            int id = Resources!.GetIdentifier("navigation_bar_height", "dimen", "android");
            return id > 0 ? Resources.GetDimensionPixelSize(id) : D(24);
        }
    }
    private int NativeTop
    {
        get
        {
            int id = Resources!.GetIdentifier("status_bar_height", "dimen", "android");
            return id > 0 ? Resources.GetDimensionPixelSize(id) : D(28);
        }
    }
    private void Add(View view, WindowManagerLayoutParams p)
    {
        try
        {
            windows.AddView(view, p);
            ownedWindows.Add(view);
        }
        catch (Java.Lang.RuntimeException e) { Android.Util.Log.Warn("LauncherSystemPanel", e.Message ?? "Overlay unavailable"); }
    }
    private void Remove(ref View? view)
    {
        if (view is null)
            return;
        var target = view;
        view = null;
        // AddView registers a window before its first traversal attaches the view.
        // IsAttachedToWindow cannot be used to decide whether removal is needed.
        try
        {
            windows.RemoveViewImmediate(target);
        }
        catch (Java.Lang.RuntimeException) { }
        ownedWindows.Remove(target);
    }
    private void UpdateStrip()
    {
        if (!Allowed || open || foreground == PackageName)
        {
            Remove(ref strip);
            Remove(ref handle);
            return;
        }
        if (strip is null && state.SystemPanelStatus)
        {
            var row = Row();
            row.SetPadding(D(12), 0, D(12), 0);
            row.Background = Bg(palette.Base, 0);
            clock = Text("", 11, true);
            clock.SetSingleLine(true);
            clock.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            row.AddView(clock, new LinearLayout.LayoutParams(0, -1, 1));
            indicators = Row();
            row.AddView(indicators);
            battery = new(this, palette.Accent, palette.Muted);
            row.AddView(battery, new LinearLayout.LayoutParams(D(24), D(14)));
            batteryLabel = Text("", 11, true, palette.Accent);
            batteryLabel.SetPadding(D(5), 0, 0, 0);
            row.AddView(batteryLabel);
            // Top position leaves the native privacy-indicator region uncovered.
            var p = WindowParams(state.SystemPanelBelowStatus ? -1 : Math.Max(D(120), (Resources!.DisplayMetrics!.WidthPixels) - D(82)), D(28), false);
            p.Y = state.SystemPanelBelowStatus ? NativeTop : 0;
            strip = row;
            Add(row, p);
            UpdateStatus();
        }
        if (handle is null)
        {
            var area = new FrameLayout(this) { ContentDescription = "Open Launcher system panel" };
            var line = new View(this) { Background = Bg(palette.Accent, 3) };
            area.AddView(line, new FrameLayout.LayoutParams(D(32), D(3), GravityFlags.Center));
            var p = WindowParams(D(68), D(24), true);
            p.Gravity = GravityFlags.Top | (state.SystemPanelSide == "Left" ? GravityFlags.Left : GravityFlags.Right);
            p.Y = NativeTop + D(state.SystemPanelOffset) + (state.SystemPanelStatus && state.SystemPanelBelowStatus ? D(28) : 0);
            area.Click += (_, _) => Show();
            area.SetOnTouchListener(new HandleTouch(this));
            handle = area;
            Add(area, p);
        }
    }
    private void UpdateStatus()
    {
        if (!screenOn)
            return;
        if (indicators is not null)
        {
            indicators.RemoveAllViews();
            foreach (var pair in new[] { ("wifi", connections?.Wifi().Active == true), ("bluetooth", connections?.Bluetooth().Active == true) })
            if (pair.Item2)
            {
                var glyph = new GlyphView(this, pair.Item1, palette.Muted) { ContentDescription = pair.Item1 + " on" };
                var lp = new LinearLayout.LayoutParams(D(16), D(16));
                lp.SetMargins(0, 0, D(5), 0);
                indicators.AddView(glyph, lp);
            }
        }
        if (panelClock is not null)
            panelClock.Text = DateTime.Now.ToString("HH:mm");
        if (panelDate is not null)
            panelDate.Text = DateTime.Now.ToString("dddd, d MMMM") + "  ·  " + percent + "%" + (charging ? " · charging" : "");
        if (clock is not null)
            clock.Text = DateTime.Now.ToString("HH:mm  ·  ddd, d MMM");
        if (batteryLabel is not null)
            batteryLabel.Text = percent + "%";
        battery?.SetState(percent, charging);
    }
    private void Show()
    {
        if (!Allowed || open)
            return;
        open = true;
        connections?.Start();
        media?.Start();
        GrayscaleService.SetPanelVisible(true);
        Remove(ref strip);
        Remove(ref handle);
        shade = new FrameLayout(this) { FocusableInTouchMode = true, ContentDescription = "Launcher system panel" };
        shade.SetBackgroundColor(new Color(0, 0, 0, 90));
        panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(D(18), D(8), D(18), D(12) + NavigationBottom);
        var surface = palette.Base;
        surface.A = (byte)(255 * state.SystemPanelOpacity / 100);
        panel.Background = Bg(surface, 0);
        shade.AddView(panel, new FrameLayout.LayoutParams(-1, -1));
        var head = Row();
        var title = Text(DateTime.Now.ToString("HH:mm"), 32, true);
        panelClock = title;
        head.AddView(title, new LinearLayout.LayoutParams(0, -2, 1));
        head.AddView(Action("Android", () => { Close(); PerformGlobalAction(GlobalAction.Notifications); }, false));
        head.AddView(Action("Close", Close, false));
        panel.AddView(head);
        var date = Text(DateTime.Now.ToString("dddd, d MMMM") + "  ·  " + percent + "%" + (charging ? " · charging" : ""), 12, false, palette.Muted);
        panelDate = date;
        date.SetPadding(0, D(4), 0, D(10));
        panel.AddView(date);
        var grab = Text("⌃", 22, true, palette.Muted);
        grab.Gravity = GravityFlags.Center;
        grab.ContentDescription = "Swipe up to close system panel";
        grab.SetMinimumHeight(D(28));
        grab.Click += (_, _) => Close();
        grab.SetOnTouchListener(new CloseTouch(this));
        panel.AddView(grab);
        var scroll = new ScrollView(this) { FillViewport = false, VerticalScrollBarEnabled = false };
        var contents = Column();
        scroll.AddView(contents);
        panel.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        controlHost = Column();
        contents.AddView(controlHost);
        Controls(controlHost);
        Sliders(contents);
        mediaHost = Column();
        contents.AddView(mediaHost);
        var heading = Row();
        heading.SetPadding(0, D(14), 0, D(8));
        heading.AddView(Text("NOTIFICATIONS", 11, true, palette.Muted), new LinearLayout.LayoutParams(0, -2, 1));
        heading.AddView(Action("Clear", () => { foreach (var n in ConversationService.NotificationSnapshot().Where(n => n.Dismissible)) ConversationService.Dismiss(n.Key); }, false));
        contents.AddView(heading);
        feed = Column();
        contents.AddView(feed);
        var footer = Row();
        footer.AddView(Action("Panel settings", () => { Close(); Launch(new Intent(this, typeof(MainActivity)).PutExtra("open_system_panel_settings", true)); }, false), new LinearLayout.LayoutParams(0, -2, 1));
        footer.AddView(Action("Disable", () => { var s = new StateStore(this).Load(); s.SystemPanelEnabled = false; new StateStore(this).Save(s); StateStore.NotifyWorkspaceChanged(); Apply(); }, false));
        panel.AddView(footer);
        shade.KeyPress += (_, e) => { if (e.KeyCode == Keycode.Back) { if (e.Event?.Action == KeyEventActions.Up) Close(); e.Handled = true; } };
        var window = WindowParams(-1, Math.Max(D(120), Resources!.DisplayMetrics!.HeightPixels - NativeTop), true, true);
        window.Y = NativeTop;
        Add(shade, window);
        shade.RequestFocus();
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            back = new Back(Close);
            backDispatcher = shade.FindOnBackInvokedDispatcher();
            backDispatcher?.RegisterOnBackInvokedCallback(0, back);
        }
        if (state.SystemPanelAnimations)
        {
            panel.TranslationY = -D(40);
            panel.Alpha = 0;
            panel.Animate()!.TranslationY(0).Alpha(1).SetDuration(180).Start();
        }
        Pulse(shade);
        StartProgress();
        handler.PostDelayed(() => { if (open && Allowed) { RenderMedia(); RenderNotifications(); } }, 50);
    }
    private void Close() => ClosePanel(true);
    private void ClosePanel(bool restoreHandle)
    {
        if (progressTick is not null)
            handler.RemoveCallbacks(progressTick);
        progressTick = null;
        progress = null;
        progressLabel = null;
        volumeSlider = null;
        seeking = false;
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && back is not null)
        {
            backDispatcher?.UnregisterOnBackInvokedCallback(back);
            back = null;
            backDispatcher = null;
        }
        if (shade is not null)
        {
            View? remove = shade;
            shade = null;
            Remove(ref remove);
        }
        panel = null;
        feed = null;
        mediaHost = null;
        controlHost = null;
        panelClock = null;
        panelDate = null;
        replying = false;
        notificationLimit = 20;
        expanded.Clear();
        open = false;
        media?.Stop();
        if (!state.SystemPanelStatus)
            connections?.Stop();
        GrayscaleService.SetPanelVisible(false);
        if (restoreHandle && !dead && listening)
            UpdateStrip();
    }
    private void Controls(LinearLayout parent)
    {
        var wifi = connections?.Wifi() ?? (false, "Unavailable");
        var bt = connections?.Bluetooth() ?? (false, "Unavailable");
        var names = new[] { "Wi-Fi", "Bluetooth", "Sound", "Display", "Battery", "Settings" };
        var order = (state.SystemPanelControls ?? []).Where(names.Contains).Distinct().Concat(names).Distinct().ToArray();
        LinearLayout? row = null;
        for (int i = 0; i < order.Length; i++)
        {
            if (i % 2 == 0)
            {
                row = Row();
                parent.AddView(row);
            }
            string name = order[i];
            string glyph = name switch
            {
                "Wi-Fi" => "wifi",
                "Bluetooth" => "bluetooth",
                "Sound" => "sound",
                "Display" => "sun",
                "Battery" => "battery",
                _ => "settings"
            };
            bool active = name == "Wi-Fi" ? wifi.Item1 : name == "Bluetooth" ? bt.Item1 : false;
            string detail = name == "Wi-Fi" ? wifi.Item2 : name == "Bluetooth" ? bt.Item2 : name == "Battery" ? percent + "%" + (charging ? " · charging" : "") : "Open Android settings";
            var tile = Column();
            tile.SetPadding(D(12), D(10), D(10), D(10));
            tile.SetMinimumHeight(D(64));
            tile.Background = Ripple(active ? palette.Accent : palette.Card, 14);
            var line = Row();
            var ink = active ? palette.OnAccent : palette.Text;
            line.AddView(new GlyphView(this, glyph, ink), new LinearLayout.LayoutParams(D(24), D(24)));
            var label = Text(name, 14, true, ink);
            label.SetPadding(D(9), 0, 0, 0);
            line.AddView(label);
            tile.AddView(line);
            var sub = Text(detail, 11, false, active ? ink : palette.Muted);
            sub.SetMaxLines(2);
            sub.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            sub.SetPadding(0, D(5), 0, 0);
            tile.AddView(sub);
            tile.ContentDescription = name + ", " + detail;
            tile.Click += (_, _) => { Pulse(tile); Close(); Launch(new Intent(name switch { "Wi-Fi" => Settings.ActionWifiSettings, "Bluetooth" => Settings.ActionBluetoothSettings, "Sound" => Settings.ActionSoundSettings, "Display" => Settings.ActionDisplaySettings, "Battery" => "android.settings.BATTERY_SAVER_SETTINGS", _ => Settings.ActionSettings })); };
            var lp = new LinearLayout.LayoutParams(0, -1, 1);
            lp.SetMargins(D(3), D(3), D(3), D(3));
            row!.AddView(tile, lp);
        }
    }
    private void Sliders(LinearLayout parent)
    {
        var audio = (AudioManager?)GetSystemService(AudioService);
        if (audio is not null)
        {
            parent.AddView(Text("MEDIA VOLUME", 10, true, palette.Muted));
            var volume = new SeekBar(this) { Max = audio.GetStreamMaxVolume(Android.Media.Stream.Music), Progress = audio.GetStreamVolume(Android.Media.Stream.Music), ContentDescription = "Media volume" };
            volumeSlider = volume;
            Tint(volume);
            parent.AddView(volume, new LinearLayout.LayoutParams(-1, D(44)));
            volume.StopTrackingTouch += (_, _) => Pulse(volume);
            volume.ProgressChanged += (_, e) => { if (e.FromUser) try { audio.SetStreamVolume(Android.Media.Stream.Music, e.Progress, VolumeNotificationFlags.RemoveSoundAndVibrate); } catch (Java.Lang.SecurityException) { } };
        }
        var brightness = Row();
        brightness.AddView(Text("BRIGHTNESS", 10, true, palette.Muted), new LinearLayout.LayoutParams(0, -2, 1));
        bool allowed = Settings.System.CanWrite(this);
        bool adaptive = Settings.System.GetInt(ContentResolver, Settings.System.ScreenBrightnessMode, 0) == 1;
        brightness.AddView(Action(allowed ? adaptive ? "Adaptive · settings" : "Display settings" : "Allow", () => { Close(); Launch(new Intent(allowed ? Settings.ActionDisplaySettings : Settings.ActionManageWriteSettings, allowed ? null : Uri.Parse("package:" + PackageName))); }, false));
        parent.AddView(brightness);
        var slider = new SeekBar(this) { Max = 255, Progress = Settings.System.GetInt(ContentResolver, Settings.System.ScreenBrightness, 128), Enabled = allowed && !adaptive, ContentDescription = "Screen brightness" };
        Tint(slider);
        parent.AddView(slider, new LinearLayout.LayoutParams(-1, D(44)));
        slider.StopTrackingTouch += (_, _) => { if (Settings.System.CanWrite(this)) try { Settings.System.PutInt(ContentResolver, Settings.System.ScreenBrightness, Math.Max(1, slider.Progress)); Pulse(slider); } catch (Java.Lang.SecurityException) { } };
    }
    private void RenderNotifications()
    {
        if (feed is null || !Allowed || replying)
            return;
        feed.RemoveAllViews();
        if (!ConversationService.Enabled(this))
        {
            feed.AddView(Text("Enable notification access in Launcher Settings to show notifications here.", 14, false, palette.Muted));
            return;
        }
        var notifications = ConversationService.NotificationSnapshot();
        if (notifications.Count == 0)
        {
            feed.AddView(Text("All caught up", 16, true));
            return;
        }
        foreach (var group in notifications.Take(notificationLimit).GroupBy(n => n.Package))
        {
            var app = Row();
            app.SetPadding(0, D(10), 0, D(6));
            try
            {
                var icon = new ImageView(this);
                icon.SetImageDrawable(PackageManager!.GetApplicationIcon(group.Key));
                using var matrix = new ColorMatrix();
                matrix.SetSaturation(0);
                icon.SetColorFilter(new ColorMatrixColorFilter(matrix));
                app.AddView(icon, new LinearLayout.LayoutParams(D(18), D(18)));
            }
            catch (Android.Content.PM.PackageManager.NameNotFoundException) { }
            var appName = Text(AppName(group.Key), 12, true, palette.Accent);
            appName.SetPadding(D(8), 0, 0, 0);
            app.AddView(appName);
            feed.AddView(app);
            foreach (var item in group.Take(30))
            {
                bool concealed = state.HideProtectedPreviews && PackageLocked(item.Package);
                var card = Column();
                card.SetPadding(D(12), D(10), D(12), D(10));
                card.Background = Ripple(palette.Card, 12);
                var title = Row();
                title.AddView(Text(concealed ? "Protected notification" : item.Title, 15, true), new LinearLayout.LayoutParams(0, -2, 1));
                title.AddView(Text(DateTimeOffset.FromUnixTimeMilliseconds(item.Time).ToLocalTime().ToString("HH:mm"), 10, false, palette.Muted));
                card.AddView(title);
                var text = Text(concealed ? "Unlock in Launcher to open." : item.Text, 13, false, palette.Muted);
                text.SetMaxLines(expanded.Contains(item.Key) ? 30 : 3);
                text.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                text.SetPadding(0, D(5), 0, D(4));
                card.AddView(text);
                card.Click += (_, _) => OpenNotification(item);
                var actions = Row();
                if (!concealed && (item.Text.Length > 140 || item.Text.Contains('\n')))
                    actions.AddView(Action("Expand", () => { expanded.Add(item.Key); text.SetMaxLines(30); }, false));
                if (ConversationService.CanReply(item.Key))
                    actions.AddView(Action("Reply", () => Reply(card, item), false));
                if (item.Dismissible)
                    actions.AddView(Action("Dismiss", () => ConversationService.Dismiss(item.Key), false));
                card.AddView(actions);
                card.SetOnTouchListener(new NotificationSwipeListener(D(90), item.Dismissible, () => ConversationService.Dismiss(item.Key), () => Notice("This notification stays until its app clears it"), (v, _) => Pulse(v)));
                var lp = new LinearLayout.LayoutParams(-1, -2);
                lp.SetMargins(0, 0, 0, D(8));
                feed.AddView(card, lp);
            }
        }
        if (notifications.Count > notificationLimit)
            feed.AddView(Action("Show more", () => { notificationLimit = Math.Min(100, notificationLimit + 20); RenderNotifications(); }, false));
    }
    private bool PackageLocked(string package) => new StateStore(this).Load().Apps.Any(p => p.Key.StartsWith(package + "/", StringComparison.Ordinal) && p.Value.BiometricLocked);
    private void Reply(LinearLayout card, Conversation item)
    {
        if (PackageLocked(item.Package))
        {
            Close();
            Launch(new Intent(this, typeof(MainActivity)).PutExtra("system_panel_reply", item.Key));
            return;
        }
        if (card.FindViewWithTag("reply") is not null)
            return;
        replying = true;
        var row = Row();
        row.Tag = "reply";
        var input = new EditText(this) { Hint = "Reply", TextSize = 14, Typeface = regular, ContentDescription = "Reply to " + item.Title };
        input.SetTextColor(palette.Text);
        input.SetHintTextColor(palette.Muted);
        row.AddView(input, new LinearLayout.LayoutParams(0, -2, 1));
        row.AddView(Action("Send", () => { if (Locked) return; string text = input.Text?.Trim() ?? ""; if (text.Length == 0) return; if (ConversationService.Reply(this, item, text)) { replying = false; card.RemoveView(row); ((Android.Views.InputMethods.InputMethodManager?)GetSystemService(InputMethodService))?.HideSoftInputFromWindow(input.WindowToken, 0); Notice("Reply sent"); } else Notice("Reply unavailable. Open the app to continue."); }, false));
        row.AddView(Action("Cancel", () => { replying = false; card.RemoveView(row); RenderNotifications(); }, false));
        card.AddView(row);
        input.RequestFocus();
        ((Android.Views.InputMethods.InputMethodManager?)GetSystemService(InputMethodService))?.ShowSoftInput(input, Android.Views.InputMethods.ShowFlags.Implicit);
    }
    private void OpenNotification(Conversation item)
    {
        if (Locked)
            return;
        Close();
        if (PackageLocked(item.Package))
        {
            Launch(new Intent(this, typeof(MainActivity)).PutExtra("system_panel_notification", item.Key));
            return;
        }
        try
        {
            if (item.Open is not null)
            {
                var options = ActivityOptions.MakeBasic()!;
                if (OperatingSystem.IsAndroidVersionAtLeast(34))
                    options.SetPendingIntentBackgroundActivityStartMode(BackgroundActivityStartMode.Allowed);
                item.Open.Send(this, 0, null, null, null, null, options.ToBundle());
                return;
            }
        }
        catch (PendingIntent.CanceledException) { }
        catch (Java.Lang.SecurityException) { }
        var intent = PackageManager?.GetLaunchIntentForPackage(item.Package);
        if (intent is not null)
            Launch(intent);
        else
            Notice("This app cannot be opened");
    }
    private sealed class Back(System.Action close) : Java.Lang.Object, Android.Window.IOnBackInvokedCallback
    {
        public void OnBackInvoked() => close();
    }
    private void RenderMedia()
    {
        if (mediaHost is null || seeking)
            return;
        mediaHost.RemoveAllViews();
        progress = null;
        progressLabel = null;
        var controller = media?.Controller;
        if (controller is null)
            return;
        var metadata = controller.Metadata;
        var mediaAccent = Color.ParseColor(ColorContrast.ForDarkSurface(state.Accent));
        var frame = new FrameLayout(this) { Background = Bg(palette.Card, 18), ClipToOutline = true };
        var card = Column();
        card.SetPadding(D(14), D(12), D(14), D(10));
        var top = Row();
        var art = metadata?.GetBitmap(MediaMetadata.MetadataKeyArt) ?? metadata?.GetBitmap(MediaMetadata.MetadataKeyAlbumArt);
        if (art is not null)
        {
            var image = new ImageView(this) { Alpha = .6f };
            image.SetImageBitmap(art);
            image.SetScaleType(ImageView.ScaleType.CenterCrop);
            frame.AddView(image, new FrameLayout.LayoutParams(-1, -1));
        }
        frame.AddView(new View(this) { Background = new ColorDrawable(new Color(0, 0, 0, 165)) }, new FrameLayout.LayoutParams(-1, -1));
        frame.AddView(card, new FrameLayout.LayoutParams(-1, -2));
        var labels = Column();
        labels.SetPadding(D(10), 0, 0, 0);
        var track = Text(metadata?.GetString(MediaMetadata.MetadataKeyTitle) ?? AppName(controller.PackageName ?? ""), 16, true, Color.White);
        track.SetSingleLine(true);
        track.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        labels.AddView(track);
        var artist = Text(metadata?.GetString(MediaMetadata.MetadataKeyArtist) ?? AppName(controller.PackageName ?? ""), 12, false, mediaAccent);
        artist.SetSingleLine(true);
        artist.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        labels.AddView(artist);
        top.AddView(labels, new LinearLayout.LayoutParams(0, -2, 1));
        card.AddView(top);
        card.Click += (_, _) => { Close(); if (PackageLocked(controller.PackageName ?? "")) { Launch(new Intent(this, typeof(MainActivity)).PutExtra("system_panel_player", controller.PackageName)); return; } try { if (controller.SessionActivity is { } activity) { activity.Send(); return; } } catch (PendingIntent.CanceledException) { } var launch = PackageManager?.GetLaunchIntentForPackage(controller.PackageName ?? ""); if (launch is not null) Launch(launch); };
        progress = new SeekBar(this) { Max = 1000, ContentDescription = "Playback position" };
        Tint(progress);
        progress.ProgressTintList = ColorStateList.ValueOf(mediaAccent);
        progress.ThumbTintList = ColorStateList.ValueOf(mediaAccent);
        card.AddView(progress, new LinearLayout.LayoutParams(-1, D(48)));
        progress.StartTrackingTouch += (_, _) => seeking = true;
        progress.StopTrackingTouch += (_, _) => { long duration = controller.Metadata?.GetLong(MediaMetadata.MetadataKeyDuration) ?? 0; if (duration > 0 && ((controller.PlaybackState?.Actions ?? 0) & PlaybackState.ActionSeekTo) != 0) controller.GetTransportControls()?.SeekTo(duration * progress!.Progress / 1000); seeking = false; Pulse(progress!); };
        progressLabel = Text("", 10, false, new Color(205, 205, 205));
        card.AddView(progressLabel);
        var actions = Row();
        actions.SetGravity(GravityFlags.Center);
        foreach (var entry in new[] { ("previous", "Previous track", 0), (media!.Playing ? "pause" : "play", media.Playing ? "Pause playback" : "Resume playback", 1), ("next", "Next track", 2) })
        {
            long required = entry.Item3 == 0 ? PlaybackState.ActionSkipToPrevious : entry.Item3 == 2 ? PlaybackState.ActionSkipToNext : media.Playing ? PlaybackState.ActionPause | PlaybackState.ActionPlayPause : PlaybackState.ActionPlay | PlaybackState.ActionPlayPause;
            bool available = ((controller.PlaybackState?.Actions ?? 0) & required) != 0;
            var control = new FrameLayout(this) { ContentDescription = entry.Item2 + (available ? "" : " unavailable for this player"), Enabled = available, Alpha = available ? 1 : .32f, Background = Ripple(new Color(255, 255, 255, 24), 24) };
            control.AddView(new GlyphView(this, entry.Item1, Color.White), new FrameLayout.LayoutParams(D(23), D(23), GravityFlags.Center));
            control.Click += (_, _) => { Pulse(control); try { var transport = controller.GetTransportControls(); if (entry.Item3 == 0) transport?.SkipToPrevious(); else if (entry.Item3 == 2) transport?.SkipToNext(); else if (media.Playing) transport?.Pause(); else transport?.Play(); } catch (Java.Lang.SecurityException) { Notice("Playback control unavailable"); } };
            var lp = new LinearLayout.LayoutParams(D(48), D(48));
            lp.SetMargins(D(8), 0, D(8), 0);
            actions.AddView(control, lp);
        }
        card.AddView(actions);
        mediaHost.AddView(frame, new LinearLayout.LayoutParams(-1, -2));
        UpdateProgress();
    }
    private void StartProgress()
    {
        progressTick = () => { if (!open || !Allowed) return; UpdateProgress(); handler.PostDelayed(progressTick!, 1000); };
        handler.PostDelayed(progressTick, 1000);
    }
    private void UpdateProgress()
    {
        if (progress is null || seeking)
            return;
        var c = media?.Controller;
        long duration = c?.Metadata?.GetLong(MediaMetadata.MetadataKeyDuration) ?? 0;
        var playback = c?.PlaybackState;
        long position = playback?.Position ?? 0;
        if (media?.Playing == true && playback is not null)
            position += (long)((SystemClock.ElapsedRealtime() - playback.LastPositionUpdateTime) * playback.PlaybackSpeed);
        position = Math.Clamp(position, 0, Math.Max(0, duration));
        progress.Progress = duration > 0 ? (int)(position * 1000 / duration) : 0;
        progress.Enabled = duration > 0 && ((playback?.Actions ?? 0) & PlaybackState.ActionSeekTo) != 0;
        if (progressLabel is not null)
            progressLabel.Text = TimeSpan.FromMilliseconds(position).ToString(@"m\:ss") + " / " + TimeSpan.FromMilliseconds(duration).ToString(@"m\:ss");
    }
    private void Launch(Intent intent)
    {
        if (Locked)
            return;
        try
        {
            intent.AddFlags(ActivityFlags.NewTask);
            StartActivity(intent);
        }
        catch (ActivityNotFoundException) { Notice("This control is unavailable on this device"); }
        catch (Java.Lang.SecurityException) { Notice("Android has restricted this control"); }
    }
    private string AppName(string package)
    {
        try
        {
            return PackageManager!.GetApplicationLabel(PackageManager.GetApplicationInfo(package, 0));
        }
        catch (Android.Content.PM.PackageManager.NameNotFoundException) { return package; }
    }
    private void Notice(string message) => Toast.MakeText(this, message, ToastLength.Short)?.Show();
    private void Pulse(View view)
    {
        if (state.HapticFeedback == "Off" || Settings.System.GetInt(ContentResolver, "haptic_feedback_enabled", 1) == 0 || SystemClock.UptimeMillis() - lastPulse < 80)
            return;
        lastPulse = SystemClock.UptimeMillis();
        (view.RootView ?? view).PerformHapticFeedback(state.HapticFeedback == "Crisp" ? FeedbackConstants.ContextClick : FeedbackConstants.ClockTick);
    }
    private LinearLayout Column() => new(this) { Orientation = Orientation.Vertical };
    private LinearLayout Row()
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        return row;
    }
    private TextView Text(string value, float size = 14, bool strong = false, Color? color = null)
    {
        var text = new TextView(this) { Text = value, TextSize = size, Typeface = strong ? bold : regular, Gravity = GravityFlags.CenterVertical };
        text.SetTextColor(color ?? palette.Text);
        return text;
    }
    private TextView Action(string label, System.Action click, bool filled = true)
    {
        var view = Text(label, 12, true, palette.Accent);
        view.Gravity = GravityFlags.Center;
        view.SetMinHeight(D(44));
        view.SetPadding(D(10), D(4), D(10), D(4));
        view.Background = Ripple(filled ? palette.Panel : Color.Transparent, 12);
        view.Click += (_, _) => { Pulse(view); click(); };
        return view;
    }
    private RippleDrawable Ripple(Color color, int radius) => new(ColorStateList.ValueOf(new Color(palette.Accent.R, palette.Accent.G, palette.Accent.B, (byte)45)), Bg(color, radius), Bg(Color.White, radius));
    private GradientDrawable Bg(Color color, int radius)
    {
        var bg = new GradientDrawable();
        bg.SetColor(color);
        bg.SetCornerRadius(D(radius));
        return bg;
    }
    private void Tint(SeekBar seek)
    {
        seek.HapticFeedbackEnabled = false;
        seek.ProgressTintList = ColorStateList.ValueOf(palette.Accent);
        seek.ThumbTintList = ColorStateList.ValueOf(palette.Accent);
    }
    private sealed class Events(SystemPanelService owner) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent is null)
                return;
            if (intent.Action == Intent.ActionBatteryChanged)
            {
                int scale = intent.GetIntExtra(BatteryManager.ExtraScale, 100);
                owner.percent = scale > 0 ? Math.Clamp(intent.GetIntExtra(BatteryManager.ExtraLevel, 0) * 100 / scale, 0, 100) : 0;
                owner.charging = intent.GetIntExtra(BatteryManager.ExtraPlugged, 0) != 0;
                owner.UpdateStatus();
                if (owner.open)
                    owner.Changed();
            }
            else if (intent.Action == Intent.ActionScreenOff)
            {
                owner.screenOn = false;
                owner.Close();
                owner.Remove(ref owner.strip);
                owner.Remove(ref owner.handle);
                owner.media?.Stop();
                owner.connections?.Stop();
            }
            else if (intent.Action is Intent.ActionScreenOn or Intent.ActionUserPresent)
            {
                owner.screenOn = true;
                owner.suspended = false;
                if (owner.state.SystemPanelStatus || owner.open)
                    owner.connections?.Start();
                if (owner.open)
                    owner.media?.Start();
                owner.UpdateStrip();
            }
            else if (intent.Action == "android.media.VOLUME_CHANGED_ACTION")
            {
                if (owner.volumeSlider is not null && owner.GetSystemService(AudioService) is AudioManager audio)
                    owner.volumeSlider.Progress = audio.GetStreamVolume(Android.Media.Stream.Music);
            }
            else
                owner.UpdateStatus();
        }
    }
    private sealed class HandleTouch(SystemPanelService owner) : Java.Lang.Object, View.IOnTouchListener
    {
        private float x, y; private bool moving;
        public bool OnTouch(View? v, MotionEvent? e)
        {
            if (e is null)
                return false;
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    x = e.RawX;
                    y = e.RawY;
                    moving = false;
                    return true;
                case MotionEventActions.Move:
                    if (e.RawY - y > owner.D(28) && e.RawY - y > Math.Abs(e.RawX - x) * 1.5)
                    {
                        moving = true;
                    }
                    return true;
                case MotionEventActions.Up:
                    if (moving && e.RawY - y > owner.D(28))
                        owner.Show();
                    else if (!moving && Math.Abs(e.RawY - y) < owner.D(10) && Math.Abs(e.RawX - x) < owner.D(10))
                        v?.PerformClick();
                    return true;
                case MotionEventActions.Cancel:
                    return true;
            }
            return true;
        }
    }
    private sealed class CloseTouch(SystemPanelService owner) : Java.Lang.Object, View.IOnTouchListener
    {
        private float y; public bool OnTouch(View? v, MotionEvent? e)
        {
            if (e is null)
                return false;
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    y = e.RawY;
                    return true;
                case MotionEventActions.Move:
                    if (owner.panel is not null)
                        owner.panel.TranslationY = Math.Min(0, e.RawY - y);
                    return true;
                case MotionEventActions.Up:
                    if (y - e.RawY > owner.D(40))
                        owner.Close();
                    else if (Math.Abs(e.RawY - y) < owner.D(8))
                        v?.PerformClick();
                    else if (owner.panel is not null)
                        owner.panel.TranslationY = 0;
                    return true;
                case MotionEventActions.Cancel:
                    if (owner.panel is not null)
                        owner.panel.TranslationY = 0;
                    return true;
            }
            return true;
        }
    }

    public static bool TryLockScreen()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
            return false;

        var service = current;

        if (service is null || service.dead)
            return false;

        try
        {
            return service.PerformGlobalAction(
                GlobalAction.LockScreen);
        }
        catch (Java.Lang.RuntimeException ex)
        {
            Android.Util.Log.Warn(
                "LauncherScreenLock",
                ex.ToString());

            return false;
        }
    }
}
