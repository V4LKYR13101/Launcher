using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using LauncherApp.UI;
namespace LauncherApp;

public partial class MainActivity
{
    private static string Counted(int count, string singular, string? plural = null) => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
    private void SectionCaption(LinearLayout parent, string title)
    {
        if (FilteringSettings && ReferenceEquals(parent, body))
            return;
        var label = Text(title.ToUpperInvariant(), 10, true, palette.Muted);
        label.LetterSpacing = .10f;
        label.SetPadding(0, Dp(20), 0, Dp(6));
        parent.AddView(label, Match());
    }
    private void TouchFeedback(View view, int radius = 12, HapticCue cue = HapticCue.Tap)
    {
        var ink = new Color(palette.Text.R, palette.Text.G, palette.Text.B, (byte)25);
        view.Foreground = new RippleDrawable(ColorStateList.ValueOf(ink), null, Background(Color.White, radius));
        view.Focusable = true;
        BindTapHaptics(view, cue);
        ApplyPressMotion(view);
    }
    private static int AppLabelLines(string label) => label.Trim().Contains(' ') ? 2 : 1;
    private float FontScale => Math.Max(1, Resources?.Configuration?.FontScale ?? 1);
    private void StyleEntry(EditText input)
    {
        BindNativeHaptics(input);
        input.Typeface = regular;
        input.SetTextColor(palette.Text);
        input.SetHintTextColor(palette.Muted);
        input.SetHighlightColor(new Color(palette.Accent.R, palette.Accent.G, palette.Accent.B, (byte)64));
        if (input.Background is not null and not GradientDrawable)
            input.BackgroundTintList = new ColorStateList([new[] { Android.Resource.Attribute.StateFocused }, Array.Empty<int>()], [palette.Action.ToArgb(), palette.Divider.ToArgb()]);
        input.FocusChange += (_, e) => { if (input.Background is GradientDrawable box) box.SetStroke(Dp(1), e.HasFocus ? palette.Action : palette.Divider); };
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            input.TextCursorDrawable?.Mutate()?.SetTint(palette.Action);
    }
    private void StyleSearchEntry(EditText input, int cornerDp, bool inSheet = false)
    {
        StyleEntry(input);
        if (!OperatingSystem.IsAndroidVersionAtLeast(33) || !state.LiquidGlassCards)
            return;
        if (!inSheet)
        {
            if (ApplyGlassCard(input, cornerDp, persistent: true))
                MakeSearchFieldTransparent(input, cornerDp);
            return;
        }

        input.ViewAttachedToWindow += Attach;
        void Attach(object? sender, View.ViewAttachedToWindowEventArgs e)
        {
            input.ViewAttachedToWindow -= Attach;
            if (!OperatingSystem.IsAndroidVersionAtLeast(33) || !state.LiquidGlassCards)
                return;
            for (var parent = input.Parent as View; parent is not null; parent = parent.Parent as View)
            {
                if (parent is not MaxHeightStack sheet)
                    continue;
                if (sheet.SearchGlass is null)
                {
                    var glass = new LiquidGlass(sheet, palette, sheet.Background);
                    int start = sheet.PaddingStart, top = sheet.PaddingTop, end = sheet.PaddingEnd, bottom = sheet.PaddingBottom;
                    sheet.Background = new LayerDrawable([sheet.Background!, glass]);
                    sheet.SetPaddingRelative(start, top, end, bottom);
                    sheet.SearchGlass = glass;
                }
                sheet.SearchGlass.AddPanel(input, cornerDp);
                MakeSearchFieldTransparent(input, cornerDp);
                return;
            }
        }
    }

    private void MakeSearchFieldTransparent(EditText input, int cornerDp)
    {
        int start = input.PaddingStart, end = input.PaddingEnd, top = input.PaddingTop, bottom = input.PaddingBottom;
        input.BackgroundTintList = null;
        input.Background = Background(Color.Transparent, cornerDp);
        input.SetPaddingRelative(start, top, end, bottom);
    }

