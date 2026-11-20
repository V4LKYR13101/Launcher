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
        indicator.ContentDescription = $"Weather settings, {weather.City}, {temperature} {condition}{freshness}";
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
