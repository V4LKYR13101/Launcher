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
            ambientReading.Text = ambient.Lux is { } current ? Localize(Resource.String.ui_current_light_value_lux, current) : ambient.Available ? state.AutomaticLightTheme ? ambient.Running ? Localize(Resource.String.ui_waiting_for_a_light_reading) : Localize(Resource.String.ui_the_sensor_could_not_start_toggle_automatic_switching_off) : Localize(Resource.String.ui_sensor_available_enable_automatic_switching) : Localize(Resource.String.ui_no_ambient_light_sensor_is_available_on_this_phone);
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
        Title(Localize(Resource.String.ui_follow_the_light));
        Paragraph(Localize(Resource.String.ui_use_the_phone_s_ambient_light_sensor_to_switch));
        ToggleRow(body, Localize(Resource.String.ui_automatic_light_dark), state.AutomaticLightTheme, enabled =>
        {
            if (enabled && !ambient.Available)
            {
                Toast(Localize(Resource.String.ui_this_phone_does_not_report_an_ambient_light_sensor));
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
        SettingRow(body, Localize(Resource.String.ui_switch_to_light), Localize(Resource.String.ui_at_or_above_value_lux, state.LightThemeLux), () => LightThreshold(true), "sun");
        SettingRow(body, Localize(Resource.String.ui_switch_to_dark), Localize(Resource.String.ui_at_or_below_value_lux, state.DarkThemeLux), () => LightThreshold(false), "photo");
        SettingRow(body, Localize(Resource.String.ui_switching_delay), state.LightThemeDelaySeconds + Localize(Resource.String.ui_seconds), () => Choose(Resource.String.ui_switching_delay, [Localize(Resource.String.ui_3_seconds), Localize(Resource.String.ui_5_seconds), Localize(Resource.String.ui_10_seconds), Localize(Resource.String.ui_20_seconds)], i => { state.LightThemeDelaySeconds = new[] { 3, 5, 10, 20 }[i]; lightCandidate = null; Save(); RenderBody(); }), "clock");
        Paragraph(Localize(Resource.String.ui_the_light_level_must_stay_beyond_a_threshold_for));
        Paragraph(Localize(Resource.String.ui_the_sensor_is_used_only_while_launcher_is_visible));
    }
    private void LightThreshold(bool bright) => Prompt(bright ? Resource.String.ui_light_threshold_lux : Resource.String.ui_dark_threshold_lux, (bright ? state.LightThemeLux : state.DarkThemeLux).ToString("0.#", UiCulture), value =>
    {
        if ((!double.TryParse(value, System.Globalization.NumberStyles.Float, UiCulture, out var lux) && !double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lux)) || !double.IsFinite(lux) || lux < 0 || lux > 200_000)
        {
            Toast(Localize(Resource.String.ui_enter_a_light_level_from_0_to_200000_lux));
            return;
        }
        if (bright && lux <= state.DarkThemeLux || !bright && lux >= state.LightThemeLux)
        {
            Toast(Localize(Resource.String.ui_the_light_threshold_must_be_higher_than_the_dark));
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
