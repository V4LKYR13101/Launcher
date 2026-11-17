using Android.Content;
using Android.Graphics;
using Android.Views;
namespace LauncherApp.UI;

public sealed class OverviewArtwork(Context context,Color accent):View(context)
{
    private readonly Paint paint=new(PaintFlags.AntiAlias){StrokeWidth=1.5f};
    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);float scale=Resources!.DisplayMetrics!.Density,r=Math.Min(Width,Height)*.34f,cx=Width*.7f,cy=Height*.42f;
        paint.SetStyle(Paint.Style.Stroke);paint.StrokeWidth=scale;
        for(int i=0;i<4;i++){paint.Color=new Color(accent.R,accent.G,accent.B,(byte)(36-i*6));canvas.DrawCircle(cx,cy,r+i*scale*12,paint);}
        paint.SetStyle(Paint.Style.Fill);paint.Color=new Color(accent.R,accent.G,accent.B,(byte)35);canvas.DrawCircle(cx+r*.7f,cy-r*.7f,scale*7,paint);
        paint.Color=new Color(accent.R,accent.G,accent.B,(byte)100);canvas.DrawCircle(cx-r*.7f,cy+r*.7f,scale*3,paint);
    }
}
