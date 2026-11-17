using Android.Content;
using Android.Graphics;
using Android.Views;
namespace LauncherApp.UI;
public sealed class HistoryChart(Context context,float[] values,string[] labels,Color accent,Color foreground,bool bars=false,string unit="m"):View(context)
{
    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);if(values.Length==0)return;
        canvas.Save();canvas.Scale(Width/400f,Height/180f);
        using var p=new Paint(PaintFlags.AntiAlias){Color=foreground,TextSize=11};
        float min=bars?0:values.Min(),max=values.Max();if(max-min<.01f)max=min+1;
        if(bars)max=new float[]{1,5,15,30,60,120,240,480,960,1440}.FirstOrDefault(v=>v>=max,MathF.Ceiling(max));
        int active=Array.FindLastIndex(values,v=>v>0);
        float X(int i)=>42+i*334f/Math.Max(1,values.Length-1);
        float Y(float value)=>145-(value-min)/(max-min)*120;
        p.Alpha=55;p.StrokeWidth=1;canvas.DrawLine(42,145,380,145,p);canvas.DrawLine(42,25,380,25,p);
        p.Alpha=165;canvas.DrawText(max.ToString("0.#")+unit,2,29,p);canvas.DrawText(min.ToString("0.#")+unit,2,147,p);
        p.Alpha=255;using var path=new Android.Graphics.Path();
        for(int i=0;i<values.Length;i++)
        {
            float x=X(i),y=Y(values[i]);
            if(bars){p.Color=i==active?accent:foreground;p.Alpha=i==active?255:110;canvas.DrawRoundRect(x-4,y,x+4,145,3,3,p);}
            else{if(i==0)path.MoveTo(x,y);else path.LineTo(x,y);}
        }
        if(!bars){p.Color=foreground;p.Alpha=240;p.StrokeWidth=2;p.SetStyle(Paint.Style.Stroke);canvas.DrawPath(path,p);p.SetStyle(Paint.Style.Fill);p.Color=accent;canvas.DrawCircle(X(0),Y(values[0]),4,p);}
        p.Color=foreground;p.Alpha=180;p.TextAlign=Paint.Align.Center;
        foreach(int i in new[]{0,values.Length/2,values.Length-1}.Distinct())if(i<labels.Length)canvas.DrawText(labels[i],X(i),171,p);
        canvas.Restore();
    }
}
