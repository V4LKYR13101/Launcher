using Android.Content;
using Android.Graphics;
using Android.Views;
namespace LauncherApp.UI;

public sealed class ResourceDonutView(Context context, Color accent, Color track, Color text, Color muted, Typeface face, string unit) : View(context)
{
    private double? value;
    public void Update(double? percent)
    {
        value = percent is { } v && double.IsFinite(v) ? Math.Clamp(v, 0, 100) : null;
        Invalidate();
    }
    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        float size = Math.Min(Width, Height), cx = Width / 2f, cy = Height / 2f;
        float radius = size * .41f, stroke = size * .065f;
        using var paint = new Paint(PaintFlags.AntiAlias) { Color = track, StrokeWidth = stroke, StrokeCap = Paint.Cap.Round };
        paint.SetStyle(Paint.Style.Stroke);
        canvas.DrawCircle(cx, cy, radius, paint);
        if (value is > 0)
        {
            paint.Color = accent;
            canvas.DrawArc(cx - radius, cy - radius, cx + radius, cy + radius, -90, (float)value.Value * 3.6f, false, paint);
        }
        paint.SetStyle(Paint.Style.Fill);
        paint.SetTypeface(face);
        paint.TextAlign = Paint.Align.Center;
        paint.Color = text;
        paint.TextSize = size * .215f;
        string label = value is { } number ? $"{number:0}%" : "n/a";
        canvas.DrawText(label, cx, cy - (paint.Ascent() + paint.Descent()) / 2 - size * .045f, paint);
        paint.Color = muted;
        paint.TextSize = size * .10f;
        canvas.DrawText(value is null ? "unavailable" : unit, cx, cy + size * .185f, paint);
    }
}
