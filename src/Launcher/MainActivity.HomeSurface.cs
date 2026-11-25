using Android.Animation;
using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Views.Accessibility;
using Android.Widget;
using LauncherApp.Services;
using LauncherApp.UI;
namespace LauncherApp;

public partial class MainActivity
{
    private readonly HashSet<Dialog> activeSheets = [];
    private string settingsQuery = "";
    private bool FilteringSettings => page == "Settings" && settingsQuery.Trim().Length > 0;
    private bool SettingMatches(string text)
    {
        if (!FilteringSettings)
            return true;
        return UiCulture.CompareInfo.IndexOf(text.Replace("*", ""), settingsQuery.Trim(),
            System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
    }

    private static string SettingsAliases(string title)
    {
        if (title == Localize(Resource.String.ui_appearance))
            return Localize(Resource.String.ui_accent_color_colour_theme_columns_page_tabs_navigation_icons);
        if (title == Localize(Resource.String.ui_setup_feature_health))
            return Localize(Resource.String.ui_recovery_permissions_support_battery_sensor);
        if (title == Localize(Resource.String.ui_root))
            return Localize(Resource.String.ui_today_agenda_calendar_checklists_reminders_cards_widgets);
        return "";
    }
    private bool depthAllowed = true;
    private bool DepthAvailable => depthAllowed;
    private static string PageLabel(string value) => value switch
    {
        "Root" => Localize(Resource.String.ui_today),
        "Tiles" => Localize(Resource.String.ui_apps),
        "Conversations" => Localize(Resource.String.ui_notifications),
        "Root settings" => Localize(Resource.String.ui_today_cards),
        "Settings" => Localize(Resource.String.ui_settings),
        "Appearance" => Localize(Resource.String.ui_appearance),
        "Theme & wallpaper" => Localize(Resource.String.ui_theme_wallpaper),
        "Tiles & icons" => Localize(Resource.String.ui_tiles_icons),
        "Motion & depth" => Localize(Resource.String.ui_motion_depth),
        "Navigation & status" => Localize(Resource.String.ui_navigation_status),
        "Apps & drawers" => Localize(Resource.String.ui_apps_drawers),
        "Notifications & panels" => Localize(Resource.String.ui_notifications_panels),
        SearchSettingsPage => Localize(Resource.String.ui_search_settings_title),
        "Image search" => Localize(Resource.String.ui_image_search),
        "Document search" => Localize(Resource.String.ui_document_search),
        "People" => Localize(Resource.String.ui_people),
        "Downloads" => Localize(Resource.String.ui_downloads),
        "Automatic theme" => Localize(Resource.String.ui_automatic_theme),
        "Updates" => Localize(Resource.String.ui_updates),
        "Grayscale setup" => Localize(Resource.String.ui_grayscale_setup),
        "Integrations" => Localize(Resource.String.ui_integrations),
        "Desktop companion" => Localize(Resource.String.ui_desktop_companion),
        "Drawers" => Localize(Resource.String.ui_drawers),
        "Hidden" => Localize(Resource.String.ui_hidden),
        "Permissions" => Localize(Resource.String.ui_permissions),
        "Weather" => Localize(Resource.String.ui_weather),
        "Wear OS" => Localize(Resource.String.ui_wear_os),
        "Quick panel" => Localize(Resource.String.ui_quick_panel),
        "Speech & audio" => Localize(Resource.String.ui_speech_audio),
        "App drawer" => Localize(Resource.String.ui_app_drawer),
        "All apps drawer" => Localize(Resource.String.ui_all_apps),
        _ => value
    };

    private Drawable Surface(Color color, int radius = 20, bool accent = false)
    {
        var top = accent ? new Color((byte)((color.R * 7 + palette.Accent.R) / 8), (byte)((color.G * 7 + palette.Accent.G) / 8), (byte)((color.B * 7 + palette.Accent.B) / 8)) : new Color((byte)Math.Min(255, color.R + 5), (byte)Math.Min(255, color.G + 5), (byte)Math.Min(255, color.B + 5));
        var shape = new GradientDrawable(GradientDrawable.Orientation.TlBr, [top.ToArgb(), color.ToArgb()]);
        shape.SetCornerRadius(Dp(radius));
        shape.SetStroke(Dp(1), accent ? new Color(palette.Accent.R, palette.Accent.G, palette.Accent.B, (byte)60) : palette.Divider);
        return shape;
    }
    private void AddHomeNavigation()
    {
        if (!state.ShowHomeNavigation || state.HomeNavigationPages.Count == 0 || searching)
            return;
        var surround = Stack();
        surround.SetPadding(Dp(16), Dp(6), Dp(16), Dp(6));
        var bar = Stack(true);
        bar.BaselineAligned = false;
        bar.Background = Surface(palette.Card, 24);
        bar.SetPadding(Dp(4), Dp(4), Dp(4), Dp(4));
        foreach (var (target, glyph) in new[] { ("Root", "calendar"), ("Tiles", "grid"), ("Conversations", "message") })
        {
            if (!state.HomeNavigationPages.Contains(target))
                continue;
            bool selected = page == target;
            var ink = selected ? palette.OnAccent : palette.Muted;
            var item = Stack(true);
            item.SetGravity(GravityFlags.Center);
            item.SetPadding(Dp(5), Dp(8), Dp(5), Dp(8));
            item.ContentDescription = PageLabel(target) + (selected ? Localize(Resource.String.ui_selected) : "");
            item.Selected = selected;
            item.Background = selected ? Background(palette.Accent, 20) : null;
            if (FontScale < 1.4f)
                item.AddView(new GlyphView(this, glyph, ink), new LinearLayout.LayoutParams(Dp(17), Dp(17)));
            var label = Text(target == "Conversations" && FontScale >= 1.4f ? Localize(Resource.String.ui_alerts) : PageLabel(target), 11, true, ink);
            label.SetSingleLine(true);
            label.Gravity = GravityFlags.Center;
            label.SetPadding(Dp(5), 0, 0, 0);
            item.AddView(label);
            item.SetMinimumHeight(Dp(48));
            TouchFeedback(item, 20);
            item.Click += (_, _) => { if (page == target) scroll.SmoothScrollTo(0, 0); else Navigate(target); };
            bar.AddView(item, new LinearLayout.LayoutParams(0, -1, 1));
        }
        surround.AddView(bar, Match());
        shell.AddView(surround, Match());
    }
    private void AddSettingsSearch()
    {
        var row = Stack(true);
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Dp(16), Dp(4), Dp(16), Dp(8));
        var input = new EditText(this) { Text = settingsQuery, Hint = Localize(Resource.String.ui_search_settings), TextSize = 15, ContentDescription = Localize(Resource.String.ui_search_settings) };
        input.SetSingleLine(true);
        input.Background = Surface(palette.Card, 16);
        input.SetPadding(Dp(16), Dp(10), Dp(16), Dp(10));
        StyleSearchEntry(input, 16);
        row.AddView(input, new LinearLayout.LayoutParams(0, -2, 1));
        input.TextChanged += (_, _) =>
        {
            RememberHomeScroll();
            settingsQuery = input.Text ?? "";
            homeScrollPositions.Remove(SettingsSearchScrollKey);
            RenderBody();
            RestoreHomeScroll();
        };
        shell.AddView(row, Match());
    }
    private void BuildOverview(LinearLayout hero)
    {
        var scene = new FrameLayout(this);
        scene.SetMinimumHeight(Dp(128));
        scene.AddView(new OverviewArtwork(this, palette.Accent) { ImportantForAccessibility = ImportantForAccessibility.No }, new FrameLayout.LayoutParams(Dp(180), -1, GravityFlags.Right));

        var highlight = BuildOverviewHighlight();
        scene.AddView(highlight ?? BuildOverviewDate(), new FrameLayout.LayoutParams(-1, -2, GravityFlags.CenterVertical));
        hero.AddView(scene, Match());
        RootActions(hero, (Localize(Resource.String.ui_capture_action), ShowCapture), (Localize(Resource.String.ui_search), StartSearch), (Localize(Resource.String.ui_arrange), BeginTodayLayout));
        if (!state.SetupCompleted)
        {
            Gap(hero, 8);
            hero.AddView(Button(Localize(Resource.String.ui_set_up_your_home), ShowFeatureHealth), Match());
        }
    }

