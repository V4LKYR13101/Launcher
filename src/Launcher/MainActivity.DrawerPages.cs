using Android.Animation;
using Android.Graphics;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using LauncherApp.UI;

namespace LauncherApp;

public partial class MainActivity
{
    private const string GroupedDrawerPage = "App drawer";
    private const string AllAppsDrawerPage = "All apps drawer";
    private string lastDrawerPage = GroupedDrawerPage;
    private View? launcherStatusRow;
    private DrawerSlide? drawerSlide;
    private ValueAnimator? drawerSlideAnimation;
    private string? drawerSwipeTarget;
    private float drawerSwipeX, drawerSwipeY;
    private long drawerSwipeStarted;
    private bool drawerSwipeActive;

    private static bool IsDrawerPage(string value) => value is GroupedDrawerPage or AllAppsDrawerPage;
    private bool IsAppsSurface => page == "Tiles" || IsDrawerPage(page);
    private bool IsDrawerSlide(string target) => page == "Tiles" && IsDrawerPage(target)
        || IsDrawerPage(page) && target == "Tiles";

    private sealed record DrawerSlide(string Origin, string Target, View Previous, View Home,
        View Drawer, View? Status, View Pulse, float Travel)
    {
        public bool Opening => IsDrawerPage(Target);
        public float Progress { get; set; }
        public bool ReturnToOrigin { get; set; }
    }

