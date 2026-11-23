using Android.Animation;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;

namespace LauncherApp.UI;

internal sealed class TouchGlowDrawable(int radius) : Drawable
{
    private readonly Paint paint = new(PaintFlags.AntiAlias | PaintFlags.Dither);
    private RadialGradient? gradient;
    private ValueAnimator? fade;
    private Color accent;
    private int pointer = -1;
    private int opacity;

    internal void Track(MotionEvent touch, Color color)
    {
        switch (touch.ActionMasked)
        {
            case MotionEventActions.Down:
                fade?.Cancel();
                pointer = touch.GetPointerId(0);
                if (gradient is null || accent != color)
                {
                    accent = color;
                    gradient?.Dispose();
                    gradient = new RadialGradient(0, 0, radius,
                        [new Color(accent.R, accent.G, accent.B, (byte)116).ToArgb(),
                         new Color(accent.R, accent.G, accent.B, (byte)55).ToArgb(),
                         new Color(accent.R, accent.G, accent.B, (byte)0).ToArgb()],
                        [0f, .4f, 1f], Shader.TileMode.Clamp!);
                    paint.SetShader(gradient);
                }
                SetAlpha(255);
                Move(touch, 0);
                break;
            case MotionEventActions.Move when pointer >= 0:
                int index = touch.FindPointerIndex(pointer);
                if (index >= 0)
                    Move(touch, index);
                break;
            case MotionEventActions.Up or MotionEventActions.PointerUp when touch.GetPointerId(touch.ActionIndex) == pointer:
                Move(touch, touch.ActionIndex);
                pointer = -1;
                FadeOut();
                break;
            case MotionEventActions.Cancel:
                Clear();
                break;
        }
    }

    private void Move(MotionEvent touch, int index)
    {
        int x = (int)MathF.Round(touch.GetX(index));
        int y = (int)MathF.Round(touch.GetY(index));
        SetBounds(x - radius, y - radius, x + radius, y + radius);
        InvalidateSelf();
    }

    private void FadeOut()
    {
        if (fade is null)
        {
            fade = ValueAnimator.OfInt(255, 0)!;
            fade.SetDuration(180);
            fade.Update += (_, args) => SetAlpha((int)((Java.Lang.Integer)args.Animation!.AnimatedValue!));
        }
        fade.Start();
    }

    internal void Clear()
    {
        pointer = -1;
        fade?.Cancel();
        SetAlpha(0);
    }

    public override void Draw(Canvas canvas)
    {
        if (opacity == 0 || gradient is null)
            return;
        canvas.Save();
        canvas.Translate(Bounds.Left + radius, Bounds.Top + radius);
        canvas.DrawCircle(0, 0, radius, paint);
        canvas.Restore();
    }

    public override void SetAlpha(int value)
    {
        if (opacity == value)
            return;
        paint.Alpha = opacity = value;
        InvalidateSelf();
    }

    public override void SetColorFilter(ColorFilter? filter) => paint.SetColorFilter(filter);
    public override int Opacity => (int)Format.Translucent;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fade?.Dispose();
            gradient?.Dispose();
            paint.Dispose();
        }
        base.Dispose(disposing);
    }
}
