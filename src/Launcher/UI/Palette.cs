using Android.Graphics;
using Launcher.Core;

namespace LauncherApp.UI;

public sealed class Palette(LauncherState state)
{
    public bool Light => state.Theme != "Dark";
    public Color Base => Color.ParseColor(Light ? state.Theme == "Sun" ? "#FFFFFF" : "#EAEAE7" : "#080808");
    public Color Card => Color.ParseColor(Light ? "#E1E1DD" : "#242424");
    public Color Panel => Color.ParseColor(Light ? "#D2D2CD" : "#383838");
    public Color Text => Color.ParseColor(Light ? "#131313" : "#F5F5F2");
    public Color Muted => Color.ParseColor(Light ? "#5D5D58" : "#A3A39F");
    public Color Accent => Color.ParseColor(state.Accent);
    public Color OnAccent
    {
        get
        {
            return ColorContrast.UseDarkText(state.Accent) ? Color.Black : Color.White;
        }
    }
}
