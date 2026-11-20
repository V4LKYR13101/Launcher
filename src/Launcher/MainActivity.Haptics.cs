using Android.OS;
using Android.Views;
using Android.Widget;
using LauncherApp.UI;
namespace LauncherApp;

public partial class MainActivity
{
    private enum HapticCue
    {
        None, Tap, Selection, Hold, ToggleOn, ToggleOff, Commit, Reject
    }
    private sealed class HapticBinding
    {
        public bool Tap, Hold; public HapticCue Cue;
    }
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<View, HapticBinding> hapticBindings = new();
    private readonly List<WeakReference<View>> nativeHapticViews = [];
    private long lastHapticAt = -1000;

    private bool Haptic(View source, HapticCue cue)
    {
        if (state.HapticFeedback == "Off" || cue == HapticCue.None || !resumed)
            return false;
        if (Android.Provider.Settings.System.GetInt(ContentResolver, "haptic_feedback_enabled", 1) == 0)
        {
            Android.Util.Log.Debug("LauncherHaptics", $"cue={cue}; mode={state.HapticFeedback}; suppressed=system");
            return false;
        }
        long now = SystemClock.UptimeMillis();
        if (now - lastHapticAt < 80)
            return false;
        var surface = source.RootView ?? source;
        if (!surface.IsAttachedToWindow || !surface.HasWindowFocus)
            return false;
        FeedbackConstants effect;
        if (cue == HapticCue.Hold)
            effect = FeedbackConstants.LongPress;
        else if ((cue is HapticCue.ToggleOn or HapticCue.ToggleOff) && OperatingSystem.IsAndroidVersionAtLeast(34))
            effect = cue == HapticCue.ToggleOn ? FeedbackConstants.ToggleOn : FeedbackConstants.ToggleOff;
        else if (cue == HapticCue.Reject && OperatingSystem.IsAndroidVersionAtLeast(30))
            effect = FeedbackConstants.Reject;
        else if (cue == HapticCue.Commit && OperatingSystem.IsAndroidVersionAtLeast(30))
            effect = state.HapticFeedback == "Crisp" ? FeedbackConstants.Confirm : FeedbackConstants.GestureEnd;
        else
            effect = state.HapticFeedback == "Crisp" ? FeedbackConstants.ContextClick : FeedbackConstants.ClockTick;
        // The decor view avoids duplicate built-in control feedback. No ignore flags:
        // Android's global touch-feedback preference and hardware policy still apply.
        bool played = surface.PerformHapticFeedback(effect);
        lastHapticAt = now;
        Android.Util.Log.Debug("LauncherHaptics", $"cue={cue}; mode={state.HapticFeedback}; accepted={played}");
        return played;
    }
    private void BindTapHaptics(View view, HapticCue cue = HapticCue.Tap)
    {
        view.HapticFeedbackEnabled = false;
        var binding = hapticBindings.GetValue(view, _ => new HapticBinding());
        binding.Cue = cue;
        if (binding.Tap)
            return;
        binding.Tap = true;
        view.Click += (_, _) => Haptic(view, binding.Cue);
    }
    private void BindHoldHaptics(View view)
    {
        view.HapticFeedbackEnabled = false;
        var binding = hapticBindings.GetValue(view, _ => new HapticBinding());
        if (binding.Hold)
            return;
        binding.Hold = true;
        view.LongClick += (_, _) => Haptic(view, HapticCue.Hold);
    }
    private void BindNativeHaptics(View view)
    {
        view.HapticFeedbackEnabled = state.HapticFeedback != "Off";
        nativeHapticViews.RemoveAll(reference => !reference.TryGetTarget(out _));
        nativeHapticViews.Add(new(view));
    }
    private void BindToggleHaptics(CompoundButton toggle)
    {
        toggle.HapticFeedbackEnabled = false;
        toggle.CheckedChange += (_, e) => Haptic(toggle, e.IsChecked ? HapticCue.ToggleOn : HapticCue.ToggleOff);
    }
    private void SyncHapticPreferences()
    {
        nativeHapticViews.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in nativeHapticViews)
        if (reference.TryGetTarget(out var view))
            view.HapticFeedbackEnabled = state.HapticFeedback != "Off";
    }
    private void ShowHapticSettings() => Sheet("Haptic feedback", (panel, dialog) =>
    {
        Paragraph("Choose how Launcher feels when you tap controls, switch pages, fold drawers or dismiss items. Your phone controls the vibration strength.", panel);
        foreach (var option in new[] { ("Off", "Disable Launcher interaction feedback"), ("Subtle", "Light ticks with a firmer touch-and-hold"), ("Crisp", "More distinct taps and gesture confirmations") })
        {
            var row = Stack(true);
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetMinimumHeight(Dp(68));
            row.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            bool selected = state.HapticFeedback == option.Item1;
            row.Background = Background(selected ? palette.Card : Android.Graphics.Color.Transparent, 12, selected ? palette.Action : palette.Divider);
            var labels = Stack();
            labels.AddView(Text(option.Item1, 15, true), Match());
            var detail = Text(option.Item2, 12, color: palette.Muted);
            detail.SetPadding(0, Dp(4), Dp(8), 0);
            labels.AddView(detail, Match());
            row.AddView(labels, new LinearLayout.LayoutParams(0, -2, 1));
            if (selected)
                row.AddView(new GlyphView(this, "check", palette.Action), new LinearLayout.LayoutParams(Dp(20), Dp(20)));
            row.ContentDescription = option.Item1 + (selected ? ", selected" : "");
            TouchFeedback(row, 12, HapticCue.None);
            row.Click += (_, _) => { state.HapticFeedback = option.Item1; Save(); SyncHapticPreferences(); Haptic(row, HapticCue.Selection); dialog.Dismiss(); RefreshBodyInPlace(); };
            var lp = Match();
            lp.SetMargins(0, 0, 0, Dp(8));
            panel.AddView(row, lp);
        }
        Paragraph("Try the current setting. Off disables Launcher’s own tap, touch-and-hold and selection feedback. Keyboard, widget-provider and Android system feedback are controlled separately.", panel);
        var previews = Stack(true);
        previews.BaselineAligned = false;
        foreach (var sample in new[] { ("Tap", HapticCue.Tap), ("Hold", HapticCue.Hold), ("Confirm", HapticCue.Commit) })
        {
            var control = Text(sample.Item1, 13, true);
            control.Gravity = GravityFlags.Center;
            control.SetMinimumHeight(Dp(48));
            control.Background = Background(palette.Card, 12, palette.Divider);
            TouchFeedback(control, 12, HapticCue.None);
            control.Click += (_, _) => Haptic(control, sample.Item2);
            var lp = new LinearLayout.LayoutParams(0, -1, 1);
            lp.SetMargins(0, 0, Dp(6), 0);
            previews.AddView(control, lp);
        }
        panel.AddView(previews, Match());
        Paragraph("If you feel nothing, check Android's touch-vibration setting. Some devices provide limited haptic effects. Launcher never overrides the system setting.", panel);
    });
}
