using Android.App;
using Android.App.Roles;
using Android.Appwidget;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.UI;
using System.Text;
using System.Text.RegularExpressions;
using Color = Android.Graphics.Color;
using Uri = Android.Net.Uri;

namespace LauncherApp;

[Activity(Name = "com.launcher.home.MainActivity", Label = "Launcher", MainLauncher = true,
    Exported = true, LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden,
    WindowSoftInputMode = SoftInput.AdjustResize)]
[IntentFilter([Intent.ActionMain], Categories = [Intent.CategoryHome, Intent.CategoryDefault])]
[IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault], DataMimeType = "text/plain")]
[IntentFilter(["android.content.pm.action.CONFIRM_PIN_SHORTCUT", "android.content.pm.action.CONFIRM_PIN_APPWIDGET"], Categories = [Intent.CategoryDefault])]
public partial class MainActivity : Activity
{
    private LauncherState state = new();
    private StateStore store = null!;
    private Palette palette = null!;
    private List<AppEntry> apps = [];
    private Dictionary<string, long> usage = new();
    private FrameLayout root = null!, scene = null!;
    private LinearLayout shell = null!, body = null!;
    private ScrollView scroll = null!;
    private EditText? searchInput;
    private TextView? timerText;
    private TextView? statusTime, statusDate;
    private TextView? statusNotificationText;
    private Android.Window.IOnBackInvokedCallback? backCallback;
    private string page = "Tiles", query = "";
    private bool searching, resumed, loading;
    private bool? appliedWallpaper;
    private int? appliedBackdropColor;
    private int openSheetCount;
    private long nextWeatherCheck;
    private readonly Handler handler = new(Looper.MainLooper!);
    private readonly List<(TextView View, string Package)> badgeViews = [];
    private Dictionary<string, int> badgeCounts = new();
    private readonly Dictionary<string, Drawable> iconCache = new();
    private readonly Dictionary<string, IconGeometry> iconGeometryCache = new();
    private readonly Dictionary<string, (bool Mono, bool Blank)> iconQualityCache = new();
    private bool catalogLoaded, catalogDirty = true;
    private int catalogRevision;
    private AppCatalogWatcher catalogWatcher = null!;
    private Action? pendingCatalogRefresh;
    private bool searchOpening;
    private Action? pendingSearch;
    private Action? tick;
    private AppWidgetHost widgetHost = null!;
    private ConnectionMonitor connections = null!;
    private MediaMonitor media = null!;
    private int pendingWidget = -1;
    private Typeface regular = Typeface.Create("sans-serif", TypefaceStyle.Normal)!;
    private Typeface bold = Typeface.Create("sans-serif", TypefaceStyle.Bold)!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
        Window.SetFormat(Format.Translucent);
#if DEBUG
        if (Intent?.GetBooleanExtra("qa_keep_awake", false) == true)
            Window!.AddFlags(WindowManagerFlags.KeepScreenOn);
#endif
        store = new(this);
        state = store.Load();
        companion = new(this);
        ambient = new(this);
        depthMotion = new(this, ApplyDeviceDepth);
        if (savedInstanceState is not null)
        {
            pendingWidget = savedInstanceState.GetInt("pending-widget", -1);
            page = savedInstanceState.GetString("page") ?? "Tiles";
            if (savedInstanceState.GetString("last-drawer-page") is { } drawerPage && IsDrawerPage(drawerPage))
                lastDrawerPage = drawerPage;
            foreach (string home in new[] { "Root", "Tiles", "Conversations", GroupedDrawerPage, AllAppsDrawerPage })
                homeScrollPositions[home] = Math.Max(0, savedInstanceState.GetInt("scroll:" + home, 0));
        }
        try
        {
            regular = Typeface.CreateFromAsset(Assets, "Outfit-Regular.ttf")!;
            bold = Typeface.CreateFromAsset(Assets, "Outfit-Bold.ttf")!;
        }
        catch (Java.Lang.RuntimeException) { }
        widgetHost = new AppWidgetHost(this, 2106);
        connections = new ConnectionMonitor(this, () => RunOnUiThread(() => { if (resumed && page == "Quick panel") RenderBody(); }));
        media = new MediaMonitor(this, MediaChanged);
        root = new FrameLayout(this);
        root.SetOnApplyWindowInsetsListener(new InsetListener(
            () => state.MinimalStatusBar && IsHomePage && state.StatusPlacement == "Top", ApplyQuickPanelBottomInset));
        // Android 15+ enforces edge-to-edge; older versions opt in here.
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && !OperatingSystem.IsAndroidVersionAtLeast(35))
            Window!.SetDecorFitsSystemWindows(false);
        SetContentView(root);
        // The root supplies the app background. A transparent window lets Android draw behind it.
        Window!.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
        catalogWatcher = new(this, CatalogChanged);
        catalogWatcher.Start();
        ConversationService.Changed += ConversationsChanged;
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            backCallback = new BackCallback(HandleBack);
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(0, backCallback);
        }
        if (Intent?.GetBooleanExtra("open_root", false) == true)
            page = "Root";
        StateStore.WorkspaceChanged += WorkspaceUpdated;
        var alertOwner = ApplicationContext ?? this;
        _ = Task.Run(() => TimerAlerts.Restore(alertOwner));
        SyncSystemTheme();
        Render();
        HandleSharedContent(Intent);
        HandlePinRequest(Intent);
        HandleSystemPanelIntent(Intent);
        if (Intent?.GetBooleanExtra("open_system_panel_settings", false) == true)
        {
            Intent.RemoveExtra("open_system_panel_settings");
            handler.Post(ShowSystemPanelSettings);
        }
    }
    protected override void OnSaveInstanceState(Bundle outState)
    {
        RememberHomeScroll();
        foreach (var saved in homeScrollPositions)
            outState.PutInt("scroll:" + saved.Key, saved.Value);
        outState.PutInt("pending-widget", pendingWidget);
        outState.PutString("page", pageSlide is { } slide && (pageSwipeActive || slide.ReturnToOrigin) ? slide.Origin : page);
        outState.PutString("last-drawer-page", lastDrawerPage);
        base.OnSaveInstanceState(outState);
    }
    protected override void OnStart()
    {
        base.OnStart();
        try
        {
            widgetHost.StartListening();
        }
        catch (Java.Lang.RuntimeException) { }
    }
    protected override void OnStop()
    {
        widgetHost.StopListening();
        base.OnStop();
    }
    protected override void OnResume()
    {
        base.OnResume();
        string previousTheme = state.Theme;
        SyncSystemTheme();
        if (previousTheme != state.Theme)
            Render();
        state.SystemPanelEnabled = store.Load().SystemPanelEnabled;
        RefreshSystemPanelSettings();
        SystemPanelService.Refresh();
        resumed = true;
        RefreshLiquidGlassWallpaper();
        UpdateStatusNotifications();
        connections.Start();
        media.Start();
        StartBatteryUpdates();
        RefreshApps();
        RefreshWeather();
        SyncDepthMotion();
        ResumeUpdateInstall();
        UpdateFileSearchAccess();
        WorkspaceUpdated();
        if (companionSession)
            companion.Start();
        SyncAutomaticTheme();
        tick = () =>
        {
            if (!resumed)
                return;
            if (state.FocusEndsAt > 0 && Now >= state.FocusEndsAt)
            {
                state.FocusEndsAt = 0;
                state.Focus = false;
                Save();
                Render();
            }
            if (timerText is not null && timerText.Text != FocusTime())
                timerText.Text = FocusTime();
            var now = DateTime.Now;
            string time = StatusClockText(now);
            if (statusTime is not null && statusTime.Text != time)
            {
                statusTime.Text = time;
                MatchHomeDateWidth();
            }
            string date = HomeStatusDateText(now);
            if (statusDate is not null && statusDate.Text != date)
            {
                statusDate.Text = date;
                MatchHomeDateWidth();
            }
            UpdateMediaProgress();
            UpdateWorkspaceTimers();
            UpdateResources();
            EvaluateAutomaticTheme();
            if (Now >= nextWeatherCheck)
            {
                nextWeatherCheck = Now + 60_000L;
                RefreshWeather();
            }
            handler.PostDelayed(tick!, 1000);
        };
        handler.Post(tick);
        if (pendingAppInspect is { } inspect)
        {
            pendingAppInspect = null;
            handler.Post(inspect);
        }
    }
    protected override void OnPause()
    {
        EndPageSlideForInterruption();
        FlushNoteSaves();
        CancelBodyRefresh();
        depthMotion.Stop();
        depthTouchActive = false;
        ResetDeviceDepth();
        StopRootFolds();
        StopAppLayer();
        resumed = false;
        ambient.Stop();
        lightCandidate = null;
        WorkspaceEditing.NoteId = null;
        companion.Stop();
        connections.Stop();
        media.Stop();
        StopBatteryUpdates();
        if (tick is not null)
            handler.RemoveCallbacks(tick);
        base.OnPause();
    }
    protected override void OnDestroy()
    {
        FlushNoteSaves();
        ResetFileSearch();
        StopPageSlide();
        StopAppLayer();
        StopDrawerAnimations();
        feedLifetime?.Cancel();
        feedLifetime?.Dispose();
        feedImageLifetime?.Cancel();
        feedImageLifetime?.Dispose();
        catalogWatcher.Stop();
        catalogWatcher.Dispose();
        if (pendingCatalogRefresh is not null)
            handler.RemoveCallbacks(pendingCatalogRefresh);
        if (pendingSearch is not null)
            handler.RemoveCallbacks(pendingSearch);
        depthMotion.Dispose();
        StopRootFolds();
        ambient.Dispose();
        companion.Dispose();
        StateStore.WorkspaceChanged -= WorkspaceUpdated;
        pendingAppInspect = null;
        appAuthentication?.Cancel();
        credentialAction = null;
        authenticationPending = false;
        updateLifetime.Cancel();
        ConversationService.Changed -= ConversationsChanged;
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && backCallback is not null)
            OnBackInvokedDispatcher.UnregisterOnBackInvokedCallback(backCallback);
        base.OnDestroy();
    }
    public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        SyncSystemTheme();
        FlushNoteSaves();
        if (openSheetCount == 0)
            Render();
        else
            root.RequestLayout();
    }
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleSystemPanelIntent(intent);
        HandleSharedContent(intent);
        HandlePinRequest(intent);
        if (intent?.GetBooleanExtra("open_system_panel_settings", false) == true)
        {
            intent.RemoveExtra("open_system_panel_settings");
            handler.Post(ShowSystemPanelSettings);
        }
        if (intent?.GetBooleanExtra("open_root", false) == true)
        {
            WorkspaceUpdated();
            Navigate("Root");
            return;
        }
