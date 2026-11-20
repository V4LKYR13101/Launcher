using Android.OS;
using Android.Widget;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private AmbientLightMonitor ambient = null!;
    private TextView? ambientReading;
    private string? lightCandidate;
    private long lightCandidateSince, lastLightSwitch;
    private void SyncAutomaticTheme()
    {
        if (ambient is null)
            return;
        if (resumed && state.AutomaticLightTheme)
        {
            if (!ambient.Running)
                ambient.Start();
        }
        else
        {
            ambient.Stop();
            lightCandidate = null;
        }
    }
    private void SyncSystemTheme()
    {
        if (state.FollowSystemTheme)
            state.Theme = (Resources!.Configuration!.UiMode & Android.Content.Res.UiMode.NightMask) == Android.Content.Res.UiMode.NightYes ? "Dark" : "Light";
    }
    private void ManualTheme(string theme)
    {
        state.FollowSystemTheme = false;
        state.AutomaticLightTheme = false;
        state.Theme = theme;
        if (theme == "Sun")
            state.UseWallpaper = false;
        Save();
        Render();
    }
    private void EvaluateAutomaticTheme()
    {
        if (ambientReading is not null && page == "Automatic theme")
            ambientReading.Text = ambient.Lux is { } current ? $"Current light · {current:0.#} lux" : ambient.Available ? state.AutomaticLightTheme ? ambient.Running ? "Waiting for a light reading…" : "The sensor could not start. Toggle automatic switching off and on." : "Sensor available · enable automatic switching" : "No ambient-light sensor is available on this phone.";
        if (!resumed || !state.AutomaticLightTheme || ambient.Lux is not { } lux)
            return;
        string? next = lux >= state.LightThemeLux && state.Theme != "Light" ? "Light" : lux <= state.DarkThemeLux && state.Theme != "Dark" ? "Dark" : null;
        if (next is null)
        {
            lightCandidate = null;
            return;
        }
        long now = SystemClock.ElapsedRealtime();
        if (lightCandidate != next)
        {
            lightCandidate = next;
            lightCandidateSince = now;
            return;
        }
        if (now - lightCandidateSince < state.LightThemeDelaySeconds * 1000L || lastLightSwitch > 0 && now - lastLightSwitch < 10_000)
            return;
        // Avoid replacing a sheet or a note editor underneath the user's fingers.
        if (WorkspaceEditing.NoteId is not null || searching || openSheetCount > 0)
            return;
        int y = scroll.ScrollY;
        state.Theme = next;
        lastLightSwitch = now;
        lightCandidate = null;
        Save();
        Render();
        var target = scroll;
        target.Post(() => target.ScrollTo(0, y));
    }
    private void RenderAutomaticTheme()
    {
        Title("Follow the light.");
        Paragraph("Use the phone's ambient-light sensor to switch Launcher between Light and Dark. Readings are in lux. This changes Launcher only.");
        ToggleRow(body, "Automatic light / dark", state.AutomaticLightTheme, enabled =>
        {
            if (enabled && !ambient.Available)
            {
                Toast("This phone does not report an ambient-light sensor.");
                RenderBody();
                return;
            }
            state.AutomaticLightTheme = enabled;
            if (enabled)
                state.FollowSystemTheme = false;
            lightCandidate = null;
            Save();
            RenderBody();
        });
        ambientReading = Text("", 14, true, palette.Action);
        ambientReading.SetPadding(0, Dp(12), 0, Dp(16));
        body.AddView(ambientReading, Match());
        EvaluateAutomaticTheme();
        SettingRow(body, "Switch to Light", $"At or above {state.LightThemeLux:0.#} lux", () => LightThreshold(true), "sun");
        SettingRow(body, "Switch to Dark", $"At or below {state.DarkThemeLux:0.#} lux", () => LightThreshold(false), "photo");
        SettingRow(body, "Switching delay", state.LightThemeDelaySeconds + " seconds", () => Choose("Switching delay", ["3 seconds", "5 seconds", "10 seconds", "20 seconds"], i => { state.LightThemeDelaySeconds = new[] { 3, 5, 10, 20 }[i]; lightCandidate = null; Save(); RenderBody(); }), "clock");
        Paragraph("The light level must stay beyond a threshold for the chosen delay. Separate Light and Dark thresholds prevent rapid switching. Choosing a theme manually turns automatic switching off.");
        Paragraph("The sensor is used only while Launcher is visible. No camera, photos, location or background polling is used. Brightness is a light-level signal, not a reliable indoor/outdoor classification.");
    }
    private void LightThreshold(bool bright) => Prompt(bright ? "Light threshold (lux)" : "Dark threshold (lux)", (bright ? state.LightThemeLux : state.DarkThemeLux).ToString("0.#"), value =>
    {
        if ((!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out var lux) && !double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lux)) || !double.IsFinite(lux) || lux < 0 || lux > 200_000)
        {
            Toast("Enter a light level from 0 to 200000 lux.");
            return;
        }
        if (bright && lux <= state.DarkThemeLux || !bright && lux >= state.LightThemeLux)
        {
            Toast("The Light threshold must be higher than the Dark threshold.");
            return;
        }
        if (bright)
            state.LightThemeLux = lux;
        else
            state.DarkThemeLux = lux;
        lightCandidate = null;
        Save();
        RenderBody();
    });
}
