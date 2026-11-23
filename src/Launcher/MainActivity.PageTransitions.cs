using Android.Animation;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;

namespace LauncherApp;

public partial class MainActivity
{
    private const string GoogleDiscover = "Google Discover";
    private const string GoogleAppPackage = "com.google.android.googlequicksearchbox";
    private readonly Dictionary<string, int> homeScrollPositions = [];
    private ScrollView? renderedHomeScroll;
    private string? renderedHomePage;
    private PageSlide? pageSlide;
    private ValueAnimator? pageSlideAnimation;
    private SwipeRoute? activeSwipe;
    private bool swipePending, pageSwipeActive, swipeInQuickPanelScroll, swipeInFilter;
    private float swipeX, swipeY;
    private long swipeStarted;

    private readonly record struct SwipeRoute(string Target, bool Horizontal, int IncomingFrom)
    {
        public float Distance(float dx, float dy) => -(Horizontal ? dx : dy) * IncomingFrom;
    }

    private sealed record PageSlide(string Origin, SwipeRoute Route, View Previous, View? HomeStatus, float Travel)
    {
        public bool OpeningPanel => Origin == "Tiles";
        public bool UsesHomeBackdrop => Origin == "Tiles" || Route.Target == "Tiles";
        public float Progress { get; set; }
        public bool ReturnToOrigin { get; set; }
    }

    private void RememberHomeScroll()
    {
        if (renderedHomePage is { } name && renderedHomeScroll?.IsAttachedToWindow == true)
            homeScrollPositions[name] = renderedHomeScroll.ScrollY;
    }

    private void RestoreHomeScroll()
    {
        if ((!IsHomePage && page != "Quick panel" && !IsSettingsPage(page)) || searching)
        {
            renderedHomeScroll = null;
            renderedHomePage = null;
            return;
        }
        renderedHomeScroll = scroll;
        renderedHomePage = todayLayoutEditor is not null ? TodayEditScrollKey : FilteringSettings ? SettingsSearchScrollKey : page;
        var target = scroll;
        string name = page;
        string key = renderedHomePage;
        int position = homeScrollPositions.GetValueOrDefault(key);
        if (position > 0 || IsSettingsPage(name))
            target.Post(() => { if (ReferenceEquals(scroll, target) && page == name && renderedHomePage == key && !searching) target.ScrollTo(0, position); });
    }

    private static int HomePageIndex(string value) => value switch { "Root" => 0, "Tiles" => 1, "Conversations" => 2, _ => -1 };

    private SwipeRoute? RouteTo(string target)
    {
        if (page == "Tiles" && (IsDrawerPage(target) || target == "Quick panel"))
            return new(target, false, target == "Quick panel" ? -1 : 1);
        if ((IsDrawerPage(page) || page == "Quick panel") && target == "Tiles")
            return new(target, false, page == "Quick panel" ? 1 : -1);
        if (IsDrawerPage(page) && IsDrawerPage(target) && page != target)
            return new(target, true, target == AllAppsDrawerPage ? 1 : -1);
        int from = HomePageIndex(page), to = HomePageIndex(target);
        return from >= 0 && to >= 0 && from != to ? new(target, true, Math.Sign(to - from)) : null;
    }

    private void Navigate(string target, bool returningFromSettings = false)
    {
        EndPageSlideForInterruption();
        RememberHomeScroll();
        if (target != "Root" && todayLayoutEditor is { } editor)
        {
            editor.StopDrag();
            todayLayoutEditor = null;
        }
        if (!returningFromSettings)
            TrackSettingsNavigation(target);
        if (IsDrawerPage(target))
            lastDrawerPage = target;
        if (RouteTo(target) is { } route && BeginPageSlide(route))
        {
            FinishPageSlide(true);
            return;
        }
        HideKeyboard();
        searching = false;
        query = "";
        page = target;
        Render();
    }

    private bool BeginPageSlide(SwipeRoute route)
    {
        float travel = route.Horizontal ? scene.Width
            : route.Target == "Quick panel" && OperatingSystem.IsAndroidVersionAtLeast(30)
                ? root.Height - root.PaddingTop : scene.Height;
        if (!LayerMotionEnabled || !state.PageTransitions || !resumed || searching || travel <= 0)
            return false;

        string origin = page;
        var previous = scene;
        var previousStatus = launcherStatusRow;
        page = route.Target;
        bool homeBackdrop = origin == "Tiles" || page == "Tiles";
        Render(pageTransition: homeBackdrop);
        previous.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
        bool closingPanel = !route.Horizontal && origin != "Tiles";
        int index = closingPanel ? root.ChildCount : root.IndexOfChild(scene);
        root.AddView(previous, index, new FrameLayout.LayoutParams(-1, -1));
        pageSlide = new PageSlide(origin, route, previous, origin == "Tiles" ? previousStatus : launcherStatusRow, travel);
        PaintPageSlide(pageSlide, 0);
        return true;
    }

