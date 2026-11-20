using Android.Graphics;
using Android.Graphics.Drawables;

namespace LauncherApp.UI;

internal sealed class DrawerPulseDrawable(Color accent, byte intensity) : Drawable
{
    private readonly Paint paint = new(PaintFlags.AntiAlias | PaintFlags.Dither);
    private RadialGradient? rings;
    private float progress;

    public float Progress
    {
        set
        {
            progress = value;
            InvalidateSelf();
        }
    }

    public override void Draw(Canvas canvas)
    {
        var area = Bounds;
        float width = area.Width(), height = area.Height();
        if (width <= 0 || height <= 0)
            return;
        rings ??= new RadialGradient(0, 0, 1,
            [Ink(0), Ink(0), Ink(.48f), Ink(.08f), Ink(1), Ink(.24f), Ink(0)],
            [0f, .36f, .53f, .65f, .80f, .92f, 1f], Shader.TileMode.Clamp!);
        paint.SetShader(rings);
        float radius = width * .12f + MathF.Sqrt(width * width + height * height) * .91f * progress;
        canvas.Save();
        canvas.ClipRect(area);
        canvas.Translate(area.Left + width / 2f, area.Top + height * .61f);
        canvas.Scale(radius, radius);
        canvas.DrawCircle(0, 0, 1, paint);
        canvas.Restore();
    }

    private int Ink(float strength) => new Color(accent.R, accent.G, accent.B, (byte)(intensity * strength)).ToArgb();

    public override void SetAlpha(int alpha)
    {
        paint.Alpha = alpha;
        InvalidateSelf();
    }

    public override void SetColorFilter(ColorFilter? filter)
    {
        paint.SetColorFilter(filter);
        InvalidateSelf();
    }

    public override int Opacity => (int)Format.Translucent;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            rings?.Dispose();
            paint.Dispose();
        }
        base.Dispose(disposing);
    }
}
