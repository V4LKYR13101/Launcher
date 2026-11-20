using Android.Graphics;
using Launcher.Core;

namespace LauncherApp.UI;

public sealed class Palette(LauncherState state)
{
    private string? cachedSource;
    private bool cachedLight;
    private string resolvedAccent = "#E4FF00";
    private string ResolvedAccent
    {
        get
        {
            if (cachedSource != state.Accent || cachedLight != Light)
            {
                cachedSource = state.Accent;
                cachedLight = Light;
                resolvedAccent = Light ? ColorContrast.ForLightSurface(state.Accent) : ColorContrast.ForDarkSurface(state.Accent);
            }
            return resolvedAccent;
        }
    }
    public bool Light => state.Theme != "Dark";
    public Color Base => Color.ParseColor(Light ? state.Theme == "Sun" ? "#FFFFFF" : "#EAEAE7" : "#080808");
    public Color Card => Color.ParseColor(Light ? "#E1E1DD" : "#191B18");
    public Color Panel => Color.ParseColor(Light ? "#D2D2CD" : "#292C26");
    public Color Sheet => Color.ParseColor(Light ? "#F2F2EE" : "#1D1D1B");
    public Color Divider => new(Muted.R, Muted.G, Muted.B, (byte)46);
    public Color Action => Accent;
    public Color Text => Color.ParseColor(Light ? "#131313" : "#F5F5F2");
    public Color Muted => Color.ParseColor(Light ? "#5D5D58" : "#A3A39F");
    public Color Accent => Color.ParseColor(ResolvedAccent);
    public Color OnAccent
    {
        get
        {
            return ColorContrast.UseDarkText(ResolvedAccent) ? Color.Black : Color.White;
        }
    }
}