    private void PaintPageSlide(PageSlide slide, float progress)
    {
        slide.Progress = progress;
        float direction = slide.Route.IncomingFrom;
        if (slide.UsesHomeBackdrop)
        {
            float cover = slide.Origin == "Tiles" ? progress : 1 - progress;
            ApplyBackdrop(cover);
            homeAccentGlow?.SetAlpha(state.HomeAccentGlow ? 255 : (int)(255 * cover));
        }
        if (slide.Route.Horizontal)
        {
            scene.TranslationX = direction * slide.Travel * (1 - progress);
            scene.Alpha = Math.Min(1, .55f + progress);
            slide.Previous.TranslationX = -direction * slide.Travel * .32f * progress;
            slide.Previous.Alpha = 1 - .45f * progress;
            return;
        }

        float reveal = slide.OpeningPanel ? progress : 1 - progress;
        float edge = slide.OpeningPanel ? direction : -direction;
        var home = slide.OpeningPanel ? slide.Previous : scene;
        var panel = slide.OpeningPanel ? scene : slide.Previous;
        home.TranslationY = -edge * Dp(24) * reveal;
        home.Alpha = Math.Max(0, 1 - 1.45f * reveal);
        panel.TranslationY = edge * slide.Travel * (1 - reveal);
        panel.Alpha = Math.Min(1, .55f + reveal);
        if (slide.HomeStatus is { } status)
        {
            float clockProgress = 1 - MathF.Pow(1 - Math.Min(1, reveal * 1.7f), 1.4f);
            status.TranslationY = -edge * slide.Travel * .51f * clockProgress;
            status.ScaleX = status.ScaleY = 1 - .32f * clockProgress;
            status.Alpha = 1 - .35f * clockProgress;
        }
    }

    private void FinishPageSlide(bool commit)
    {
        if (pageSlide is not { } slide)
            return;
        slide.ReturnToOrigin = !commit;
        float target = commit ? 1 : 0;
        void Complete()
        {
            if (commit)
                StopPageSlide();
            else
                Render();
        }

        float remaining = Math.Abs(target - slide.Progress);
        if (!LayerMotionEnabled || remaining < .001f)
        {
            Complete();
            return;
        }
        var animation = ValueAnimator.OfFloat(slide.Progress, target)!;
        pageSlideAnimation = animation;
        animation.SetDuration((long)(90 + 140 * remaining));
        animation.SetInterpolator(new DecelerateInterpolator(1.45f));
        animation.Update += (_, e) =>
        {
            if (ReferenceEquals(pageSlideAnimation, animation))
                PaintPageSlide(slide, ((Java.Lang.Float)e.Animation!.AnimatedValue!).FloatValue());
        };
        animation.AnimationEnd += (_, _) =>
        {
            if (ReferenceEquals(pageSlideAnimation, animation))
                Complete();
        };
        animation.Start();
    }

    private void StopPageSlide()
    {
        var animation = pageSlideAnimation;
        pageSlideAnimation = null;
        animation?.Cancel();
        if (pageSlide is not { } slide)
            return;
        pageSlide = null;
        ApplyBackdrop(IsWallpaperHome ? 0 : 1);
        ApplyWallpaperVisibility();
        homeAccentGlow?.SetAlpha(IsWallpaperHome && !state.HomeAccentGlow ? 0 : 255);
        scene.TranslationX = scene.TranslationY = 0;
        scene.Alpha = 1;
        if (launcherStatusRow is { } status)
        {
            status.TranslationY = 0;
            status.ScaleX = status.ScaleY = 1;
            status.Alpha = 1;
        }
        root.RemoveView(slide.Previous);
    }

    private void EndPageSlideForInterruption()
    {
        swipePending = false;
        activeSwipe = null;
        pageSwipeActive = false;
        if (pageSlide is { } slide && (slide.ReturnToOrigin || pageSlideAnimation is null))
        {
            page = slide.Origin;
            StopPageSlide();
            Render();
        }
        else
            StopPageSlide();
    }

