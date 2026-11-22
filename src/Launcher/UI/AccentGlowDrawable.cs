using Android.Graphics;
using Android.Graphics.Drawables;

namespace LauncherApp.UI;

public sealed class AccentGlowDrawable(Color accent, byte intensity) : Drawable
{
    private Color accentColor = accent;
    private readonly Paint paint = new(PaintFlags.AntiAlias | PaintFlags.Dither);
    private RadialGradient? gradient;
    private int alpha = 255;
    internal int GlowAlpha => alpha;

    public Color Accent
    {
        get => accentColor;
        set
        {
            if (accentColor == value)
                return;
            accentColor = value;
            gradient?.Dispose();
            gradient = null;
            InvalidateSelf();
        }
    }

    public override void Draw(Canvas canvas)
    {
        var area = Bounds;
        float width = area.Width(), height = area.Height();
        if (width <= 0 || height <= 0)
            return;

        gradient ??= new RadialGradient(0, 0, 1,
            [new Color(Accent.R, Accent.G, Accent.B, intensity).ToArgb(),
             new Color(Accent.R, Accent.G, Accent.B, (byte)(intensity / 3)).ToArgb(),
             new Color(Accent.R, Accent.G, Accent.B, (byte)0).ToArgb()],
            [0f, .47f, 1f], Shader.TileMode.Clamp!);
        paint.SetShader(gradient);

        float center = area.Left + width / 2;
        float reach = Math.Min(height * .65f, width * .56f);
        canvas.Save();
        canvas.ClipRect(area);
        DrawEdge(area.Top, reach * .8f, (int)(alpha * .72f));
        DrawEdge(area.Bottom, reach, alpha);
        canvas.Restore();

        void DrawEdge(float edge, float spread, int opacity)
        {
            paint.Alpha = opacity;
            canvas.Save();
            canvas.Translate(center, edge);
            canvas.Scale(width * .74f, spread);
            canvas.DrawCircle(0, 0, 1, paint);
            canvas.Restore();
        }
    }

    public static Color Blend(Color from, Color to, float progress) => new(
        (byte)Math.Round(from.R + (to.R - from.R) * progress),
        (byte)Math.Round(from.G + (to.G - from.G) * progress),
        (byte)Math.Round(from.B + (to.B - from.B) * progress));

    public override void SetAlpha(int value)
    {
        alpha = value;
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
            gradient?.Dispose();
            paint.Dispose();
        }
        base.Dispose(disposing);
    }
}
