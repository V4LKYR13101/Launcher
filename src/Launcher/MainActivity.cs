using Android.App;
using Android.App.Roles;
using Android.Appwidget;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.UI;
using Color = Android.Graphics.Color;
using Uri = Android.Net.Uri;

namespace LauncherApp;

[Activity(Name = "com.launcher.home.MainActivity", Label = "Launcher", MainLauncher = true,
    Exported = true, LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden,
    WindowSoftInputMode = SoftInput.AdjustResize)]
[IntentFilter([Intent.ActionMain], Categories = [Intent.CategoryHome, Intent.CategoryDefault])]
public partial class MainActivity : Activity
{
    private LauncherState state = new();
    private StateStore store = null!;
    private Palette palette = null!;
    private List<AppEntry> apps = [];
    private Dictionary<string, long> usage = new();
    private FrameLayout root = null!;
    private LinearLayout shell = null!, body = null!;
    private ScrollView scroll = null!;
    private EditText? searchInput;
    private TextView? timerText;
    private TextView? statusTime;
    private Android.Window.IOnBackInvokedCallback? backCallback;
    private string page = "Tiles", query = "";
    private bool searching, resumed, loading;
    private float touchX, touchY;
    private bool panelDragging;
    private long panelTouchStarted;
    private long nextWeatherCheck;
    private bool panelTouchInNotifications;
    private readonly Handler handler = new(Looper.MainLooper!);
    private readonly List<(TextView View,string Package)> badgeViews=[];
    private Dictionary<string,int> badgeCounts=new();
    private readonly Dictionary<string,Drawable> iconCache=new();
    private int searchLimit=24;
    private bool searchOpening;
    private Action? pendingSearch;
    private Action? tick;
    private AppWidgetHost widgetHost = null!;
    private ConnectionMonitor connections=null!;
    private MediaMonitor media=null!;
    private int pendingWidget = -1;
    private Typeface regular = Typeface.Create("sans-serif", TypefaceStyle.Normal)!;
    private Typeface bold = Typeface.Create("sans-serif", TypefaceStyle.Bold)!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
#if DEBUG
        if(Intent?.GetBooleanExtra("qa_keep_awake",false)==true)Window!.AddFlags(WindowManagerFlags.KeepScreenOn);
#endif
        store = new(this); state = store.Load();
        if (savedInstanceState is not null)
        {
            pendingWidget = savedInstanceState.GetInt("pending-widget", -1);
            page = savedInstanceState.GetString("page") ?? "Tiles";
        }
        try
        {
            regular = Typeface.CreateFromAsset(Assets, "Outfit-Regular.ttf")!;
            bold = Typeface.CreateFromAsset(Assets, "Outfit-Bold.ttf")!;
        }
        catch (Java.Lang.RuntimeException) { }
        widgetHost = new AppWidgetHost(this, 2106);
        connections=new ConnectionMonitor(this,()=>RunOnUiThread(()=>{if(resumed && page=="Quick panel")RenderBody();}));
        media=new MediaMonitor(this,MediaChanged);
        root = new FrameLayout(this);
        root.SetOnApplyWindowInsetsListener(new InsetListener(()=>state.MinimalStatusBar && IsHomePage && state.StatusPlacement=="Top"));
        // Android 15+ enforces edge-to-edge; older versions opt in here.
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && !OperatingSystem.IsAndroidVersionAtLeast(35)) Window!.SetDecorFitsSystemWindows(false);
        SetContentView(root);
        ConversationService.Changed += ConversationsChanged;
        if(OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            backCallback=new BackCallback(HandleBack);
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(0,backCallback);
        }
        Render();
    }
    protected override void OnSaveInstanceState(Bundle outState)
    {
        outState.PutInt("pending-widget", pendingWidget); outState.PutString("page", page);
        base.OnSaveInstanceState(outState);
    }
    protected override void OnStart()
    {
        base.OnStart();
        try { widgetHost.StartListening(); } catch (Java.Lang.RuntimeException) { }
    }
    protected override void OnStop() { widgetHost.StopListening(); base.OnStop(); }
    protected override void OnResume()
    {
        base.OnResume(); resumed = true; connections.Start();media.Start();StartBatteryUpdates();RefreshApps();RefreshWeather();
        ResumeUpdateInstall();
        tick = () =>
        {
            if (!resumed) return;
            if (state.FocusEndsAt > 0 && Now >= state.FocusEndsAt)
            {
                state.FocusEndsAt = 0; state.Focus = false; Save(); Render();
            }
            if (timerText is not null && timerText.Text!=FocusTime()) timerText.Text = FocusTime();
            string time=DateTime.Now.ToString("HH:mm   ·   ddd, d MMM");
            if (statusTime is not null && statusTime.Text!=time) statusTime.Text = time;
            UpdateMediaProgress();
            if(Now>=nextWeatherCheck){nextWeatherCheck=Now+60_000L;RefreshWeather();}
            handler.PostDelayed(tick!, 1000);
        };
        handler.Post(tick);
    }
    protected override void OnPause()
    {
        resumed = false;
        connections.Stop();
        media.Stop();StopBatteryUpdates();
        if (tick is not null) handler.RemoveCallbacks(tick);
        base.OnPause();
    }
    protected override void OnDestroy()
    {
        updateLifetime.Cancel();
        ConversationService.Changed -= ConversationsChanged;
        if(OperatingSystem.IsAndroidVersionAtLeast(33) && backCallback is not null) OnBackInvokedDispatcher.UnregisterOnBackInvokedCallback(backCallback);
        base.OnDestroy();
    }
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
#if DEBUG
        if(intent?.GetBooleanExtra("qa_keep_awake",false)==true)Window!.AddFlags(WindowManagerFlags.KeepScreenOn);