    private void AddDrawerHeader()
    {
        var header = Stack(true);
        header.SetGravity(GravityFlags.CenterVertical);
        header.SetPadding(Dp(12), Dp(8), Dp(12), 0);
        header.AddView(IconButton("down", "Close app drawer", () => Navigate("Tiles")), new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        var title = Text(page == AllAppsDrawerPage ? "All apps" : "Apps", 24, true);
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            title.AccessibilityHeading = true;
        title.SetMaxLines(2);
        header.AddView(title, new LinearLayout.LayoutParams(0, -2, 1));
        foreach (var (target, glyph, label) in new[] { (GroupedDrawerPage, "filter", "Grouped apps"), (AllAppsDrawerPage, "grid", "All apps grid") })
        {
            var button = IconButton(glyph, label, () => { if (page != target) Navigate(target); });
            button.Selected = page == target;
            if (button.Selected)
                button.Background = Background(palette.Card, 20);
            header.AddView(button, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        }
        shell.AddView(header, Match());
    }

    private bool BeginDrawerSlide(string target)
    {
        if (!LayerMotionEnabled || !state.PageTransitions || !resumed || searching || scene.Height <= 0)
            return false;

        string origin = page;
        View previous = scene;
        var previousStatus = launcherStatusRow;
        float travel = previous.Height;
        ResetDeviceDepth();
        RememberHomeScroll();
        page = target;
        Render();

        bool opening = IsDrawerPage(target);
        var pulse = new View(this)
        {
            Alpha = 0,
            Background = new DrawerPulseDrawable(Color.ParseColor(state.Accent), (byte)(palette.Light ? 70 : 135)),
            ImportantForAccessibility = ImportantForAccessibility.No
        };
        int incomingIndex = root.IndexOfChild(scene);
        root.AddView(pulse, incomingIndex, new FrameLayout.LayoutParams(-1, -1));
        previous.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
        root.AddView(previous, opening ? incomingIndex + 1 : root.ChildCount, new FrameLayout.LayoutParams(-1, -1));
        drawerSlide = new DrawerSlide(origin, target, previous, opening ? previous : scene, opening ? scene : previous,
            opening ? previousStatus : launcherStatusRow, pulse, travel);
        PaintDrawerSlide(drawerSlide, opening ? 0 : 1);
        return true;
    }

    private void PaintDrawerSlide(DrawerSlide slide, float progress)
    {
        slide.Progress = progress;
        slide.Home.TranslationY = -Dp(24) * progress;
        slide.Home.Alpha = Math.Max(0, 1 - 1.45f * progress);
        slide.Drawer.TranslationY = slide.Travel * (1 - progress);
        slide.Drawer.Alpha = Math.Min(1, .55f + progress);
        if (slide.Status is { } status)
        {
            float clockProgress = 1 - MathF.Pow(1 - Math.Min(1, progress * 1.7f), 1.4f);
            status.TranslationY = -slide.Travel * .51f * clockProgress;
            status.ScaleX = status.ScaleY = 1 - .32f * clockProgress;
            status.Alpha = 1 - .35f * clockProgress;
        }
        if (slide.Opening && slide.Pulse.Background is DrawerPulseDrawable pulse)
        {
            pulse.Progress = progress;
            slide.Pulse.Alpha = Math.Max(0, MathF.Sin(MathF.PI * progress));
        }
    }

    private void FinishDrawerSlide(bool commit)
    {
        if (drawerSlide is not { } slide)
            return;
        slide.ReturnToOrigin = !commit;
        float target = slide.Opening == commit ? 1 : 0;
        void Complete()
        {
            StopDrawerSlide();
            if (!commit)
            {
                page = slide.Origin;
                Render();
            }
        }

        if (!LayerMotionEnabled || Math.Abs(target - slide.Progress) < .001f)
        {
            Complete();
            return;
        }
        var animation = ValueAnimator.OfFloat(slide.Progress, target)!;
        drawerSlideAnimation = animation;
        animation.SetDuration((long)(140 + 150 * Math.Abs(target - slide.Progress)));
        animation.SetInterpolator(new DecelerateInterpolator(1.45f));
        animation.Update += (_, e) =>
        {
            if (ReferenceEquals(drawerSlideAnimation, animation))
                PaintDrawerSlide(slide, ((Java.Lang.Float)e.Animation!.AnimatedValue!).FloatValue());
        };
        animation.AnimationEnd += (_, _) =>
        {
            if (ReferenceEquals(drawerSlideAnimation, animation))
                Complete();
        };
        animation.Start();
    }

    private void StopDrawerSlide()
    {
        var animation = drawerSlideAnimation;
        drawerSlideAnimation = null;
        animation?.Cancel();
        if (drawerSlide is not { } slide)
            return;
        drawerSlide = null;
        scene.TranslationY = 0;
        scene.Alpha = 1;
        if (launcherStatusRow is { } status)
        {
            status.TranslationY = 0;
            status.ScaleX = status.ScaleY = 1;
            status.Alpha = 1;
        }
        root.RemoveView(slide.Previous);
        root.RemoveView(slide.Pulse);
    }

    private void EndDrawerSlideForInterruption()
    {
        var restore = drawerSlide is { } slide && (slide.ReturnToOrigin || drawerSwipeActive) ? slide.Origin : null;
        drawerSwipeActive = false;
        drawerSwipeTarget = null;
        StopDrawerSlide();
        if (restore is not null)
        {
            page = restore;
            Render();
        }
    }

    private bool HandleDrawerPageTouch(MotionEvent ev)
    {
        if (ev.ActionMasked == MotionEventActions.Down)
        {
            EndDrawerSlideForInterruption();
            if (searching || openSheetCount > 0)
                return false;
            if (page == "Tiles" && !scroll.CanScrollVertically(1))
                drawerSwipeTarget = lastDrawerPage;
            else if (IsDrawerPage(page) && !scroll.CanScrollVertically(-1))
                drawerSwipeTarget = "Tiles";
            drawerSwipeX = ev.RawX;
            drawerSwipeY = ev.RawY;
            drawerSwipeStarted = ev.EventTime;
            return false;
        }

        if (drawerSwipeTarget is not { } target)
            return false;
        float dx = ev.RawX - drawerSwipeX;
        float distance = (ev.RawY - drawerSwipeY) * (IsDrawerPage(target) ? -1 : 1);
        if (ev.ActionMasked == MotionEventActions.Move)
        {
            if (!drawerSwipeActive)
            {
                float slop = Math.Max(Dp(10), ViewConfiguration.Get(this)!.ScaledTouchSlop);
                if (Math.Abs(dx) <= slop && Math.Abs(distance) <= slop)
                    return false;
                if (distance <= slop || distance < Math.Abs(dx) * 1.3f)
                {
                    drawerSwipeTarget = null;
                    return false;
                }
                using var cancel = MotionEvent.Obtain(ev)!;
                cancel.Action = MotionEventActions.Cancel;
                base.DispatchTouchEvent(cancel);
                cancel.Recycle();
                drawerSwipeActive = true;
                BeginDrawerSlide(target);
            }
            if (drawerSlide is { } slide)
            {
                float fraction = Math.Clamp(distance / slide.Travel, 0, 1);
                PaintDrawerSlide(slide, slide.Opening ? fraction : 1 - fraction);
            }
            return true;
        }
        if (ev.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel or MotionEventActions.PointerDown)
        {
            drawerSwipeTarget = null;
            if (!drawerSwipeActive)
                return false;
            drawerSwipeActive = false;
            float speed = distance * 1000 / Math.Max(1, ev.EventTime - drawerSwipeStarted);
            bool commit = ev.ActionMasked == MotionEventActions.Up
                && (distance >= Dp(72) || distance >= Dp(24) && speed >= Dp(520));
            if (commit)
            {
                Haptic(root, HapticCue.Commit);
                if (IsDrawerPage(target))
                    lastDrawerPage = target;
            }
            if (drawerSlide is not null)
                FinishDrawerSlide(commit);
            else if (commit)
                Navigate(target);
            return true;
        }
        return drawerSwipeActive;
    }
}
