
using System;
using Android.Graphics;
using Android.Graphics.Drawables;
using Path = Android.Graphics.Path;

namespace LauncherApp.UI;

public sealed class FingerprintDrawable(
    Color background,
    Color accent,
    float radius,
    Color? outline = null) : Drawable
{
    private int opacity = 255;

    private Path? ridges;

    protected override void OnBoundsChange(Rect bounds)
    {
        base.OnBoundsChange(bounds);
        ridges?.Dispose();
        ridges = null;
        float w = bounds.Width(), h = bounds.Height();
        if (w <= 0 || h <= 0)
            return;
        // The earlier full-tile whorl, with gentler shaping and constant radial pitch.
        // One continuous ridge cannot overlap adjacent independently drawn curves.
        var path = new Path();
        const double pitch = 4.6;
        const double step = Math.PI / 100;
        double end = 2 * Math.PI * 150 / pitch;
        bool first = true;
        for (double t = 0; t <= end; t += step)
        {
            double r = pitch * t / (2 * Math.PI);
            double shape = 1 + .04 * Math.Sin(3 * t) + .025 * Math.Cos(2 * t);
            float x = w * (float)(.51 + r * shape * Math.Cos(t) / 100);
            float y = h * (float)(.43 + r * shape * Math.Sin(t) / 100);
            if (first)
            {
                path.MoveTo(x, y);
                first = false;
            }
            else
                path.LineTo(x, y);
        }
        ridges = path;
    }


    public override void Draw(Canvas canvas)
    {
        var b = Bounds;

        if (b.Width() <= 0 || b.Height() <= 0)
            return;

        using var paint = new Paint(PaintFlags.AntiAlias);

        // Background
        paint.Color = background;
        paint.Alpha = background.A * opacity / 255;
        paint.SetStyle(Paint.Style.Fill);

        canvas.DrawRoundRect(
            b.Left, b.Top, b.Right, b.Bottom,
            radius, radius, paint);

        canvas.Save();

        // Clip everything to the tile.
        using (var clip = new Path())
        {
            clip.AddRoundRect(
                new RectF(b),
                radius, radius,
                Path.Direction.Cw!);

            canvas.ClipPath(clip);
        }

        canvas.Translate(b.Left, b.Top);
        paint.Color = accent;
        bool inGlass = background.A == 0;
        paint.Alpha = (int)(opacity * (inGlass ? .76f : .62f));
        paint.SetStyle(Paint.Style.Stroke);
        paint.StrokeWidth = Math.Max(inGlass ? 1f : .8f, Math.Min(b.Width(), b.Height()) * (inGlass ? .009f : .007f));
        paint.StrokeCap = Paint.Cap.Round;
        paint.StrokeJoin = Paint.Join.Round;
        if (ridges is not null)
            canvas.DrawPath(ridges, paint);

        canvas.Restore();

        // Actual outline, never a filled rectangle.
        if (outline is { } border)
        {
            paint.Color = border;
            paint.Alpha = opacity;
            paint.SetStyle(Paint.Style.Stroke);
            paint.StrokeWidth = 1.2f;

            canvas.DrawRoundRect(
                b.Left + 0.7f,
                b.Top + 0.7f,
                b.Right - 0.7f,
                b.Bottom - 0.7f,
                radius,
                radius,
                paint);
        }
    }

    public override int Opacity => (int)Format.Translucent;

    public override void SetAlpha(int alpha)
    {
        opacity = Math.Clamp(alpha, 0, 255);
        InvalidateSelf();
    }

    public override void SetColorFilter(ColorFilter? colorFilter)
    {
    }
}