#if DEBUG
        if (intent?.GetBooleanExtra("qa_keep_awake", false) == true)
            Window!.AddFlags(WindowManagerFlags.KeepScreenOn);
#endif
        if (intent?.HasCategory(Intent.CategoryHome) == true)
        {
            DismissSheets();
            if (page == "Tiles" && !searching)
                scroll.ScrollTo(0, 0);
            Navigate("Tiles");
        }
    }
    public override void OnBackPressed() => HandleBack();
    private void HandleBack()
    {
        if (appLayer is not null)
        {
            AnimateAppLayer(false);
            return;
        }
        if (searching)
        {
            HideKeyboard();
            searching = false;
            query = "";
            Render();
        }
        else if (IsDrawerPage(page))
            Navigate("Tiles");
        else if (page != "Tiles")
            Navigate(IsHomePage || page is "Settings" or "Quick panel" ? "Tiles" : "Settings");
        else
            scroll.SmoothScrollTo(0, 0);
    }
    private void CatalogChanged()
    {
        // Remove private identities immediately; reload only after profile access is checked.
        if (apps.RemoveAll(a => a.PrivateProfile) > 0 && resumed)
        {
            EndPageSlideForInterruption();
            RenderBody();
        }
        catalogDirty = true;
        catalogRevision++;
        if (resumed)
            ScheduleCatalogRefresh();
    }
    private void ScheduleCatalogRefresh()
    {
        if (pendingCatalogRefresh is not null)
            handler.RemoveCallbacks(pendingCatalogRefresh);
        pendingCatalogRefresh = () => { pendingCatalogRefresh = null; if (resumed && !IsDestroyed) RefreshApps(); };
        handler.PostDelayed(pendingCatalogRefresh, 200);
    }
    private async void RefreshApps(bool force = false)
    {
        if (force)
        {
            catalogDirty = true;
            catalogRevision++;
        }
        if (loading)
            return;
        bool reload = !catalogLoaded || catalogDirty, successful = false;
        int revision = catalogRevision;
        if (reload)
            catalogDirty = false;
        loading = true;
        try
        {
            var result = await Task.Run(() => (Catalog: reload ? AppCatalog.Load(this) : null, Usage: AppCatalog.UsageToday(this)));
            if (IsDestroyed)
                return;
            if (result.Catalog is { } updated)
            {
                apps = updated;
                catalogLoaded = true;
                packageLabels.Clear();
                iconCache.Clear();
                iconQualityCache.Clear();
                iconGeometryCache.Clear();
                AppOrganization.Reconcile(state, apps);
                Save();
            }
            usage = result.Usage;
            successful = true;
            if (resumed)
            {
                if (searching && IsAppsSurface)
                {
                    searchOpening = false;
                    if (appSearchAdapter is not null)
                        RefreshAppSearch();
                    else
                        RenderBody();
                }
                else if (!searching)
                {
                    if (appLayer is not null)
                        pendingLayerRefresh = true;
                    else
                        RefreshBodyInPlace();
                }
            }
        }
        catch (Exception e) when (e is Java.Lang.RuntimeException or InvalidOperationException)
        {
            if (reload)
                catalogDirty = true;
            Toast("Could not refresh apps. Reopen Launcher to retry.");
        }
        finally
        {
            loading = false;
            if (catalogDirty && resumed && !IsDestroyed && (successful || revision != catalogRevision))
                ScheduleCatalogRefresh();
        }
    }
    private void ConversationsChanged() => RunOnUiThread(() =>
    {
        badgeCounts = ConversationService.BadgeCounts();

        UpdateStatusNotifications();

        if (!resumed)
            return;

        if (page is "Conversations" or "Quick panel" && !searching)
        {
            RefreshBodyInPlace();
        }
        else
        {
            foreach (var (badge, package) in badgeViews)
            {
                UpdateBadge(badge, package);
            }
        }
    });

    private static long Now => DateTimeOffset.Now.ToUnixTimeMilliseconds();
    private void Save()
    {
        FlushNoteSaves();
        try
        {
            store.Save(state);
            SystemPanelService.Refresh();
            GrayscaleService.RulesChanged();
            SyncAutomaticTheme();
            SyncDepthMotion();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Toast("Changes are kept in this session. Could not save; check free storage."); }
    }
    private int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density + .5f);
    private LinearLayout Stack(bool horizontal = false) => new(this) { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical };
    private static LinearLayout.LayoutParams Match(int height = -2) => new(-1, height);
    private GradientDrawable Background(Color color, int radius = 12, Color? stroke = null)
    {
        var bg = new GradientDrawable();
        bg.SetColor(color);
        bg.SetCornerRadius(Dp(radius));
        if (stroke.HasValue)
            bg.SetStroke(Dp(1), stroke.Value);
        return bg;
    }
    private Android.Content.Res.ColorStateList AccentTint() => new(
        [new[] { -Android.Resource.Attribute.StateEnabled }, new[] { Android.Resource.Attribute.StateChecked }, Array.Empty<int>()],
        [new Color(palette.Muted.R, palette.Muted.G, palette.Muted.B, (byte)100).ToArgb(), palette.Accent.ToArgb(), palette.Muted.ToArgb()]);
    private Android.Content.Res.ColorStateList SwitchTrackTint() => new(
        [new[] { -Android.Resource.Attribute.StateEnabled }, new[] { Android.Resource.Attribute.StateChecked }, Array.Empty<int>()],
        [new Color(palette.Muted.R, palette.Muted.G, palette.Muted.B, (byte)35).ToArgb(), new Color(palette.Accent.R, palette.Accent.G, palette.Accent.B, (byte)85).ToArgb(), new Color(palette.Muted.R, palette.Muted.G, palette.Muted.B, (byte)65).ToArgb()]);
    private TextView Text(string value, float size = 14, bool strong = false, Color? color = null)
    {
        var view = new TextView(this) { Text = value, TextSize = size, Typeface = strong ? bold : regular };
        view.SetTextColor(color ?? palette.Text);
        view.SetIncludeFontPadding(false);
        return view;
    }
    private TextView Button(string value, Action action, bool filled = false)
    {
        var view = Text(value, 14, true, filled ? palette.OnAccent : palette.Text);
        view.SetTextColor(new Android.Content.Res.ColorStateList([new[] { -Android.Resource.Attribute.StateEnabled }, Array.Empty<int>()], [palette.Muted.ToArgb(), (filled ? palette.OnAccent : palette.Text).ToArgb()]));
        view.Gravity = GravityFlags.Center;
        view.SetPadding(Dp(14), Dp(10), Dp(14), Dp(10));
        view.SetMinimumHeight(Dp(48));
        view.Background = Background(filled ? palette.Accent : Color.Transparent, 12, filled ? null : palette.Divider);
        if (filled)
            view.BackgroundTintList = new Android.Content.Res.ColorStateList([new[] { -Android.Resource.Attribute.StateEnabled }, Array.Empty<int>()], [palette.Panel.ToArgb(), palette.Accent.ToArgb()]);
        TouchFeedback(view);
        view.Click += (_, _) => action();
        return view;
    }
    private View IconButton(string glyph, string description, Action action)
    {
        var frame = new FrameLayout(this) { ContentDescription = description };
        frame.SetMinimumWidth(Dp(48));
        frame.SetMinimumHeight(Dp(48));
        frame.AddView(new GlyphView(this, glyph, palette.Text), new FrameLayout.LayoutParams(Dp(22), Dp(22), GravityFlags.Center));
        TouchFeedback(frame, 24);
        frame.Click += (_, _) => action();
        return frame;
    }
    private void Gap(LinearLayout parent, int height = 12) => parent.AddView(new View(this), Match(Dp(height)));
    private void Paragraph(string text, LinearLayout? parent = null, TextAlignment alignment = TextAlignment.TextStart)
    {
        if (FilteringSettings &&
            (parent is null || ReferenceEquals(parent, body)))
            return;

        var v = Text(text, 13, color: palette.Muted);

        // Highlight text enclosed in *asterisks*
        ApplyAccentMarkers(v, text);

        v.SetLineSpacing(Dp(3), 1);
        v.SetPadding(0, Dp(4), 0, Dp(8));
        v.TextAlignment = alignment;

        (parent ?? body).AddView(v, Match());
    }
    private new void Title(string title)
    {
        if (!IsHomePage)
            return; // Settings pages already have a fixed, accessible title.
        var v = Text(title, 26, true);
        v.SetPadding(0, Dp(8), 0, Dp(16));
        body.AddView(v, Match());
    }
    private void Toast(string message) => Android.Widget.Toast.MakeText(this, message, ToastLength.Short)!.Show();
    private void ApplySystemBars()
    {
        bool light = palette.Light && !state.UseWallpaper && !IsWallpaperHome;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            const int lightStatus = 8, lightNavigation = 16;
            Window!.InsetsController?.SetSystemBarsAppearance(light ? lightStatus | lightNavigation : 0, lightStatus | lightNavigation);
            if (state.MinimalStatusBar && IsHomePage)
                Window.InsetsController?.Hide(WindowInsets.Type.StatusBars());
            else
                Window.InsetsController?.Show(WindowInsets.Type.StatusBars());
        }
        else
        {
#pragma warning disable CA1422 // Legacy bar appearance for Android 7–10.
            var flags = light ? SystemUiFlags.LightStatusBar : 0;
            if (light && OperatingSystem.IsAndroidVersionAtLeast(26))
                flags |= SystemUiFlags.LightNavigationBar;
            Window!.DecorView.SystemUiFlags = flags;
            if (state.MinimalStatusBar && IsHomePage)
                Window.AddFlags(WindowManagerFlags.Fullscreen);
            else
                Window.ClearFlags(WindowManagerFlags.Fullscreen);
#pragma warning restore CA1422
        }
    }
    private void Render() => Render(pageTransition: false);

    private void Render(bool pageTransition)
    {
        if (pageSlide is { } slide && (pageSwipeActive || slide.ReturnToOrigin))
            page = slide.Origin;
        if (!SearchingFiles)
            ResetFileSearch();
        StopPageSlide();
        CancelBodyRefresh();
        RememberHomeScroll();
        depthMotion.Stop();
        ResetDeviceDepth();
        StopAppLayer();
        palette = new(state);
        timerText = null;
        statusTime = null;
        statusDate = null;
        launcherStatusRow = null;
        homeWeatherSlot = null;
        homeBattery = null;
        homeDrawerMarker = null;
        statusNotificationText = null;
        batteryIcon = null;
        batteryPercentText = null;
        root.RemoveAllViews();
        ApplyWallpaperVisibility(pageTransition);
        ApplyBackdrop(IsWallpaperHome ? 0 : 1);
        ApplySystemBars();
        homeAccentGlow = null;
        if (IsHomePage && !searching && (!IsWallpaperHome || state.HomeAccentGlow || pageTransition))
        {
            homeAccentGlow = new AccentGlowDrawable(Color.ParseColor(state.Accent), (byte)(palette.Light ? 23 : 40));
            var accentLighting = new View(this)
            {
                Background = homeAccentGlow,
                ImportantForAccessibility = ImportantForAccessibility.No
            };
            root.AddView(accentLighting, new FrameLayout.LayoutParams(-1, -1));
        }
        scene = new FrameLayout(this);
        ConfigureLiquidGlass();
        root.AddView(scene, new FrameLayout.LayoutParams(-1, -1));
        shell = Stack();
        scene.AddView(shell, new FrameLayout.LayoutParams(-1, -1));
        if (IsHomePage && !IsDrawerPage(page) && state.MinimalStatusBar)
            AddMinimalStatus();
        if (IsDrawerPage(page) && !searching)
            AddDrawerHeader();
        if (!IsHomePage)
            AddPageHeader(page);
        if (page == "Settings")
            AddSettingsSearch();
        body = Stack();
        int side = Math.Max(16, (Resources!.Configuration!.ScreenWidthDp - 720) / 2);
        body.SetPadding(Dp(side), Dp(12), Dp(side), page == "Quick panel" ? 0 : Dp(12));

        //screenlock gesture adding
        AttachScreenLockGesture(body);
        
        body.HapticFeedbackEnabled = false;
        body.LongClick += (_, _) => { if (page == "Tiles" && !searching) { Haptic(body, HapticCue.Hold); QuickControls(); } };
        scroll = new ScrollView(this) { FillViewport = true, VerticalScrollBarEnabled = false, OverScrollMode = OverScrollMode.Never, VerticalFadingEdgeEnabled = state.LayeredInterface };
        if (homeBattery is not null)
            scroll.ScrollChange += (_, _) => PositionHomeBattery();
        scroll.SetBackgroundColor(Color.Transparent);
        scroll.SetClipToPadding(false);
        scroll.SetFadingEdgeLength(Dp(14));
        if (searching && IsAppsSurface)
            shell.AddView(body, new LinearLayout.LayoutParams(-1, 0, 1));
        else
        {
            scroll.AddView(body, new ScrollView.LayoutParams(-1, -2));
            shell.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        }
        RenderBody();
        if (IsHomePage && (page != "Tiles" || searching))
            AddSearchBar();
        if (IsHomePage && !IsDrawerPage(page))
            AddHomeNavigation();
        root.RequestApplyInsets();
        RestoreHomeScroll();
        SyncDepthMotion();
    }
    private bool IsHomePage => IsAppsSurface || page is "Root" or "Conversations";
    private bool IsWallpaperHome => page == "Tiles" && !searching;

    private void ApplyWallpaperVisibility(bool pageTransition = false)
    {
        bool visible = IsWallpaperHome || state.UseWallpaper || pageTransition;
        if (appliedWallpaper == visible)
            return;
        if (visible)
            Window!.AddFlags(WindowManagerFlags.ShowWallpaper);
        else
            Window!.ClearFlags(WindowManagerFlags.ShowWallpaper);
        appliedWallpaper = visible;
    }

    private void ApplyBackdrop(float dimmingFraction)
    {
        var color = palette.Base;
        int dimming = state.UseWallpaper ? state.WallpaperDim : 100;
        color = new Color(color.R, color.G, color.B, (byte)(dimming * 255 * dimmingFraction / 100));
        if (appliedBackdropColor == color.ToArgb())
            return;
        root.SetBackgroundColor(color);
        appliedBackdropColor = color.ToArgb();
    }
    private void AddPageHeader(string title)
    {
        var row = Stack(true);
        row.SetGravity(GravityFlags.CenterVertical);
        row.AddView(IconButton("back", "Back", () => Navigate(page is "Settings" or "Quick panel" ? "Tiles" : "Settings")), new LinearLayout.LayoutParams(Dp(52), Dp(52)));
        string label = title switch
        {
            "Updates" => "App updates",
            "Root settings" => "Today cards",
            "Grayscale setup" => "App grayscale",
            "Integrations" => "Online cards",
            "Hidden" => "Hidden apps",
            _ => PageLabel(title)
        };
        var heading = Text(label, 22, true);
        heading.SetMaxLines(2);
        heading.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        heading.SetPadding(0, Dp(10), Dp(16), Dp(10));
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            heading.AccessibilityHeading = true;
        row.AddView(heading, new LinearLayout.LayoutParams(0, -2, 1));
        row.SetMinimumHeight(Dp(58));
        shell.AddView(row, Match());
    }
    private void RenderBody()
    {
        depthAllowed = new DeviceCapabilities(this).AllowDepth;
        if (appLayer is not null)
        {
            pendingLayerRefresh = true;
            return;
        }
        ResetDeviceDepth();
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            liquidGlass?.ClearPanels(keepPersistent: true);
        conversationFilterScroll = null;
        depthHeadings.Clear();
        StopRootFolds();
        scroll.VerticalFadingEdgeEnabled = state.LayeredInterface;
        resourcesPanel = null;
        memoryDonut = null;
        storageDonut = null;
        powerDonut = null;
        memoryAmount = null;
        storageAmount = null;
        powerStatus = null;
        resourcesFootnote = null;
        feedImageLifetime?.Cancel();
        feedRows.Clear();
        feedPanel = null;
        feedStatus = null;
        feedRefresh = null;
        feedRefreshGlyph = null;
        feedRefreshProgress = null;
        StopDrawerAnimations();
        appSearchAdapter = null;
        appSearchList = null;
        appSearchTitle = null;
        appSearchCount = null;
        body.RemoveAllViews();
        badgeViews.Clear();
        badgeCounts = ConversationService.Enabled(this) ? ConversationService.BadgeCounts() : new();
        timerText = null;
        mediaProgress = null;
        mediaTime = null;
        body.SetGravity(GravityFlags.Top);
        if (searching)
        {
            if (IsAppsSurface)
            {
                RenderAppSearch();
                return;
            }
            if (searchOpening)
            {
                Title("Search");
                Paragraph(page == "Conversations" ? "Find a notification." : page == "Root" ? "Choose a card or type a command." : "Find an app by name.");
            }
            else
                RenderSearchResults();
            return;
        }
        switch (page)
        {
            case "Tiles":
                RenderTiles();
                break;
            case GroupedDrawerPage:
            case AllAppsDrawerPage:
                RenderAppDrawer();
                break;
            case "Root":
                RenderRoot();
                break;
            case "Conversations":
                RenderConversations();
                break;
            case "Settings":
                RenderSettings();
                break;
            case "Updates":
                RenderUpdates();
                break;
            case "Grayscale setup":
                RenderGrayscaleSetup();
                break;
            case "Integrations":
                RenderIntegrations();
                break;
            case "Desktop companion":
                RenderCompanion();
                break;
            case "Appearance":
                RenderAppearance();
                break;
            case "Automatic theme":
                RenderAutomaticTheme();
                break;
            case "Drawers":
                RenderDrawerSettings();
                break;
            case "Hidden":
                RenderHidden();
                break;
            case "Permissions":
                RenderPermissions();
                break;
            case "Root settings":
                RenderRootSettings();
                break;
            case "Quick panel":
                RenderQuickPanel();
                break;
            case "Weather":
                RenderWeatherSettings();
                break;
        }
    }
    private void AddSearchBar()
    {
        int barHeight = (int)Math.Max(52, 15 * FontScale * 1.3f + 8);
        var bottom = Stack();
        bottom.SetPadding(Dp(16), Dp(4), Dp(16), 0);
        var pill = Stack(true);
        pill.SetGravity(GravityFlags.CenterVertical);
        if (!ApplyGlassCard(pill, 30, persistent: true))
            pill.Background = Background(palette.Card, 30);
        if (IsAppsSurface)
            AddFileSearchToggle(pill);
        if (searching)
        {
            var close = IconButton(IsAppsSurface ? "close" : "back", "Close search", HandleBack);
            if (!IsAppsSurface)
                pill.AddView(close, new LinearLayout.LayoutParams(Dp(44), Dp(barHeight)));
            var input = new EditText(this) { Text = query, Hint = SearchingFiles ? "Search files" : page == "Conversations" ? "Search notifications" : page == "Root" ? "Type a command" : "Search apps", TextSize = 15, Typeface = regular };
            searchInput = input;
            searchInput.SetSingleLine(true);
            searchInput.SetTextColor(palette.Text);
            searchInput.SetHintTextColor(palette.Muted);
            searchInput.Background = null;
            StyleEntry(input);
            searchInput.SetPadding(Dp(4), 0, 0, 0);
            searchInput.ImeOptions = ImeAction.Search;
            searchInput.TextChanged += (_, _) =>
            {
                query = input.Text ?? "";
                if (pendingSearch is not null)
                    handler.RemoveCallbacks(pendingSearch);
                pendingSearch = () => { pendingSearch = null; if (searching) { if (IsAppsSurface) RefreshAppSearch(); else RenderBody(); } };
                handler.PostDelayed(pendingSearch, 40);
            };
            searchInput.EditorAction += (_, e) =>
            {
                if (page == "Root")
                    ExecuteRootCommand(query);
                else if (page != "Conversations")
                {
                    OpenFirstSearchResult();
                }
                e.Handled = true;
            };
            pill.AddView(searchInput, new LinearLayout.LayoutParams(0, Dp(barHeight), 1));
            if (IsAppsSurface)
                pill.AddView(close, new LinearLayout.LayoutParams(Dp(44), Dp(barHeight)));
        }
        else
        {
            if (!IsAppsSurface)
            {
                var control = IconButton("search", "Search; hold for launcher controls", StartSearch);
                BindHoldHaptics(control);
                control.LongClick += (_, _) => QuickControls();
                pill.AddView(control, new LinearLayout.LayoutParams(Dp(44), Dp(barHeight)));
            }
            var prompt = Text(FontScale >= 1.4f ? "Search" : page == "Conversations" ? "Search notifications" : page == "Root" ? "Type a command" : "Search apps", 15, color: palette.Muted);
            prompt.SetSingleLine(true);
            prompt.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            prompt.ContentDescription = page == "Root" ? "Search Today or enter a command" : page == "Conversations" ? "Search notifications" : "Search apps";
            prompt.SetPadding(Dp(4), 0, 0, 0);
            prompt.Gravity = GravityFlags.CenterVertical;
            BindTapHaptics(prompt);
            prompt.Click += (_, _) => StartSearch();
            pill.AddView(prompt, new LinearLayout.LayoutParams(0, Dp(barHeight), 1));
            BindHoldHaptics(pill);
            pill.LongClick += (_, _) => QuickControls();
        }
        pill.AddView(IconButton("more", "Launcher settings", () => { HideKeyboard(); QuickControls(); }), new LinearLayout.LayoutParams(Dp(48), Dp(barHeight)));
        bottom.AddView(pill, Match(Dp(barHeight)));
        shell.AddView(bottom, Match());
    }
    private void StartSearch() => BeginSearch(files: false, keepQuery: false);

    private void BeginSearch(bool files, bool keepQuery)
    {
        if (pendingSearch is not null)
        {
            handler.RemoveCallbacks(pendingSearch);
            pendingSearch = null;
        }
        searching = true;
        fileSearchMode = files;
        fileSearchAccess = files && SharedStorage.CanRead(this);
        searchOpening = true;
        if (!keepQuery)
            query = "";
        if (files && !fileSearchAccess)
            HideKeyboard();
        Render();
        if (files)
        {
            if (!fileSearchAccess)
                return;
            _ = LoadSearchFilesAsync();
        }
        FocusSearchInput();
    }

    private void FocusSearchInput()
    {
        var input = searchInput;
        input?.RequestFocus();
        input?.Post(() =>
        {
            if (!searching || !ReferenceEquals(searchInput, input) || IsDestroyed)
                return;
            ((InputMethodManager?)GetSystemService(InputMethodService))?.ShowSoftInput(input, ShowFlags.Implicit);
            searchOpening = false;
            if (!IsAppsSurface)
                handler.Post(() => { if (searching) RenderBody(); });
        });
    }
    private void HideKeyboard() => ((InputMethodManager?)GetSystemService(InputMethodService))?.HideSoftInputFromWindow(root.WindowToken, HideSoftInputFlags.None);
    public override bool DispatchTouchEvent(MotionEvent? ev)
    {
        if (ev is not null)
            lastHomeTouch = System.Environment.TickCount64;
        if (ev?.ActionMasked == MotionEventActions.Down)
            depthTouchActive = true;
        else if (ev?.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel)
            depthTouchActive = false;
        if (ev?.ActionMasked == MotionEventActions.Down)
            drawerTouchActive = false;
        if (appLayer is null && ev is not null && HandlePageSwipe(ev))
            return true;
        return base.DispatchTouchEvent(ev);
    }
    private void Launch(AppEntry app)
    {
        var rule = state.Apps[app.Id];
        if (rule.DailyGoalMinutes > 0 && usage.GetValueOrDefault(app.PackageName) >= rule.DailyGoalMinutes * 60_000L)
            Confirm("Your daily goal", $"You have used {app.Label} for {AppOrganization.FormatDuration(usage.GetValueOrDefault(app.PackageName))} today. Open it intentionally?", "Open app", () => LaunchDirect(app));
        else
            LaunchDirect(app);
    }
    private void LaunchDirect(AppEntry app) => WithAppUnlock(app.PackageName, () => StartAppDirect(app));
    private void StartAppDirect(AppEntry app)
    {
        try
        {
            HideKeyboard();
            if (app.ProfileSerial >= 0 || app.ShortcutId.Length > 0)
            {
                var launcher = GetSystemService(LauncherAppsService) as LauncherApps;
                var user = ProfileCatalog.User(this, app.ProfileSerial);
                if (launcher is null || user is null)
                {
                    Toast("This profile is no longer available.");
                    return;
                }
                if (app.ShortcutId.Length > 0 && OperatingSystem.IsAndroidVersionAtLeast(25))
                    launcher.StartShortcut(app.PackageName, app.ShortcutId, null, null, user);
                else
                    launcher.StartMainActivity(new ComponentName(app.PackageName, app.ActivityName), user, null, null);
                return;
            }
            var intent = new Intent(Intent.ActionMain).AddCategory(Intent.CategoryLauncher)!
                .SetComponent(new ComponentName(app.PackageName, app.ActivityName))!.AddFlags(ActivityFlags.NewTask | ActivityFlags.ResetTaskIfNeeded);
            StartActivity(intent);
        }
        catch (Exception e) when (e is ActivityNotFoundException or Java.Lang.SecurityException) { Toast("This app is no longer available."); RefreshApps(true); }
    }
    private void OpenIntent(Intent intent)
    {
        try
        {
            StartActivity(intent);
        }
        catch (Exception e) when (e is ActivityNotFoundException or Java.Lang.SecurityException) { Toast("No app is available for this action."); }
    }
    private void OpenUrl(string url) => OpenIntent(new Intent(Intent.ActionView, Uri.Parse(url)));
    private bool ApplyQuickPanelBottomInset(int inset)
    {
        if (page != "Quick panel" || scroll is null)
            return false;
        if (scroll.PaddingBottom != inset)
            scroll.SetPadding(0, 0, 0, inset);
        return true;
    }

    private sealed class InsetListener(Func<bool>? drawAtTop = null, Func<int, bool>? drawAtBottom = null) : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View? v, WindowInsets? insets)
        {
            if (insets is null)
                return null!;
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var i = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.Ime() | WindowInsets.Type.DisplayCutout());
                int top = drawAtTop?.Invoke() == true ? insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.Ime()).Top : i.Top;
                int bottom = drawAtBottom?.Invoke(i.Bottom) == true ? 0 : i.Bottom;
                v?.SetPadding(i.Left, top, i.Right, bottom);
            }
            else
            {
#pragma warning disable CA1422 // Compatibility branch for Android 7–10.
                v?.SetPadding(insets.SystemWindowInsetLeft, insets.SystemWindowInsetTop, insets.SystemWindowInsetRight, insets.SystemWindowInsetBottom);
#pragma warning restore CA1422
            }
            return insets;
        }
    }
    private sealed class BackCallback(Action action) : Java.Lang.Object, Android.Window.IOnBackInvokedCallback
    {
        public void OnBackInvoked() => action();
    }

    private void ApplyAccentMarkers(TextView view, string value)
    {
        var matches = Regex.Matches(value, @"\*([^*\r\n]+)\*");

        if (matches.Count == 0)
            return;

        var plain = new StringBuilder(value.Length);
        var spans = new List<(int Start, int Length)>();

        int position = 0;

        foreach (Match match in matches)
        {
            plain.Append(value, position, match.Index - position);

            string highlighted = match.Groups[1].Value;

            spans.Add((plain.Length, highlighted.Length));
            plain.Append(highlighted);

            position = match.Index + match.Length;
        }

        plain.Append(value, position, value.Length - position);

        var formatted = new SpannableString(plain.ToString());

        foreach (var span in spans)
        {
            formatted.SetSpan(
                new ForegroundColorSpan(palette.Accent),
                span.Start,
                span.Start + span.Length,
                SpanTypes.ExclusiveExclusive
            );
        }

        view.SetText(formatted, TextView.BufferType.Spannable);
    }
}
