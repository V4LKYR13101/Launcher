using Android.Graphics;
using Android.Graphics.Drawables;
namespace LauncherApp.UI;

// Measure once per cached icon. Transparent padding is not part of its visual size.
public readonly record struct IconGeometry(float Left, float Top, float Right, float Bottom, float Fill)
{
    public static IconGeometry Measure(Drawable source)
    {
        const int size = 64;
        using var bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(bitmap);
        using var previous = new Rect(source.Bounds);
        try
        {
            source.SetBounds(0, 0, size, size);
            source.Draw(canvas);
            var pixels = new int[size * size];
            bitmap.GetPixels(pixels, 0, size, 0, 0, size, size);
            int left = size, top = size, right = 0, bottom = 0, count = 0;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            if ((uint)pixels[y * size + x] >> 24 > 48)
            {
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x + 1);
                bottom = Math.Max(bottom, y + 1);
                count++;
            }
            if (count < 8)
                return new(0, 0, 1, 1, .9f);
            float density = count / (float)((right - left) * (bottom - top));
            float fill = Math.Clamp(.88f + (1 - density) * .12f, .88f, .96f);
            return new(left / (float)size, top / (float)size, right / (float)size, bottom / (float)size, fill);
        }
        catch (Java.Lang.RuntimeException) { return new(0, 0, 1, 1, .9f); }
        finally { source.SetBounds(previous.Left, previous.Top, previous.Right, previous.Bottom); }
    }
}

public sealed class OpticalIconDrawable(Drawable source, IconGeometry geometry) : Drawable
{
    public override int IntrinsicWidth => 64;
    public override int IntrinsicHeight => 64;
    public override void Draw(Canvas canvas)
    {
        float width = geometry.Right - geometry.Left, height = geometry.Bottom - geometry.Top;
        float scale = Math.Min(Bounds.Width() / Math.Max(.01f, width), Bounds.Height() / Math.Max(.01f, height)) * geometry.Fill;
        int checkpoint = canvas.Save();
        canvas.Translate(Bounds.ExactCenterX() - (geometry.Left + width / 2) * scale, Bounds.ExactCenterY() - (geometry.Top + height / 2) * scale);
        canvas.Scale(scale / 64, scale / 64);
        source.SetBounds(0, 0, 64, 64);
        source.Draw(canvas);
        canvas.RestoreToCount(checkpoint);
    }
    public override void SetAlpha(int alpha)
    {
        source.SetAlpha(alpha);
        InvalidateSelf();
    }
    public override void SetColorFilter(ColorFilter? filter)
    {
        source.SetColorFilter(filter);
        InvalidateSelf();
    }
    public override int Opacity => (int)Format.Translucent;
}
