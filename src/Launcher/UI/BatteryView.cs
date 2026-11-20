using Android.Animation;
using Android.Content;
using Android.Graphics;
using Android.Views;

namespace LauncherApp.UI;

public sealed class BatteryView(Context context, Color accent, Color outline) : View(context)
{
    private float level = 1, pulse = 1;
    private bool charging;
    private ValueAnimator? fillAnimation, pulseAnimation;
    public void SetState(int percent, bool isCharging)
    {
        float target = Math.Clamp(percent / 100f, 0, 1);
        if (Math.Abs(level - target) > .001f)
        {
            fillAnimation?.Cancel();
            fillAnimation = ValueAnimator.OfFloat(level, target)!;
            fillAnimation.SetDuration(350);
            fillAnimation.Update += (_, e) => { level = ((Java.Lang.Float)e.Animation!.AnimatedValue!).FloatValue(); Invalidate(); };
            fillAnimation.Start();
        }
        ContentDescription = $"Battery {percent} percent" + (isCharging ? ", charging" : "");
        if (charging != isCharging)
        {
            charging = isCharging;
            AnimateCharging();
        }
        Invalidate();
    }
    private void AnimateCharging()
    {
        pulseAnimation?.Cancel();
        pulse = 1;
        if (!charging || !IsAttachedToWindow)
            return;
        pulseAnimation = ValueAnimator.OfFloat(.45f, 1f)!;
        pulseAnimation.SetDuration(1000);
        pulseAnimation.RepeatCount = ValueAnimator.Infinite;
        pulseAnimation.RepeatMode = ValueAnimatorRepeatMode.Reverse;
        pulseAnimation.Update += (_, e) => { pulse = ((Java.Lang.Float)e.Animation!.AnimatedValue!).FloatValue(); Invalidate(); };
        pulseAnimation.Start();
    }
    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        AnimateCharging();
    }
    protected override void OnDetachedFromWindow()
    {
        fillAnimation?.Cancel();
        pulseAnimation?.Cancel();
        base.OnDetachedFromWindow();
    }
    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        canvas.Save();
        canvas.Scale(Width / 28f, Height / 16f);
        using var p = new Paint(PaintFlags.AntiAlias) { Color = outline, StrokeWidth = 1.2f };
        p.SetStyle(Paint.Style.Stroke);
        canvas.DrawRoundRect(1, 2, 24, 14, 2, 2, p);
        p.SetStyle(Paint.Style.Fill);
        canvas.DrawRoundRect(25, 6, 27, 10, 1, 1, p);
        p.Color = accent;
        p.Alpha = (int)(255 * pulse);
        if (level > 0)
            canvas.DrawRoundRect(3, 4, 3 + 19 * level, 12, 1, 1, p);
        if (charging)
        {
            p.Color = outline;
            p.Alpha = 255;
            using var bolt = new Android.Graphics.Path();
            bolt.MoveTo(14, 1);
            bolt.LineTo(9, 8);
            bolt.LineTo(13, 8);
            bolt.LineTo(10, 15);
            bolt.LineTo(18, 6);
            bolt.LineTo(14, 6);
            bolt.Close();
            canvas.DrawPath(bolt, p);
        }
        canvas.Restore();
    }
}
