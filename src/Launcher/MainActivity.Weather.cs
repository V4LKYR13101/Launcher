using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.UI;

namespace LauncherApp;

public partial class MainActivity
{
    private bool weatherLoading;
    private FrameLayout? homeWeatherSlot;
    private string? weatherError;
    private int weatherRequest;
    private long weatherAttemptAt;
    private string Degrees(double? c)
    {
        if (c is null)
            return "—";
        return Math.Round(state.Weather.Fahrenheit ? c.Value * 9 / 5 + 32 : c.Value).ToString("0") + "°";
    }
    private void RenderHomeWeather()
    {
        homeWeatherSlot?.RemoveAllViews();
        var weather = state.Weather;
        if (!weather.Enabled || string.IsNullOrEmpty(weather.City))
            return;

        bool hasReading = weather.Temperature.HasValue;
        string temperature = hasReading ? Degrees(weather.Temperature) + (weather.Fahrenheit ? "F" : "C") : "";
        string condition = hasReading ? WeatherCondition.Label(weather.Code) : weatherLoading ? "Updating…" : "Weather unavailable";
        string freshness = weather.UpdatedAt > 0 && Now - weather.UpdatedAt > 3 * 60 * 60_000L ? ", cached" : "";
        var indicator = Stack();
        indicator.SetGravity(GravityFlags.Center);
        indicator.SetPadding(Dp(14), Dp(4), Dp(14), Dp(5));
        indicator.ContentDescription = Localize(Resource.String.ui_weather_settings_value_value_valuevalue, weather.City, temperature, condition, freshness);
        var icon = new GlyphView(this, hasReading ? WeatherCondition.Glyph(weather.Code) : "cloud", palette.Accent);
        indicator.AddView(icon, new LinearLayout.LayoutParams(Dp(27), Dp(27)));
        if (hasReading)
            indicator.AddView(Text(temperature, 21), new LinearLayout.LayoutParams(-2, -2));
        indicator.AddView(Text(condition, 11, color: palette.Muted), new LinearLayout.LayoutParams(-2, -2));
        TouchFeedback(indicator, 14);
        indicator.Click += (_, _) => Navigate("Weather");
        BindHoldHaptics(indicator);
        indicator.LongClick += (_, _) => RefreshWeather(true);
        var position = new FrameLayout.LayoutParams(-2, -2, GravityFlags.CenterHorizontal);
        if (homeWeatherSlot is { } slot)
            slot.AddView(indicator, position);
        else
        {
            var row = new FrameLayout(this);
            row.AddView(indicator, position);
            body.AddView(row, Match());
        }
    }
    private async void RefreshWeather(bool force = false)
    {
        var w = state.Weather;
        if (!w.Enabled || w.Latitude is null || w.Longitude is null || weatherLoading)
            return;
        if (!force && (w.RefreshMinutes == 0 || Now - weatherAttemptAt < 5 * 60_000L || w.Hours.Count > 0 && w.UpdatedAt > 0 && Now - w.UpdatedAt < w.RefreshMinutes * 60_000L))
            return;
        int request = ++weatherRequest;
        double latitude = w.Latitude.Value, longitude = w.Longitude.Value;
        weatherLoading = true;
        weatherError = null;
        weatherAttemptAt = Now;
        try
        {
            var reading = await WeatherClient.Fetch(latitude, longitude);
            if (IsDestroyed || request != weatherRequest || state.Weather.Latitude != latitude || state.Weather.Longitude != longitude)
                return;
            state.Weather.Temperature = reading.Temperature;
            state.Weather.High = reading.High;
            state.Weather.Low = reading.Low;
            state.Weather.Code = reading.Code;
            state.Weather.Day = reading.Day;
            state.Weather.UpdatedAt = Now;
            Save();
            state.Weather.Hours = reading.Hours;
            state.Weather.Days = reading.Days;
            state.Weather.UtcOffsetSeconds = reading.UtcOffsetSeconds;
            Save();
        }
        catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        {
            if (request == weatherRequest)
                weatherError = "Couldn’t update weather. Your last forecast is kept.";
        }
        finally
        {
            if (request == weatherRequest)
                weatherLoading = false;
            if (!IsDestroyed && resumed && !searching && (page is "Tiles" or "Weather" or "Root") && WorkspaceEditing.NoteId is null)
                RefreshBodyInPlace();
        }
    }
    private void RenderWeatherSettings()
    {
        Title(Localize(Resource.String.ui_weather));
        ToggleRow(body, Localize(Resource.String.ui_weather_strip_on_home), state.Weather.Enabled, v => { state.Weather.Enabled = v; Save(); if (v) RefreshWeather(); });
        SettingRow(body, Localize(Resource.String.ui_location), string.IsNullOrEmpty(state.Weather.City) ? Localize(Resource.String.ui_search_for_a_city_or_town) : state.Weather.City, () =>
            Prompt(Resource.String.ui_weather_location, "", SearchWeatherCity), "map");
        SettingRow(body, Localize(Resource.String.ui_temperature_units), state.Weather.Fahrenheit ? Localize(Resource.String.ui_fahrenheit_f) : Localize(Resource.String.ui_celsius_c), () => Choose(Resource.String.ui_temperature_units, [Localize(Resource.String.ui_celsius_c), Localize(Resource.String.ui_fahrenheit_f)], i => { state.Weather.Fahrenheit = i == 1; Save(); Render(); }), "sun");
        SettingRow(body, Localize(Resource.String.ui_automatic_refresh), state.Weather.RefreshMinutes == 0 ? Localize(Resource.String.ui_manual_only) : Localize(Resource.String.ui_every_value_minutes_while_launcher_is_open, state.Weather.RefreshMinutes), () => Choose(Resource.String.ui_weather_refresh, [Localize(Resource.String.ui_manual_only), Localize(Resource.String.ui_30_minutes), Localize(Resource.String.ui_1_hour), Localize(Resource.String.ui_3_hours), Localize(Resource.String.ui_6_hours)], i => { state.Weather.RefreshMinutes = new[] { 0, 30, 60, 180, 360 }[i]; Save(); Render(); }), "clock");
        if (state.Weather.Temperature.HasValue)
        {
            Gap(body);
            body.AddView(Text(Degrees(state.Weather.Temperature), 54, true, palette.Accent), Match());
            body.AddView(Text(WeatherCondition.Label(state.Weather.Code), 20, true, palette.Accent), Match());
            Gap(body, 8);
            Paragraph(Localize(Resource.String.ui_high_value_low_value, Degrees(state.Weather.High), Degrees(state.Weather.Low)));
            Paragraph(Localize(Resource.String.ui_updated) + DateTimeOffset.FromUnixTimeMilliseconds(state.Weather.UpdatedAt).ToLocalTime().ToString("ddd, HH:mm", UiCulture));
        }
        if (weatherError is not null)
            Paragraph(weatherError);
        body.AddView(Button(weatherLoading ? Localize(Resource.String.ui_updating) : Localize(Resource.String.ui_refresh_weather), () => RefreshWeather(true)), Match());
        Gap(body);
        Paragraph(Localize(Resource.String.ui_choose_any_city_launcher_sends_the_city_search_and));
        SettingRow(body, Localize(Resource.String.ui_weather_by_open_meteo), Localize(Resource.String.ui_forecast_and_location_data_attribution), () => OpenUrl("https://open-meteo.com/"), "cloud");
    }
    private async void SearchWeatherCity(string query)
    {
        if (query.Trim().Length < 2)
        {
            Toast(Localize(Resource.String.ui_enter_a_city_or_town_name));
            return;
        }
        Toast(Localize(Resource.String.ui_searching_locations));
        try
        {
            var places = await WeatherClient.Search(query.Trim());
            if (IsDestroyed)
                return;
            if (places.Length == 0)
            {
                Toast(Localize(Resource.String.ui_no_matching_locations_try_adding_a_country));
                return;
            }
            Choose(Resource.String.ui_select_location, places.Select(p => p.Label).ToArray(), i =>
            {
                var p = places[i];
                state.Weather.City = p.Label;
                state.Weather.Latitude = p.Latitude;
                state.Weather.Longitude = p.Longitude;
                state.Weather.UpdatedAt = 0;
                state.Weather.Temperature = null;
                state.Weather.High = null;
                state.Weather.Low = null;
                state.Weather.Hours.Clear();
                state.Weather.Days.Clear();
                state.Weather.UtcOffsetSeconds = 0;
                weatherRequest++;
                weatherLoading = false;
                Save();
                Render();
                RefreshWeather(true);
            });
        }
        catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        {
            Toast(Localize(Resource.String.ui_couldnt_search_locations_check_your_connection_and_try_again));
        }
    }
}
