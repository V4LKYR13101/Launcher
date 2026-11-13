namespace Launcher.Core;

public static class ColorContrast
{
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
