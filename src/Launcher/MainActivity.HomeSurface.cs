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
        string aliases = text.StartsWith("Appearance ") ? " accent color colour theme columns page tabs navigation icons wallpaper typography corners motion" : text.StartsWith("Setup & feature health ") ? " recovery permissions support battery sensor" : text.StartsWith("Root ") ? " today agenda calendar checklists reminders cards widgets" : "";
        return (text + aliases).Contains(settingsQuery.Trim(), StringComparison.CurrentCultureIgnoreCase);
    }
    private bool depthAllowed = true;
    private bool DepthAvailable => depthAllowed;
    private string PageLabel(string value) => value switch { "Root" => "Today", "Tiles" => "Apps", "Conversations" => "Notifications", "Root settings" => "Today cards", _ => value };
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
            item.ContentDescription = PageLabel(target) + (selected ? ", selected" : "");
            item.Selected = selected;
            item.Background = selected ? Background(palette.Accent, 20) : null;
            if (FontScale < 1.4f)
                item.AddView(new GlyphView(this, glyph, ink), new LinearLayout.LayoutParams(Dp(17), Dp(17)));
            var label = Text(target == "Conversations" && FontScale >= 1.4f ? "Alerts" : PageLabel(target), 11, true, ink);
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
        var input = new EditText(this) { Text = settingsQuery, Hint = "Search settings", TextSize = 15, ContentDescription = "Search settings" };
        input.SetSingleLine(true);
        StyleEntry(input);
        input.Background = Surface(palette.Card, 16);
        input.SetPadding(Dp(16), Dp(10), Dp(16), Dp(10));
        row.AddView(input, new LinearLayout.LayoutParams(0, -2, 1));
        input.TextChanged += (_, _) => { settingsQuery = input.Text ?? ""; RenderBody(); };
        shell.AddView(row, Match());
    }
    private void BuildOverview(LinearLayout hero)
    {
        var scene = new FrameLayout(this);
        scene.SetMinimumHeight(Dp(128));
        scene.AddView(new OverviewArtwork(this, palette.Accent) { ImportantForAccessibility = ImportantForAccessibility.No }, new FrameLayout.LayoutParams(Dp(180), -1, GravityFlags.Right));
        var date = Stack(true);
        date.SetGravity(GravityFlags.CenterVertical);
        date.SetPadding(0, Dp(4), 0, Dp(10));
        date.AddView(Text(DateTime.Today.ToString("dd"), 58, true, palette.Accent));
        var context = Stack();
        context.SetPadding(Dp(14), 0, 0, 0);
        context.AddView(Text(DateTime.Today.ToString("dddd"), 21, true), Match());
        var month = Text(DateTime.Today.ToString("MMMM yyyy").ToUpperInvariant(), 10, true, palette.Muted);
        month.LetterSpacing = .12f;
        month.SetPadding(0, Dp(6), 0, 0);
        context.AddView(month, Match());
        date.AddView(context, new LinearLayout.LayoutParams(0, -2, 1));
        scene.AddView(date, new FrameLayout.LayoutParams(-1, -2, GravityFlags.CenterVertical));
        hero.AddView(scene, Match());
        int running = state.Timers.Count(t => t.EndsAt > 0);
        string summary = Counted(state.Notes.Count, "note") + "   /   " + Counted(running, "active timer");
        var status = Text(summary, 12, color: palette.Muted);
        status.SetPadding(0, 0, 0, Dp(8));
        hero.AddView(status, Match());
        RootActions(hero, ("+ Capture", ShowCapture), ("Search", StartSearch), ("Arrange", ShowLayoutEditor));
        if (!state.SetupCompleted)
        {
            Gap(hero, 8);
            hero.AddView(Button("Set up your home", ShowFeatureHealth), Match());
        }
    }
    private void ShowCapture() => Choose("Capture something", ["Note", "Checklist", "Reminder", "Timer", "Journey", "Link"], i =>
    {
        if (i == 0)
            CreateNote("");
        else if (i == 1)
            EditChecklist();
        else if (i == 2)
            EditReminder();
        else if (i == 3)
            Prompt("Timer duration", "10 min", CreateTimer);
        else if (i == 4)
            EditTicket(new(), true);
        else
            Prompt("Save a link", "", CreateNoteFromShare);
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
