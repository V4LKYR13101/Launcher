using Android.Graphics;
using Android.Graphics.Drawables;
namespace LauncherApp.UI;

public sealed class FontIconDrawable(Typeface typeface, string glyph) : Drawable
{
    private Color ink = Color.White; private int alpha = 255; private ColorFilter? filter;
    public void SetInk(Color colour)
    {
        ink = colour;
        InvalidateSelf();
    }
    public override int IntrinsicWidth => 48;
    public override int IntrinsicHeight => 48;
    public override void Draw(Canvas canvas)
    {
        if (Bounds.Width() <= 0 || Bounds.Height() <= 0)
            return;
        using var paint = new Paint(PaintFlags.AntiAlias) { TextSize = 100, Color = ink, Alpha = alpha };
        paint.SetTypeface(typeface);
        paint.SetColorFilter(filter);
        using var extent = new Rect();
        paint.GetTextBounds(glyph, 0, glyph.Length, extent);
        if (extent.Width() <= 0 || extent.Height() <= 0)
            return;
        paint.TextSize = 100 * Math.Min(Bounds.Width() / (float)extent.Width(), Bounds.Height() / (float)extent.Height()) * .88f;
        paint.GetTextBounds(glyph, 0, glyph.Length, extent);
        canvas.DrawText(glyph, Bounds.Left + (Bounds.Width() - extent.Width()) / 2f - extent.Left, Bounds.Top + (Bounds.Height() - extent.Height()) / 2f - extent.Top, paint);
    }
    public override void SetAlpha(int value)
    {
        alpha = value;
        InvalidateSelf();
    }
    public override void SetColorFilter(ColorFilter? value)
    {
        filter = value;
        InvalidateSelf();
    }
    public override int Opacity => (int)Format.Translucent;
    public override ConstantState GetConstantState() => new FontState(typeface, glyph);
    private sealed class FontState(Typeface font, string text) : ConstantState
    {
        public override Android.Content.PM.ConfigChanges ChangingConfigurations => 0;
        public override Drawable NewDrawable() => new FontIconDrawable(font, text);
    }
}