    private EditText FormField(LinearLayout panel, string label, string value, string hint = "", bool multiline = false, bool search = false)
    {
        var caption = Text(label.ToUpperInvariant(), 10, true, palette.Muted);
        caption.LetterSpacing = .08f;
        caption.SetPadding(Dp(4), Dp(12), 0, Dp(8));
        panel.AddView(caption, Match());
        var input = new EditText(this) { Text = value, Hint = hint, TextSize = 16, Typeface = regular };
        input.SetSingleLine(!multiline);
        if (multiline)
        {
            input.SetMinLines(3);
            input.SetMaxLines(6);
            input.Gravity = GravityFlags.Top;
        }
        input.Background = Background(palette.Card, 12, palette.Divider);
        input.SetPadding(Dp(14), Dp(12), Dp(14), Dp(12));
        if (search)
            StyleSearchEntry(input, 12, inSheet: true);
        else
            StyleEntry(input);
        panel.AddView(input, Match(multiline ? -2 : Math.Max(Dp(52), Dp(16 * FontScale * 1.3f + 24))));
        return input;
    }
    private Action? pendingBodyRefresh;
    private long lastHomeTouch;
    private void CancelBodyRefresh()
    {
        if (pendingBodyRefresh is { } refresh)
            handler.RemoveCallbacks(refresh);
        pendingBodyRefresh = null;
    }
    private void RefreshBodyInPlace()
    {
        if (todayLayoutEditor is not null || pendingBodyRefresh is not null)
            return;
        string requestedPage = page;
        var target = scroll;
        int observed = target.ScrollY;
        pendingBodyRefresh = () =>
        {
            if (IsDestroyed || !resumed || page != requestedPage || !ReferenceEquals(scroll, target))
            {
                CancelBodyRefresh();
                return;
            }
            if (depthTouchActive || openSheetCount > 0 || pageSlide is not null || drawerAnimations.Count > 0 || rootFoldAnimations.Count > 0
                || System.Environment.TickCount64 - lastHomeTouch < 220 || target.ScrollY != observed)
            {
                observed = target.ScrollY;
                handler.PostDelayed(pendingBodyRefresh!, 100);
                return;
            }
            pendingBodyRefresh = null;
            int y = target.ScrollY, offset = 0;
            string? anchor = null;
            for (int i = 0; i < body.ChildCount; i++)
            if (body.GetChildAt(i) is { } child && child.Bottom > y && child.Tag is { } tag)
            {
                anchor = tag.ToString();
                offset = y - child.Top;
                break;
            }
            RenderBody();
            target.Post(() =>
            {
                if (IsDestroyed || !ReferenceEquals(scroll, target) || page != requestedPage)
                    return;
                int position = anchor is not null && body.FindViewWithTag(anchor) is { } same ? same.Top + offset : y;
                target.ScrollTo(0, Math.Max(0, position));
            });
        };
        handler.PostDelayed(pendingBodyRefresh, 80);
    }
    private void DialogActions(LinearLayout panel, (string Title, Action Action) secondary, (string Title, Action Action) primary)
    {
        bool stacked = FontScale >= 1.4f;
        var row = Stack(!stacked);
        row.BaselineAligned = false;
        row.SetGravity(GravityFlags.FillVertical);
        var first = stacked ? Match() : new LinearLayout.LayoutParams(0, -1, 1);
        first.SetMargins(0, 0, stacked ? 0 : Dp(4), stacked ? Dp(8) : 0);
        row.AddView(Button(secondary.Title, secondary.Action), first);
        var second = stacked ? Match() : new LinearLayout.LayoutParams(0, -1, 1);
        second.SetMargins(stacked ? 0 : Dp(4), 0, 0, 0);
        row.AddView(Button(primary.Title, primary.Action, true), second);
        panel.AddView(row, Match());
    }
}