#endif
        if (intent?.HasCategory(Intent.CategoryHome) == true)
        {
            HideKeyboard(); searching = false; query = ""; page = "Tiles"; Render();
        }
    }
    public override void OnBackPressed() => HandleBack();
    private void HandleBack()
    {
        if (searching) { HideKeyboard(); searching = false; query = ""; Render(); }
        else if (page != "Tiles") { page = "Tiles"; Render(); }
        else scroll.SmoothScrollTo(0, 0);
    }
    private async void RefreshApps()
    {
        if (loading) return;
        loading = true;
        try
        {
            var result = await Task.Run(() => (Apps: AppCatalog.Load(this), Usage: AppCatalog.UsageToday(this)));
            if (IsDestroyed) return;
            apps = result.Apps; usage = result.Usage;
            AppOrganization.Reconcile(state, apps); Save();
            if (!searching && resumed) Render();
        }
        catch (Exception e) when (e is Java.Lang.RuntimeException or InvalidOperationException)
        { Toast("Could not refresh apps. Reopen Launcher to retry."); }
        finally { loading = false; }
    }
    private void ConversationsChanged() => RunOnUiThread(() =>
    {
        badgeCounts=ConversationService.BadgeCounts();
        if(!resumed)return;
        if(page is "Conversations" or "Quick panel" && !searching)RenderBody();
        else foreach(var (badge,package) in badgeViews)UpdateBadge(badge,package);
    });
    private static long Now => DateTimeOffset.Now.ToUnixTimeMilliseconds();
    private void Save() => store.Save(state);
    private int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density + .5f);
    private LinearLayout Stack(bool horizontal = false) => new(this) { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical };
    private static LinearLayout.LayoutParams Match(int height = -2) => new(-1, height);
    private GradientDrawable Background(Color color, int radius = 12, Color? stroke = null)
    {
        var bg = new GradientDrawable(); bg.SetColor(color); bg.SetCornerRadius(Dp(radius));
        if (stroke.HasValue) bg.SetStroke(Dp(1), stroke.Value);
        return bg;
    }
    private Android.Content.Res.ColorStateList AccentTint()=>new(
        [new[]{Android.Resource.Attribute.StateChecked},Array.Empty<int>()],
        [palette.Accent.ToArgb(),palette.Muted.ToArgb()]);
    private TextView Text(string value, float size = 14, bool strong = false, Color? color = null)
    {
        var view = new TextView(this) { Text = value, TextSize = size, Typeface = strong ? bold : regular };
        view.SetTextColor(color ?? palette.Text); view.SetIncludeFontPadding(false);
        return view;
    }
    private TextView Button(string value, Action action, bool filled = false)
    {
        var view = Text(value, 14, true, filled ? palette.OnAccent : palette.Text);
        view.Gravity = GravityFlags.Center; view.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12)); view.SetMinimumHeight(Dp(48));
        view.Background = Background(filled ? palette.Accent : palette.Panel, 28);
        view.Click += (_, _) => action(); return view;
    }
    private View IconButton(string glyph, string description, Action action)
    {
        var frame = new FrameLayout(this) { ContentDescription = description };
        frame.SetMinimumWidth(Dp(48)); frame.SetMinimumHeight(Dp(48));
        frame.AddView(new GlyphView(this, glyph, palette.Text), new FrameLayout.LayoutParams(Dp(22), Dp(22), GravityFlags.Center));
        frame.Click += (_, _) => action(); return frame;
    }
    private void Gap(LinearLayout parent, int height = 12) => parent.AddView(new View(this), Match(Dp(height)));
    private void Paragraph(string text, LinearLayout? parent = null)
    {
        var v = Text(text, 13, color: palette.Muted); v.SetLineSpacing(Dp(3), 1);
        v.SetPadding(0, Dp(4), 0, Dp(12)); (parent ?? body).AddView(v, Match());
    }
    private new void Title(string title)
    {
        var v = Text(title, 28, true); v.SetPadding(0, Dp(14), 0, Dp(18)); body.AddView(v, Match());
    }
    private void Toast(string message) => Android.Widget.Toast.MakeText(this, message, ToastLength.Short)!.Show();
    private void ApplySystemBars()
    {
        bool light=palette.Light && !state.UseWallpaper;
        if(OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            const int lightStatus=8, lightNavigation=16;
            Window!.InsetsController?.SetSystemBarsAppearance(light ? lightStatus|lightNavigation : 0,lightStatus|lightNavigation);
            if(state.MinimalStatusBar && IsHomePage) Window.InsetsController?.Hide(WindowInsets.Type.StatusBars());
            else Window.InsetsController?.Show(WindowInsets.Type.StatusBars());
        }
        else
        {
#pragma warning disable CA1422 // Legacy bar appearance for Android 7–10.
            var flags=light ? SystemUiFlags.LightStatusBar : 0;
            if(light && OperatingSystem.IsAndroidVersionAtLeast(26)) flags|=SystemUiFlags.LightNavigationBar;
            Window!.DecorView.SystemUiFlags=flags;
            if(state.MinimalStatusBar && IsHomePage)Window.AddFlags(WindowManagerFlags.Fullscreen);
            else Window.ClearFlags(WindowManagerFlags.Fullscreen);
#pragma warning restore CA1422
        }
    }
    private void Render()
    {
        palette = new(state); timerText = null; statusTime=null;batteryIcon=null;batteryPercentText=null; root.RemoveAllViews();
        Window!.SetBackgroundDrawable(new ColorDrawable(state.UseWallpaper ? Color.Transparent : palette.Base));
        if (state.UseWallpaper) Window.AddFlags(WindowManagerFlags.ShowWallpaper);
        else Window.ClearFlags(WindowManagerFlags.ShowWallpaper);
        root.SetBackgroundColor(state.UseWallpaper ? new Color(0,0,0,110) : palette.Base);
        ApplySystemBars();
        shell = Stack(); root.AddView(shell, new FrameLayout.LayoutParams(-1, -1));
        if(IsHomePage && state.MinimalStatusBar) AddMinimalStatus();
        if (!IsHomePage) AddPageHeader(page);
        body = Stack(); body.SetPadding(Dp(16), Dp(14), Dp(16), Dp(12));
        body.LongClick += (_, _) => { if(page=="Tiles" && !searching) QuickControls(); };
        scroll = new ScrollView(this) { FillViewport = true, VerticalScrollBarEnabled = false };
        scroll.SetClipToPadding(false);
        if(page=="Quick panel")shell.AddView(body,new LinearLayout.LayoutParams(-1,0,1));
        else
        {
            scroll.AddView(body, new ScrollView.LayoutParams(-1, -2));
            shell.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        }
        RenderBody();
        if (IsHomePage) AddSearchBar();
        if(page=="Quick panel")
        {
            var close=new FrameLayout(this){ContentDescription="Close quick panel"};
            var handle=new View(this){Background=Background(palette.Muted,4)};
            close.AddView(handle,new FrameLayout.LayoutParams(Dp(38),Dp(4),GravityFlags.Center));
            close.Click+=(_,_)=>Navigate("Tiles");shell.AddView(close,Match(Dp(30)));
        }
        root.RequestApplyInsets();
    }
    private bool IsHomePage => page is "Tiles" or "Root" or "Conversations";
    private void AddPageHeader(string title)
    {
        var row = Stack(true); row.SetGravity(GravityFlags.CenterVertical);
        row.AddView(IconButton("back", "Back", () => Navigate(page is "Settings" or "Quick panel" ? "Tiles" : "Settings")), new LinearLayout.LayoutParams(Dp(52), Dp(52)));
        row.AddView(Text(title, 21, true), new LinearLayout.LayoutParams(0, -2, 1)); shell.AddView(row, Match(Dp(58)));
    }
    private void RenderBody()
    {
        int previousFeedY=page=="Quick panel"?scroll.ScrollY:0;
        body.RemoveAllViews(); badgeViews.Clear();badgeCounts=ConversationService.Enabled(this)?ConversationService.BadgeCounts():new();timerText = null;mediaProgress=null;mediaTime=null; body.SetGravity(GravityFlags.Top);
        if (searching)
        {
            if(searchOpening){Title("Search");Paragraph(page=="Conversations"?"Find a notification.":"Find an app by name.");}
            else RenderSearchResults();return;
        }
        switch (page)
        {
            case "Tiles": RenderTiles(); break;
            case "Root": RenderRoot(); break;
            case "Conversations": RenderConversations(); break;
            case "Settings": RenderSettings(); break;
            case "Updates": RenderUpdates(); break;
            case "Appearance": RenderAppearance(); break;
            case "Drawers": RenderDrawerSettings(); break;
            case "Hidden": RenderHidden(); break;
            case "Permissions": RenderPermissions(); break;
            case "Root settings": RenderRootSettings(); break;
            case "Quick panel": RenderQuickPanel(); break;
            case "Weather": RenderWeatherSettings(); break;
        }
        if(page=="Quick panel" && previousFeedY>0){var feed=scroll;feed.Post(()=>feed.ScrollTo(0,previousFeedY));}
    }
    private void Navigate(string target) { HideKeyboard(); searching = false; query = ""; page = target; Render(); }
    private void AddSearchBar()
    {
        var bottom = Stack(); bottom.SetPadding(Dp(16), Dp(4), Dp(16), 0);
        var pill = Stack(true); pill.SetGravity(GravityFlags.CenterVertical); pill.Background = Background(palette.Card, 30);
        if (searching)
        {
            pill.AddView(IconButton("back", "Close search", () => { HideKeyboard(); searching=false; query=""; Render(); }), new LinearLayout.LayoutParams(Dp(44), Dp(52)));
            searchInput = new EditText(this) { Text = query, Hint = page == "Conversations" ? "Search notifications" : "Search apps", TextSize = 15, Typeface = regular };
            searchInput.SetSingleLine(true); searchInput.SetTextColor(palette.Text); searchInput.SetHintTextColor(palette.Muted);
            searchInput.Background = null; searchInput.SetPadding(Dp(4),0,0,0); searchInput.ImeOptions = ImeAction.Search;
            searchInput.TextChanged += (_, _) =>
            {
                query=searchInput.Text??"";searchLimit=24;
                if(pendingSearch is not null)handler.RemoveCallbacks(pendingSearch);
                pendingSearch=()=>{if(searching)RenderBody();};handler.PostDelayed(pendingSearch,40);
            };
            searchInput.EditorAction += (_, e) =>
            {
                if (page != "Conversations")
                {
                    var first = AppOrganization.Visible(state, apps, query).FirstOrDefault(); if (first is not null) Launch(first);
                }
                e.Handled = true;
            };
            pill.AddView(searchInput, new LinearLayout.LayoutParams(0, Dp(52), 1));
        }
        else
        {
            var control = IconButton("search", "Search; hold for launcher controls", StartSearch);
            control.LongClick += (_, _) => QuickControls(); pill.AddView(control, new LinearLayout.LayoutParams(Dp(44), Dp(52)));
            var prompt = Text(page == "Conversations" ? "Search notifications" : "Search apps", 15, color: palette.Muted);
            prompt.SetPadding(Dp(4),0,0,0); prompt.Gravity=GravityFlags.CenterVertical; prompt.Click += (_, _) => StartSearch();
            pill.AddView(prompt, new LinearLayout.LayoutParams(0, Dp(52), 1));
            pill.LongClick += (_, _) => QuickControls();
        }
        pill.AddView(IconButton("more","Launcher settings",()=>{HideKeyboard();QuickControls();}),new LinearLayout.LayoutParams(Dp(48),Dp(52)));
        bottom.AddView(pill, Match(Dp(52)));shell.AddView(bottom, Match());
    }
    private void StartSearch()
    {
        searching = true;searchOpening=true; query="";searchLimit=12; Render(); searchInput?.RequestFocus();
        searchInput?.Post(() =>
        {
            ((InputMethodManager?)GetSystemService(InputMethodService))?.ShowSoftInput(searchInput, ShowFlags.Implicit);
            searchOpening=false;handler.Post(()=>{if(searching)RenderBody();});
        });
    }
    private void HideKeyboard() => ((InputMethodManager?)GetSystemService(InputMethodService))?.HideSoftInputFromWindow(root.WindowToken, HideSoftInputFlags.None);
    public override bool DispatchTouchEvent(MotionEvent? ev)
    {
        if (ev is not null && (IsHomePage || page=="Quick panel") && !searching)
        {
            if (ev.Action == MotionEventActions.Down)
            {
                touchX=ev.GetX(); touchY=ev.GetY();panelDragging=false;panelTouchStarted=System.Environment.TickCount64;
                using var notificationBounds=new Rect();
                panelTouchInNotifications=page=="Quick panel" && scroll.GetGlobalVisibleRect(notificationBounds)
                    && notificationBounds.Contains((int)ev.RawX,(int)ev.RawY);
            }
            if(page=="Quick panel" && !panelTouchInNotifications)
            {
                float dx=ev.GetX()-touchX,dy=ev.GetY()-touchY;
                if(ev.Action==MotionEventActions.Move && (panelDragging || dy < -Dp(8) && Math.Abs(dy)>Math.Abs(dx)*1.2f))
                {
                    if(!panelDragging)
                    {
                        var canceled=MotionEvent.Obtain(ev)!;canceled.Action=MotionEventActions.Cancel;base.DispatchTouchEvent(canceled);canceled.Recycle();
                        panelDragging=true;shell.Animate()?.Cancel();
                    }
                    shell.TranslationY=Math.Min(0,dy);return true;
                }
                if(ev.Action==MotionEventActions.Up && panelDragging)
                {
                    float distance=Math.Max(0,-dy);
                    float speed=distance/Math.Max(.001f,(System.Environment.TickCount64-panelTouchStarted)/1000f);
                    panelDragging=false;
                    if(distance>Dp(45) || distance>Dp(15) && speed>Dp(450))
                        shell.Animate()!.TranslationY(-root.Height).SetDuration(140).WithEndAction(new Java.Lang.Runnable(()=>Navigate("Tiles"))).Start();
                    else shell.Animate()!.TranslationY(0).SetDuration(130).Start();
                    return true;
                }
                if(ev.Action==MotionEventActions.Cancel && panelDragging)
                {panelDragging=false;shell.Animate()!.TranslationY(0).SetDuration(130).Start();return true;}
            }
            if(ev.Action==MotionEventActions.Up && page=="Tiles" && scroll.ScrollY==0 && ev.GetY()-touchY>Dp(130)
                && Math.Abs(ev.GetX()-touchX)<Dp(65))
            {
                var canceled=MotionEvent.Obtain(ev)!;canceled.Action=MotionEventActions.Cancel;base.DispatchTouchEvent(canceled);canceled.Recycle();
                Navigate("Quick panel");return true;
            }
            if (ev.Action == MotionEventActions.Up && IsHomePage && Math.Abs(ev.GetX()-touchX)>Dp(80) && Math.Abs(ev.GetX()-touchX)>Math.Abs(ev.GetY()-touchY)*1.8f)
            {
                var cancel=MotionEvent.Obtain(ev)!; cancel.Action=MotionEventActions.Cancel; base.DispatchTouchEvent(cancel); cancel.Recycle();
                string[] pages=["Root","Tiles","Conversations"];
                int index=Array.IndexOf(pages,page)+(ev.GetX()<touchX ? 1 : -1);
                if(index>=0 && index<pages.Length) Navigate(pages[index]); return true;
            }
        }
        return base.DispatchTouchEvent(ev);
    }
    private void Launch(AppEntry app)
    {
        var rule=state.Apps[app.Id];
        if (rule.DailyGoalMinutes > 0 && usage.GetValueOrDefault(app.PackageName) >= rule.DailyGoalMinutes*60_000L)
            Confirm("Your daily goal",$"You have used {app.Label} for {AppOrganization.FormatDuration(usage.GetValueOrDefault(app.PackageName))} today. Open it intentionally?","Open app",()=>LaunchDirect(app));
        else LaunchDirect(app);
    }
    private void LaunchDirect(AppEntry app)
    {
        try
        {
            HideKeyboard();
            var intent = new Intent(Intent.ActionMain).AddCategory(Intent.CategoryLauncher)!
                .SetComponent(new ComponentName(app.PackageName,app.ActivityName))!.AddFlags(ActivityFlags.NewTask | ActivityFlags.ResetTaskIfNeeded);
            StartActivity(intent);
        }
        catch (Exception e) when (e is ActivityNotFoundException or Java.Lang.SecurityException) { Toast("This app is no longer available."); RefreshApps(); }
    }
    private void OpenIntent(Intent intent)
    {
        try { StartActivity(intent); }
        catch (Exception e) when (e is ActivityNotFoundException or Java.Lang.SecurityException) { Toast("No app is available for this action."); }
    }
    private void OpenUrl(string url) => OpenIntent(new Intent(Intent.ActionView,Uri.Parse(url)));
    private sealed class InsetListener(Func<bool>? drawAtTop=null) : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View? v, WindowInsets? insets)
        {
            if (insets is null) return null!;
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var i = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.Ime() | WindowInsets.Type.DisplayCutout());
                int top=drawAtTop?.Invoke()==true?insets.GetInsets(WindowInsets.Type.SystemBars()|WindowInsets.Type.Ime()).Top:i.Top;
                v?.SetPadding(i.Left,top,i.Right,i.Bottom);
            }
            else
            {
#pragma warning disable CA1422 // Compatibility branch for Android 7–10.
                v?.SetPadding(insets.SystemWindowInsetLeft,insets.SystemWindowInsetTop,insets.SystemWindowInsetRight,insets.SystemWindowInsetBottom);
#pragma warning restore CA1422
            }
            return insets;
        }
    }
    private sealed class BackCallback(Action action) : Java.Lang.Object, Android.Window.IOnBackInvokedCallback
    {
        public void OnBackInvoked()=>action();
    }
}
