using System.Text.Json;

namespace LauncherApp.Services;

public sealed record WeatherPlace(string Name, string Region, string Country, double Latitude, double Longitude)
{
    public string Label => string.Join(", ", new[] { Name, Region, Country }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
}
public sealed record WeatherReading(double Temperature, int Code, bool Day, double? High, double? Low, List<Launcher.Core.ForecastHour> Hours, List<Launcher.Core.ForecastDay> Days, int UtcOffsetSeconds);

public static class WeatherClient
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    public static async Task<WeatherPlace[]> Search(string query)
    {
        using var stream = await Client.GetStreamAsync("https://geocoding-api.open-meteo.com/v1/search?name=" + System.Uri.EscapeDataString(query) + "&count=8&language=en&format=json");
        using var json = await JsonDocument.ParseAsync(stream);
        if (!json.RootElement.TryGetProperty("results", out var results))
            return [];
        string Read(JsonElement e, string name) => e.TryGetProperty(name, out var p) ? p.GetString() ?? "" : "";
        if (results.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid location response.");
        return results.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("latitude", out var lat) && lat.TryGetDouble(out double latitude) && double.IsFinite(latitude) && latitude is >= -90 and <= 90 && e.TryGetProperty("longitude", out var lon) && lon.TryGetDouble(out double longitude) && double.IsFinite(longitude) && longitude is >= -180 and <= 180)
            .Take(8).Select(e => new WeatherPlace(Read(e, "name"), Read(e, "admin1"), Read(e, "country"), e.GetProperty("latitude").GetDouble(), e.GetProperty("longitude").GetDouble())).ToArray();
    }
    public static async Task<WeatherReading> Fetch(double latitude, double longitude)
    {
        string url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,weather_code,is_day&hourly=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min,weather_code&timezone=auto&forecast_days=7&past_days=0");
        using var stream = await Client.GetStreamAsync(url);
        using var json = await JsonDocument.ParseAsync(stream);
        var parsed = Launcher.Core.WeatherParser.Parse(json.RootElement);
        return new(parsed.Temperature, parsed.Code, parsed.Day, parsed.High, parsed.Low, parsed.Hours, parsed.Days, parsed.UtcOffsetSeconds);
    }
}
