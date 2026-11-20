using Android.Animation;
using Android.Graphics;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using Launcher.Core;
using LauncherApp.UI;

namespace LauncherApp;

public partial class MainActivity
{
    private static readonly (string Name, string Hex)[] AccentPresets =
    [
        ("Warm white", "#F4F4E8"),
        ("Mint", "#9FE7C1"),
        ("Acid yellow", "#E4FF00"),
        ("Sky", "#9BCBFF"),
        ("Lilac", "#CEB8FF"),
        ("Peach", "#FFBB98"),
        ("Forge Orange", "#FF553B")
    ];

    private AccentGlowDrawable? homeAccentGlow;

    private void ShowAccentChoice()
    {
        var choices = AccentPresets.ToList();
        int selected = choices.FindIndex(choice => string.Equals(choice.Hex, state.Accent, StringComparison.OrdinalIgnoreCase));
        if (selected < 0)
        {
            selected = choices.Count;
            choices.Add(("Custom · " + state.Accent.ToUpperInvariant(), state.Accent));
        }

        var glow = new AccentGlowDrawable(Color.ParseColor(state.Accent), (byte)(palette.Light ? 46 : 86));
        Sheet("Accent", (panel, dialog) =>
        {
            int slotWidth = Dp(68);
            var preview = Stack();
            var viewport = new HorizontalScrollView(this)
            {
                HorizontalScrollBarEnabled = false,
                OverScrollMode = OverScrollMode.Never
            };
            var strip = Stack(true);
            viewport.AddView(strip, new FrameLayout.LayoutParams(-2, -1));
            preview.AddView(viewport, Match(Dp(112)));

            var name = Text(choices[selected].Name, 16, true);
            name.Gravity = GravityFlags.Center;
            name.SetPadding(Dp(8), Dp(4), Dp(8), Dp(12));
            name.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
            preview.AddView(name, Match());
            panel.AddView(preview, Match());

            var swatches = new List<(View Target, View Pill, View Check)>();
            float position = selected;
            bool recenter = true;
            viewport.LayoutChange += (_, _) =>
            {
                int inset = Math.Max(0, (viewport.Width - slotWidth) / 2);
                if (strip.PaddingLeft == inset)
                    return;
                recenter = true;
                strip.SetPadding(inset, 0, inset, 0);
            };
            strip.LayoutChange += (_, _) =>
            {
                if (!recenter)
                    return;
                recenter = false;
                viewport.ScrollTo(selected * slotWidth, 0);
            };

            ValueAnimator? motion = null;
            void PaintSwatches(float center)
            {
                position = center;
                for (int i = 0; i < swatches.Count; i++)
                {
                    var (_, pill, check) = swatches[i];
                    float distance = Math.Min(2, Math.Abs(i - center));
                    pill.ScaleX = 1 - .22f * distance;
                    pill.ScaleY = 1 - .21f * distance;
                    check.Alpha = Math.Max(0, 1 - distance);
                }
            }

            void Select(int index)
            {
                motion?.Cancel();
                motion = null;
                selected = index;
                for (int i = 0; i < swatches.Count; i++)
                    swatches[i].Target.Selected = i == index;
                var choice = choices[index];
                name.Text = choice.Name;
                Color target = Color.ParseColor(choice.Hex);
                float start = position;
                int scrollFrom = viewport.ScrollX, scrollTo = index * slotWidth;
                Color from = glow.Accent;
                var previousHomeColor = homeAccentGlow?.Accent ?? from;
                ApplyAccent(choice.Hex);
                void Paint(float progress)
                {
                    PaintSwatches(start + (index - start) * progress);
                    viewport.ScrollTo((int)Math.Round(scrollFrom + (scrollTo - scrollFrom) * progress), 0);
                    glow.Accent = AccentGlowDrawable.Blend(from, target, progress);
                    if (homeAccentGlow is { } homeGlow)
                        homeGlow.Accent = AccentGlowDrawable.Blend(previousHomeColor, target, progress);
                }

                if (!LayerMotionEnabled)
                {
                    Paint(1);
                    return;
                }
                Paint(0);
                motion = ValueAnimator.OfFloat(0, 1)!;
                motion.SetDuration(250);
                motion.SetInterpolator(new DecelerateInterpolator(1.5f));
                motion.Update += (_, e) => Paint(((Java.Lang.Float)e.Animation!.AnimatedValue!).FloatValue());
                motion.Start();
            }

            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                var (label, hex) = choices[index];
                var target = new FrameLayout(this) { ContentDescription = label, Focusable = true, Selected = index == selected };
                target.SetAccessibilityDelegate(new ButtonSemantics());
                var pill = new FrameLayout(this)
                {
                    Background = Background(Color.ParseColor(hex), 32),
                    ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants
                };
                var ink = ColorContrast.UseDarkText(hex) ? Color.Black : Color.White;
                var check = new GlyphView(this, "check", ink);
                pill.AddView(check, new FrameLayout.LayoutParams(Dp(23), Dp(23), GravityFlags.Center));
                target.AddView(pill, new FrameLayout.LayoutParams(Dp(56), Dp(92), GravityFlags.Center));
                BindTapHaptics(target, HapticCue.Selection);
                target.Click += (_, _) => Select(index);
                strip.AddView(target, new LinearLayout.LayoutParams(slotWidth, -1));
                swatches.Add((target, pill, check));
            }
            PaintSwatches(selected);

            Gap(panel, 8);
            panel.AddView(Button("Custom hex color", () =>
            {
                dialog.Dismiss();
                Prompt("Accent · #RRGGBB", state.Accent, value => ApplyAccent(value.Trim()));
            }), Match());

            dialog.DismissEvent += (_, _) =>
            {
                motion?.Cancel();
                if (homeAccentGlow is { } homeGlow)
                    homeGlow.Accent = Color.ParseColor(state.Accent);
            };
        }, backgroundGlow: glow);
    }

    private void ApplyAccent(string hex)
    {
        if (string.Equals(hex, state.Accent, StringComparison.OrdinalIgnoreCase))
            return;
        string currentPage = page;
        int scrollY = scroll.ScrollY;
        state.Accent = hex;
        Save();
        Render();
        if (!IsHomePage && scrollY > 0)
        {
            var target = scroll;
            target.Post(() =>
            {
                if (ReferenceEquals(scroll, target) && page == currentPage)
                    target.ScrollTo(0, scrollY);
            });
        }
    }
}
