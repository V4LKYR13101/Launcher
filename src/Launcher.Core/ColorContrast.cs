namespace Launcher.Core;

public static class ColorContrast
{
    // Keep the hue while reducing brightness only as far as the light surfaces need.
    public static string ForLightSurface(string hex)
    {
        if(hex.Length!=7 || hex[0]!='#')throw new ArgumentException("Expected #RRGGBB.",nameof(hex));
        int r=Convert.ToInt32(hex.Substring(1,2),16),g=Convert.ToInt32(hex.Substring(3,2),16),b=Convert.ToInt32(hex.Substring(5,2),16);
        static double Linear(double c) {c/=255;return c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);}
        static double Luminance(int r,int g,int b)=>.2126*Linear(r)+.7152*Linear(g)+.0722*Linear(b);
        // Panel is the darkest opaque light-mode surface used for accent content.
        double surface=Luminance(210,210,205);
        double Contrast(double scale)=>(surface+.05)/(Luminance((int)(r*scale),(int)(g*scale),(int)(b*scale))+.05);
        if(Contrast(1)>=4.5)return hex;
        double low=0,high=1;
        for(int i=0;i<16;i++){double mid=(low+high)/2;if(Contrast(mid)>=4.5)low=mid;else high=mid;}
        return $"#{(int)(r*low):X2}{(int)(g*low):X2}{(int)(b*low):X2}";
    }

    // Dark custom accents still need to remain readable on dark cards and panels.
    public static string ForDarkSurface(string hex)
    {
        if(hex.Length!=7||hex[0]!='#')throw new ArgumentException("Expected #RRGGBB.",nameof(hex));
        int r=Convert.ToInt32(hex.Substring(1,2),16),g=Convert.ToInt32(hex.Substring(3,2),16),b=Convert.ToInt32(hex.Substring(5,2),16);
        static double Linear(double value){value/=255;return value<=.04045?value/12.92:Math.Pow((value+.055)/1.055,2.4);}
        static double Luminance(double red,double green,double blue)=>.2126*Linear(red)+.7152*Linear(green)+.0722*Linear(blue);
        double surface=Luminance(55,57,52);
        double Contrast(double mix)=>(Luminance(r+(255-r)*mix,g+(255-g)*mix,b+(255-b)*mix)+.05)/(surface+.05);
        if(Contrast(0)>=4.5)return hex;
        double low=0,high=1;for(int i=0;i<16;i++){double middle=(low+high)/2;if(Contrast(middle)>=4.5)high=middle;else low=middle;}
        return $"#{(int)Math.Ceiling(r+(255-r)*high):X2}{(int)Math.Ceiling(g+(255-g)*high):X2}{(int)Math.Ceiling(b+(255-b)*high):X2}";
    }

    // WCAG relative luminance: choose whichever of black/white has higher contrast.
    public static bool UseDarkText(string hex)
    {
        if(hex.Length!=7 || hex[0]!='#')throw new ArgumentException("Expected #RRGGBB.",nameof(hex));
        double Linear(int offset)
        {
            double c=Convert.ToInt32(hex.Substring(offset,2),16)/255d;
            return c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);
        }
        double luminance=.2126*Linear(1)+.7152*Linear(3)+.0722*Linear(5);
        return (luminance+.05)/.05 >= 1.05/(luminance+.05);
    }
}
