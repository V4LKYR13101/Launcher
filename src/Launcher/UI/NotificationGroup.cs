using Android.Animation;
using Android.Graphics;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using LauncherApp.Services;

namespace LauncherApp.UI;

internal static class NotificationGroup
{
    public static StackDepth? AddRows(LinearLayout section, LinearLayout header, View? chevron, string appName,
        IReadOnlyList<Conversation> items, HashSet<string> expandedApps, Func<Conversation, View> row,
        Color edgeColor, bool glass, Func<bool> animate, Func<bool>? canToggle = null,
        (View Row, Action<bool> SetCollapsed)? front = null)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            header.AccessibilityHeading = true;
        var newest = items[0];
        section.AddView(front?.Row ?? row(newest), new LinearLayout.LayoutParams(-1, -2));
        if (chevron is null)
            return null;

        var context = section.Context!;
        float density = context.Resources!.DisplayMetrics!.Density;
        var viewport = new FrameLayout(context);
        var older = new LinearLayout(context) { Orientation = Orientation.Vertical };
        var olderLayout = new FrameLayout.LayoutParams(-1, -2);
        var depth = new StackDepth(Math.Min(3, items.Count - 1), density);
        View edges = glass ? new View(context) : new StackEdges(context, depth, edgeColor);
        edges.ImportantForAccessibility = ImportantForAccessibility.No;
        int stackHeight = depth.Layers * depth.Step;
        var viewportLayout = new LinearLayout.LayoutParams(-1, -2);
        viewport.AddView(older, olderLayout);
        viewport.AddView(edges, new FrameLayout.LayoutParams(-1, stackHeight));
        section.AddView(viewport, viewportLayout);
        older.SetCameraDistance(1200 * density);

        bool expanded = expandedApps.Contains(newest.Package);
        float progress = expanded ? 1 : 0;
        ValueAnimator? slide = null;

        void PopulateOlder()
        {
            if (older.ChildCount > 0)
                return;
            for (int i = 1; i < items.Count; i++)
                older.AddView(row(items[i]), new LinearLayout.LayoutParams(-1, -2));
        }

        void UpdateHeader()
        {
            header.ContentDescription = $"{appName}, {items.Count} notifications, {(expanded ? "expanded" : "collapsed")}";
            front?.SetCollapsed(!expanded);
            older.ImportantForAccessibility = expanded
                ? ImportantForAccessibility.Auto : ImportantForAccessibility.NoHideDescendants;
        }

        void Settle()
        {
            progress = expanded ? 1 : 0;
            depth.Reveal = 1 - progress;
            older.Visibility = expanded ? ViewStates.Visible : ViewStates.Gone;
            edges.Visibility = expanded ? ViewStates.Gone : ViewStates.Visible;
            olderLayout.Height = -2;
            older.LayoutParameters = olderLayout;
            viewportLayout.Height = -2;
            viewport.LayoutParameters = viewportLayout;
            older.Alpha = edges.Alpha = 1;
            older.TranslationY = 0;
            older.ScaleX = 1;
            older.RotationX = 0;
            chevron.Rotation = expanded ? 180 : 0;
        }

        if (expanded)
            PopulateOlder();
        UpdateHeader();
        Settle();
        header.Click += (_, _) =>
        {
            if (canToggle?.Invoke() == false)
                return;
            expanded = !expanded;
            if (expanded)
            {
                expandedApps.Add(newest.Package);
                PopulateOlder();
            }
            else
                expandedApps.Remove(newest.Package);
            UpdateHeader();

            var previous = slide;
            slide = null;
            previous?.Cancel();
            if (viewport.Width <= 0 || !animate()
                || OperatingSystem.IsAndroidVersionAtLeast(26) && !ValueAnimator.AreAnimatorsEnabled())
            {
                Settle();
                return;
            }

            older.Visibility = edges.Visibility = ViewStates.Visible;
            older.Measure(View.MeasureSpec.MakeMeasureSpec(viewport.Width, MeasureSpecMode.Exactly),
                View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
            int openHeight = older.MeasuredHeight;
            olderLayout.Height = openHeight;
            older.LayoutParameters = olderLayout;
            older.PivotY = 0;
            float target = expanded ? 1 : 0;
            var animation = ValueAnimator.OfFloat(progress, target)!;
            slide = animation;
            animation.SetDuration((long)(120 + 120 * Math.Abs(target - progress)));
            animation.SetInterpolator(new DecelerateInterpolator(1.45f));
            animation.Update += (_, e) =>
            {
                progress = ((Java.Lang.Float)e.Animation!.AnimatedValue!).FloatValue();
                depth.Reveal = 1 - progress;
                viewportLayout.Height = (int)(stackHeight + (openHeight - stackHeight) * progress);
                viewport.LayoutParameters = viewportLayout;
                older.Alpha = progress;
                older.TranslationY = -18 * density * (1 - progress);
                older.ScaleX = .93f + .07f * progress;
                older.RotationX = -14 * (1 - progress);
                edges.Alpha = 1 - progress;
                chevron.Rotation = 180 * progress;
            };
            animation.AnimationEnd += (_, _) =>
            {
                if (!ReferenceEquals(slide, animation))
                    return;
                slide = null;
                Settle();
            };
            animation.Start();
        };
        return depth;
    }

    internal sealed class StackDepth(int layers, float density)
    {
        public int Layers { get; } = layers;
        public int Step { get; } = (int)MathF.Round(9 * density);
        public float Reveal { get; set; }
    }

    private sealed class StackEdges(Android.Content.Context context, StackDepth depth, Color color) : View(context)
    {
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            float dp = Resources!.DisplayMetrics!.Density;
            using var paint = new Paint(PaintFlags.AntiAlias);
            for (int layer = depth.Layers; layer >= 1; layer--)
            {
                float inset = (2 + layer * 4) * dp;
                float bottom = layer * depth.Step;
                paint.Color = new Color(color.R, color.G, color.B, (byte)(80 - 14 * layer));
                canvas.DrawRoundRect(inset, bottom - 28 * dp, Width - inset, bottom, 15 * dp, 15 * dp, paint);
            }
        }
    }
}
