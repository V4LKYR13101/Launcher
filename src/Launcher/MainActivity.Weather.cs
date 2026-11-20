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
    private string? weatherError;
    private int weatherRequest;
    private long weatherAttemptAt;
    private string Degrees(double? c)
    {
        if (c is null)
            return "—";
        return Math.Round(state.Weather.Fahrenheit ? c.Value * 9 / 5 + 32 : c.Value).ToString("0") + "°";
    }
    private void AddWeatherStrip()
    {
        var weather = state.Weather;
        if (!weather.Enabled || string.IsNullOrEmpty(weather.City))
            return;
        var strip = Stack(true);
        strip.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
        strip.SetGravity(GravityFlags.CenterVertical);
        strip.Background = Background(palette.Card, 16);
        strip.ContentDescription = "Weather settings";
        strip.AddView(new GlyphView(this, "map", palette.Muted), new LinearLayout.LayoutParams(Dp(20), Dp(20)));
        var labels = Stack();
        labels.SetPadding(Dp(10), 0, Dp(8), 0);
        var city = Text(string.IsNullOrEmpty(weather.City) ? "Your weather" : weather.City, 13, true);
        city.SetSingleLine(true);
        city.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        labels.AddView(city, Match());
        string detail = weather.Temperature.HasValue ? WeatherCondition.Label(weather.Code) : weatherLoading ? "Updating…" : "Choose a city in Settings";
        if (weather.UpdatedAt > 0 && Now - weather.UpdatedAt > 3 * 60 * 60_000L)
            detail += " · cached";
        var condition = Text(detail, 11, color: palette.Accent);
        condition.SetPadding(0, Dp(4), 0, 0);
        labels.AddView(condition, Match());
        strip.AddView(labels, new LinearLayout.LayoutParams(0, -2, 1));
        var temperature = Text(Degrees(weather.Temperature), 24, true, palette.Accent);
        temperature.Gravity = GravityFlags.CenterVertical;
        strip.AddView(temperature);
        if (weather.Temperature.HasValue)
            strip.AddView(new GlyphView(this, WeatherCondition.Glyph(weather.Code), palette.Accent), new LinearLayout.LayoutParams(Dp(28), Dp(28)));
        TouchFeedback(strip, 16);
        strip.Click += (_, _) => Navigate("Weather");
        BindHoldHaptics(strip);
        strip.LongClick += (_, _) => RefreshWeather(true);
        var lp = Match();
        lp.SetMargins(0, 0, 0, Dp(16));
        body.AddView(strip, lp);
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
        Title("Weather");
        ToggleRow(body, "Weather strip on home", state.Weather.Enabled, v => { state.Weather.Enabled = v; Save(); if (v) RefreshWeather(); });
        SettingRow(body, "Location", string.IsNullOrEmpty(state.Weather.City) ? "Search for a city or town" : state.Weather.City, () =>
            Prompt("Weather location", "", SearchWeatherCity), "map");
        SettingRow(body, "Temperature units", state.Weather.Fahrenheit ? "Fahrenheit · °F" : "Celsius · °C", () => Choose("Temperature units", ["Celsius · °C", "Fahrenheit · °F"], i => { state.Weather.Fahrenheit = i == 1; Save(); Render(); }), "sun");
        SettingRow(body, "Automatic refresh", state.Weather.RefreshMinutes == 0 ? "Manual only" : $"Every {state.Weather.RefreshMinutes} minutes while Launcher is open", () => Choose("Weather refresh", ["Manual only", "30 minutes", "1 hour", "3 hours", "6 hours"], i => { state.Weather.RefreshMinutes = new[] { 0, 30, 60, 180, 360 }[i]; Save(); Render(); }), "clock");
        if (state.Weather.Temperature.HasValue)
        {
            Gap(body);
            body.AddView(Text(Degrees(state.Weather.Temperature), 54, true, palette.Accent), Match());
            body.AddView(Text(WeatherCondition.Label(state.Weather.Code), 20, true, palette.Accent), Match());
            Gap(body, 8);
            Paragraph($"High {Degrees(state.Weather.High)}  ·  Low {Degrees(state.Weather.Low)}");
            Paragraph("Updated " + DateTimeOffset.FromUnixTimeMilliseconds(state.Weather.UpdatedAt).ToLocalTime().ToString("ddd, HH:mm"));
        }
        if (weatherError is not null)
            Paragraph(weatherError);
        body.AddView(Button(weatherLoading ? "Updating…" : "Refresh weather", () => RefreshWeather(true)), Match());
        Gap(body);
        Paragraph("Choose any city. Launcher sends the city search and selected coordinates to Open-Meteo for weather; it does not use GPS or track your location.");
        SettingRow(body, "Weather by Open-Meteo", "Forecast and location data · attribution", () => OpenUrl("https://open-meteo.com/"), "cloud");
    }
    private async void SearchWeatherCity(string query)
    {
        if (query.Trim().Length < 2)
        {
            Toast("Enter a city or town name.");
            return;
        }
        Toast("Searching locations…");
        try
        {
            var places = await WeatherClient.Search(query.Trim());
            if (IsDestroyed)
                return;
            if (places.Length == 0)
            {
                Toast("No matching locations. Try adding a country.");
                return;
            }
            Choose("Select location", places.Select(p => p.Label).ToArray(), i =>
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
            Toast("Couldn’t search locations. Check your connection and try again.");
        }
    }
}
