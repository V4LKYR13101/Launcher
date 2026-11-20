using Android.Animation;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private DepthMotionMonitor depthMotion = null!;
    private bool depthTouchActive;
    private SurfaceOrientation? depthRotation;
    private readonly List<View> depthHeadings = [];
    private readonly Dictionary<string, ValueAnimator> rootFoldAnimations = [];
    private readonly Dictionary<string, Action> rootFoldFinishes = [];
    private bool LayerMotionEnabled => state.LayeredInterface && DepthAvailable && Android.Provider.Settings.Global.GetFloat(ContentResolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) > 0;
    private void ResetDeviceDepth()
    {
        if (body is not null)
        {
            body.TranslationX = body.TranslationY = body.RotationX = body.RotationY = 0;
        }
        foreach (var view in depthHeadings)
            view.TranslationX = view.TranslationY = 0;
    }
    private void SyncDepthMotion()
    {
        if (depthMotion is null)
            return;
        if (resumed && IsHomePage && !searching && openSheetCount == 0 && state.TiltDepth && LayerMotionEnabled)
        {
            if (!depthMotion.Running)
                depthRotation = null;
            depthMotion.Start();
        }
        else
        {
            depthMotion.Stop();
            ResetDeviceDepth();
        }
    }
    private void ApplyDeviceDepth(float x, float y)
    {
        if (depthTouchActive || homePageAnimation is not null || drawerSlide is not null || drawerAnimations.Count > 0 || rootFoldAnimations.Count > 0)
            return;
        if (!resumed || !IsHomePage || searching || openSheetCount > 0 || !LayerMotionEnabled || !state.TiltDepth)
        {
            ResetDeviceDepth();
            return;
        }
        var rotation = Android.Views.SurfaceOrientation.Rotation0;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            rotation = Display?.Rotation ?? rotation;
        else
        {
#pragma warning disable CA1422
            rotation = WindowManager?.DefaultDisplay?.Rotation ?? rotation;
#pragma warning restore CA1422
        }
        if (depthRotation != rotation)
        {
            depthRotation = rotation;
            depthMotion.Recenter();
            ResetDeviceDepth();
            return;
        }
        (x, y) = rotation switch
        {
            SurfaceOrientation.Rotation90 => (y, -x),
            SurfaceOrientation.Rotation180 => (-x, -y),
            SurfaceOrientation.Rotation270 => (-y, x),
            _ => (x, y)
        };
        body.PivotX = body.Width / 2f;
        body.PivotY = scroll.ScrollY + scroll.Height / 2f;
        body.SetCameraDistance(Dp(2400));
        body.TranslationX = Dp(5) * x;
        body.TranslationY = Dp(3) * y;
        body.RotationX = -1.2f * y;
        body.RotationY = 1.2f * x;
        foreach (var view in depthHeadings)
        {
            view.TranslationX = -Dp(2) * x;
            view.TranslationY = -Dp(1.5f) * y;
        }
    }
    private void PaintDrawerCells(LinearLayout content, float fraction)
    {
        for (int r = 0; r < content.ChildCount; r++)
        if (content.GetChildAt(r) is ViewGroup row)
            for (int c = 0; c < row.ChildCount; c++)
            if (row.GetChildAt(c) is { } cell)
            {
                float delay = Math.Min(.45f, r * .07f + c * .025f), f = Math.Clamp((fraction - delay) / (1 - delay), 0, 1);
                cell.Alpha = .15f + .85f * f;
                cell.TranslationY = -Dp(10) * (1 - f);
                cell.ScaleX = cell.ScaleY = .96f + .04f * f;
            }
    }
    private void StopRootFolds()
    {
        var active = rootFoldAnimations.Values.ToArray();
        var finish = rootFoldFinishes.Values.ToArray();
        rootFoldAnimations.Clear();
        rootFoldFinishes.Clear();
        foreach (var animation in active)
            animation.Cancel();
        foreach (var complete in finish)
            complete();
    }
    private void SwapRootPane(string id, FrameLayout viewport, LinearLayout next)
    {
        ResetDeviceDepth();
        rootFoldFinishes.Remove(id);
        if (rootFoldAnimations.Remove(id, out var active))
            active.Cancel();
        int from = viewport.Height;
        var old = viewport.GetChildAt(viewport.ChildCount - 1);
        viewport.RemoveAllViews();
        if (old is not null)
        {
            old.Alpha = 1;
            old.RotationX = 0;
            old.TranslationY = 0;
            viewport.AddView(old, new FrameLayout.LayoutParams(-1, -2));
        }
        viewport.AddView(next, new FrameLayout.LayoutParams(-1, -2));
        next.PivotY = 0;
        next.SetCameraDistance(Dp(1600));
        int width = viewport.Width > 0 ? viewport.Width : Resources!.DisplayMetrics!.WidthPixels - Dp(32);
        next.Measure(View.MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly), View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
        int target = next.MeasuredHeight;
        var layout = viewport.LayoutParameters!;
        layout.Height = from;
        viewport.LayoutParameters = layout;
        next.Alpha = 0;
        next.TranslationY = -Dp(10);
        next.RotationX = -7;
        var animation = ValueAnimator.OfFloat(0, 1)!;
        animation.SetDuration(240);
        animation.SetInterpolator(new DecelerateInterpolator(1.4f));
        rootFoldAnimations[id] = animation;
        animation.Update += (_, e) =>
        {
            if (!rootFoldAnimations.TryGetValue(id, out var current) || !ReferenceEquals(current, animation))
                return;
            float f = ((Java.Lang.Float)e.Animation!.AnimatedValue!).FloatValue();
            var size = viewport.LayoutParameters!;
            size.Height = (int)(from + (target - from) * f);
            viewport.LayoutParameters = size;
            if (old is not null)
            {
                old.Alpha = Math.Max(0, 1 - 4 * f);
                old.RotationX = 3 * f;
            }
            next.Alpha = Math.Clamp((f - .12f) / .88f, 0, 1);
            next.TranslationY = -Dp(10) * (1 - f);
            next.RotationX = -7 * (1 - f);
        };
        rootFoldFinishes[id] = () =>
        {
            if (old is not null)
                viewport.RemoveView(old);
            next.Alpha = 1;
            next.TranslationY = next.RotationX = 0;
            var size = viewport.LayoutParameters!;
            size.Height = -2;
            viewport.LayoutParameters = size;
            workspaceTimerViews.RemoveAll(p => !p.Text.IsAttachedToWindow);
        };
        animation.AnimationEnd += (_, _) =>
        {
            if (!rootFoldAnimations.TryGetValue(id, out var current) || !ReferenceEquals(current, animation))
                return;
            rootFoldAnimations.Remove(id);
            if (rootFoldFinishes.Remove(id, out var finish))
                finish();
        };
        animation.Start();
    }
}
