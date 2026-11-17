using Android.Graphics;
using Android.Graphics.Drawables;
namespace LauncherApp.UI;
public static class IconQuality
{
    // Some packages publish a solid monochrome mask instead of a usable glyph.
    public static bool UsableMonochrome(Drawable drawable)
    {
        using var bitmap=Bitmap.CreateBitmap(32,32,Bitmap.Config.Argb8888!)!;using var canvas=new Canvas(bitmap);using var original=new Rect(drawable.Bounds);
        try
        {
            drawable.SetBounds(0,0,32,32);drawable.Draw(canvas);var pixels=new int[1024];bitmap.GetPixels(pixels,0,32,0,0,32,32);
            int visible=pixels.Count(p=>(uint)p>>24>96);return visible>8&&visible<800;
        }
        catch(Java.Lang.RuntimeException){return false;}
        finally{drawable.SetBounds(original.Left,original.Top,original.Right,original.Bottom);}
    }
    public static bool Blank(Drawable drawable)
    {
        using var bitmap=Bitmap.CreateBitmap(24,24,Bitmap.Config.Argb8888!)!;using var canvas=new Canvas(bitmap);using var original=new Rect(drawable.Bounds);
        try
        {
            drawable.SetBounds(0,0,24,24);drawable.Draw(canvas);var pixels=new int[576];bitmap.GetPixels(pixels,0,24,0,0,24,24);
            var opaque=pixels.Where(p=>(uint)p>>24>128).ToArray();if(opaque.Length<5)return true;if(opaque.Length<450)return false;
            int R(int p)=>(p>>16)&255;int G(int p)=>(p>>8)&255;int B(int p)=>p&255;
            return opaque.Max(R)-opaque.Min(R)<5&&opaque.Max(G)-opaque.Min(G)<5&&opaque.Max(B)-opaque.Min(B)<5;
        }
        catch(Java.Lang.RuntimeException){return false;}
        finally{drawable.SetBounds(original.Left,original.Top,original.Right,original.Bottom);}
    }
}
