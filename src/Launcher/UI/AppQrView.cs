using Android.Content;
using Android.Graphics;
using Android.Views;
using QRCoder;
namespace LauncherApp.UI;
public sealed class AppQrView:View
{
    private readonly QRCodeData code;
    private readonly Paint ink=new(){Color=Color.Black,AntiAlias=false};
    public AppQrView(Context context,string value):base(context)
    {code=QRCodeGenerator.GenerateQrCode(value,QRCodeGenerator.ECCLevel.M);ContentDescription="QR code for this app's Play Store page";}
    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);canvas.DrawColor(Color.White);int count=code.ModuleMatrix.Count;float size=Math.Min(Width,Height),step=size/count,left=(Width-size)/2,top=(Height-size)/2;
        for(int y=0;y<count;y++)for(int x=0;x<count;x++)if(code.ModuleMatrix[y][x])canvas.DrawRect(left+(float)Math.Floor(x*step),top+(float)Math.Floor(y*step),left+(float)Math.Ceiling((x+1)*step),top+(float)Math.Ceiling((y+1)*step),ink);
    }
    protected override void Dispose(bool disposing){if(disposing){code.Dispose();ink.Dispose();}base.Dispose(disposing);}
}