    private LinearLayout BuildOverviewDate()
    {
        var date = Stack(true);
        date.SetGravity(GravityFlags.CenterVertical);
        date.SetPadding(0, Dp(4), 0, Dp(10));
        date.AddView(Text(DateTime.Today.ToString("dd"), 58, true, palette.Accent));
        var context = Stack();
        context.SetPadding(Dp(14), 0, 0, 0);
        context.AddView(Text(DateTime.Today.ToString("dddd", UiCulture), 21, true), Match());
        var month = Text(DateTime.Today.ToString("MMMM yyyy", UiCulture).ToUpperInvariant(), 10, true, palette.Muted);
        month.LetterSpacing = .12f;
        month.SetPadding(0, Dp(6), 0, 0);
        context.AddView(month, Match());
        date.AddView(context, new LinearLayout.LayoutParams(0, -2, 1));
        return date;
    }

    private LinearLayout? BuildOverviewHighlight()
    {
        long now = Now;
        var timer = state.RootHidden.Contains("timers") ? null :
            state.Timers.Where(item => item.EndsAt > now).MinBy(item => item.EndsAt);
        if (timer is not null)
        {
            var countdown = Text(TimerDisplay(timer), 36, true, palette.Accent);
            overviewTimerView = (timer, countdown);
            return OverviewHighlight(Localize(Resource.String.ui_running_timer), countdown, timer.Title, Localize(Resource.String.ui_pause_timer), () => ToggleTimer(timer));
        }

        if (!state.CalendarVisible || state.RootHidden.Contains("calendar") || !AgendaReader.Allowed(this))
            return null;

        RefreshAgenda();
        var next = agendaEvents.FirstOrDefault(item => item.Ends > now && item.Begins < now + 24 * 60 * 60 * 1000);
        return next is null ? null : OverviewHighlight(next.Begins <= now && !next.AllDay ? Localize(Resource.String.ui_on_now) : Localize(Resource.String.ui_on_your_agenda),
            Text(next.Title, 24, true), AgendaTime(next), Localize(Resource.String.ui_open_in_calendar), () => OpenAgendaEvent(next));
    }

