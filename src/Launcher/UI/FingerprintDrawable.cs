
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
    private int opacity = 180;

    public override void Draw(Canvas canvas)
    {
        var b = Bounds;

        if (b.Width() <= 0 || b.Height() <= 0)
            return;

        using var paint = new Paint(PaintFlags.AntiAlias);

        // Background
        paint.Color = background;
        paint.Alpha = opacity;
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

        // Fingerprint occupies 94% of the tile.
        canvas.Translate(
            b.Left + b.Width() / 2f,
            b.Top + b.Height() / 2f);

        float scale = Math.Min(
            b.Width() * 0.94f / 100f,
            b.Height() * 0.94f / 100f);

        canvas.Scale(scale, scale);

        paint.Color = accent;
        paint.Alpha = (int)(opacity * 0.88f);
        paint.SetStyle(Paint.Style.Stroke);
        paint.StrokeWidth = 1.65f;
        paint.StrokeCap = Paint.Cap.Round;
        paint.StrokeJoin = Paint.Join.Round;

        // Outer fingerprint ridges.
        // Drawn from outside inward.
        for (int i = 0; i < 13; i++)
        {
            float rx = 48f - i * 3.3f;
            float ry = 54f - i * 3.5f;

            if (rx <= 5 || ry <= 5)
                break;

            using var path = new Path();

            float bottom = 55f;
            float top = -ry;

            // Left lower opening
            path.MoveTo(-rx * 0.78f, bottom);

            // Left side flowing upward
            path.CubicTo(
                -rx * 1.10f, 30f,
                -rx * 1.08f, -ry * 0.45f,
                -rx * 0.65f, -ry * 0.85f);

            // Crown
            path.CubicTo(
                -rx * 0.30f, top - 5f,
                rx * 0.30f, top - 5f,
                rx * 0.65f, -ry * 0.85f);

            // Right side
            path.CubicTo(
                rx * 1.08f, -ry * 0.45f,
                rx * 1.08f, 23f,
                rx * 0.72f, 39f);

            // Right lower ridge termination
            path.CubicTo(
                rx * 0.55f, 45f,
                rx * 0.70f, 51f,
                rx * 0.85f, bottom);

            canvas.DrawPath(path, paint);
        }

        // Inner fingerprint whorl
        using (var inner = new Path())
        {
            inner.MoveTo(-7f, 55f);

            inner.CubicTo(
                -12f, 39f,
                -16f, 18f,
                -11f, 2f);

            inner.CubicTo(
                -7f, -13f,
                7f, -16f,
                14f, -5f);

            inner.CubicTo(
                22f, 8f,
                13f, 24f,
                8f, 35f);

            inner.CubicTo(
                4f, 44f,
                7f, 51f,
                11f, 55f);

            canvas.DrawPath(inner, paint);
        }

        // Central ridge
        using (var centre = new Path())
        {
            centre.MoveTo(0f, 55f);

            centre.CubicTo(
                -5f, 37f,
                -7f, 16f,
                -2f, 3f);

            centre.CubicTo(
                1f, -4f,
                8f, 1f,
                8f, 9f);

            centre.CubicTo(
                8f, 20f,
                2f, 27f,
                3f, 37f);

            canvas.DrawPath(centre, paint);
        }

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