    private SwipeRoute? SwipeTarget(float dx, float dy)
    {
        if (Math.Abs(dx) > Math.Abs(dy) * 1.3f)
        {
            int direction = dx < 0 ? 1 : -1;
            if (page == "Tiles" && direction < 0 && state.SwipeRightOpensDiscover)
                return new(GoogleDiscover, true, -1);
            if (swipeInFilter && conversationFilterScroll?.CanScrollHorizontally(direction) == true)
                return null;
            string[] pages = IsDrawerPage(page) ? [GroupedDrawerPage, AllAppsDrawerPage] : ["Root", "Tiles", "Conversations"];
            int current = Array.IndexOf(pages, page);
            int next = current + direction;
            return current >= 0 && next >= 0 && next < pages.Length ? RouteTo(pages[next]) : null;
        }
        if (Math.Abs(dy) <= Math.Abs(dx) * 1.3f)
            return null;
        return page switch
        {
            "Tiles" when dy < 0 && !scroll.CanScrollVertically(1) => RouteTo(lastDrawerPage),
            "Tiles" when dy > 0 && !scroll.CanScrollVertically(-1) && !drawerTouchActive => RouteTo("Quick panel"),
            "Quick panel" when dy < 0 && !swipeInQuickPanelScroll => RouteTo("Tiles"),
            _ when IsDrawerPage(page) && dy > 0 && !scroll.CanScrollVertically(-1) => RouteTo("Tiles"),
            _ => null
        };
    }

    private static bool TouchWithin(View? view, MotionEvent ev)
    {
        using var bounds = new Rect();
        return view is not null && view.GetGlobalVisibleRect(bounds) && bounds.Contains((int)ev.RawX, (int)ev.RawY);
    }

    private bool HandlePageSwipe(MotionEvent ev)
    {
        if (ev.ActionMasked == MotionEventActions.Down)
        {
            EndPageSlideForInterruption();
            swipePending = todayLayoutEditor is null && !searching && openSheetCount == 0 && (IsHomePage || page == "Quick panel");
            if (swipePending)
            {
                swipeX = ev.RawX;
                swipeY = ev.RawY;
                swipeStarted = ev.EventTime;
                swipeInQuickPanelScroll = page == "Quick panel" && TouchWithin(scroll, ev)
                    && (scroll.CanScrollVertically(1) || scroll.CanScrollVertically(-1));
                swipeInFilter = page == "Conversations" && TouchWithin(conversationFilterScroll, ev);
            }
            return false;
        }
        if (!swipePending)
            return false;

        float dx = ev.RawX - swipeX, dy = ev.RawY - swipeY;
        if (ev.ActionMasked == MotionEventActions.Move)
        {
            if (!pageSwipeActive)
            {
                float slop = Math.Max(Dp(10), ViewConfiguration.Get(this)!.ScaledTouchSlop);
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) <= slop)
                    return false;
                body.CancelLongPress();
                activeSwipe = SwipeTarget(dx, dy);
                if (activeSwipe is not { } route)
                {
                    swipePending = false;
                    return false;
                }
                using var cancel = MotionEvent.Obtain(ev)!;
                cancel.Action = MotionEventActions.Cancel;
                base.DispatchTouchEvent(cancel);
                cancel.Recycle();
                pageSwipeActive = true;
                if (route.Target != GoogleDiscover)
                    BeginPageSlide(route);
            }
            if (pageSlide is { } slide)
                PaintPageSlide(slide, Math.Clamp(slide.Route.Distance(dx, dy) / slide.Travel, 0, 1));
            return true;
        }
        if (ev.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel or MotionEventActions.PointerDown)
        {
            swipePending = false;
            if (!pageSwipeActive || activeSwipe is not { } route)
                return false;
            pageSwipeActive = false;
            activeSwipe = null;
            float distance = route.Distance(dx, dy);
            float speed = distance * 1000 / Math.Max(1, ev.EventTime - swipeStarted);
            bool commit = ev.ActionMasked == MotionEventActions.Up
                && (distance >= Dp(58) || distance >= Dp(22) && speed >= Dp(480));
            if (commit)
            {
                Haptic(root, HapticCue.Commit);
                if (IsDrawerPage(route.Target))
                    lastDrawerPage = route.Target;
            }
            if (pageSlide is not null)
                FinishPageSlide(commit);
            else if (commit && route.Target == GoogleDiscover)
                OpenGoogleDiscover();
            else if (commit)
                Navigate(route.Target);
            return true;
        }
        return pageSwipeActive;
    }

    private void OpenGoogleDiscover() => WithAppUnlock(GoogleAppPackage, () =>
    {
        try
        {
            using var launch = PackageManager?.GetLaunchIntentForPackage(GoogleAppPackage);
            if (launch is not null)
            {
                StartActivity(launch);
                return;
            }
        }
        catch (Exception e) when (e is ActivityNotFoundException or Java.Lang.SecurityException) { }
        Toast("The Google app is unavailable. Opening Today.");
        Navigate("Root");
    });
}