    private LinearLayout OverviewHighlight(string caption, TextView title, string detail, string actionLabel, System.Action action)
    {
        var group = Stack();
        group.SetPadding(0, Dp(8), 0, Dp(8));
        var label = Text(caption, 12, true, palette.Accent);
        group.AddView(label, Match());
        title.SetPadding(0, Dp(4), 0, Dp(4));
        title.SetMaxLines(2);
        title.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        group.AddView(title, Match());
        var summary = Text(detail, 13, color: palette.Muted);
        summary.SetMaxLines(1);
        summary.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        group.AddView(summary, Match());
        group.AddView(RootAction(actionLabel, action), new LinearLayout.LayoutParams(-2, -2));
        return group;
    }
    private void ShowCapture() => Choose(Resource.String.ui_capture_something, [Localize(Resource.String.ui_note), Localize(Resource.String.ui_checklist), Localize(Resource.String.ui_reminder), Localize(Resource.String.ui_timer), Localize(Resource.String.ui_journey), Localize(Resource.String.ui_link)], i =>
    {
        if (i == 0)
            CreateNote("");
        else if (i == 1)
            EditChecklist();
        else if (i == 2)
            EditReminder();
        else if (i == 3)
            Prompt(Resource.String.ui_timer_duration, "10 min", CreateTimer);
        else if (i == 4)
            EditTicket(new(), true);
        else
            Prompt(Resource.String.ui_save_a_link, "", CreateNoteFromShare);
    });
    private void DismissSheets()
    {
        foreach (var dialog in activeSheets.ToArray())
            dialog.Dismiss();
    }
    private sealed class ButtonSemantics : View.AccessibilityDelegate
    {
        public override void OnInitializeAccessibilityNodeInfo(View host, AccessibilityNodeInfo info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            info.ClassName = "android.widget.Button";
        }
    }
    private void ApplyPressMotion(View view)
    {
        view.SetAccessibilityDelegate(new ButtonSemantics());
        if (!LayerMotionEnabled)
            return;
        var press = new AnimatorSet();
        press.PlayTogether(ObjectAnimator.OfFloat(view, "scaleX", .985f)!, ObjectAnimator.OfFloat(view, "scaleY", .985f)!);
        press.SetDuration(90);
        var release = new AnimatorSet();
        release.PlayTogether(ObjectAnimator.OfFloat(view, "scaleX", 1f)!, ObjectAnimator.OfFloat(view, "scaleY", 1f)!);
        release.SetDuration(160);
        var states = new StateListAnimator();
        states.AddState([Android.Resource.Attribute.StatePressed], press);
        states.AddState([], release);
        view.StateListAnimator = states;
    }
}
