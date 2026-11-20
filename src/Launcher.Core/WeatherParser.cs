using System.Text.Json;
namespace Launcher.Core;

public sealed record ParsedWeather(double Temperature, int Code, bool Day, double? High, double? Low, List<ForecastHour> Hours, List<ForecastDay> Days, int UtcOffsetSeconds);
public static class WeatherParser
{
    public static ParsedWeather Parse(JsonElement root)
    {
        try
        {
            var current = root.GetProperty("current");
            var daily = root.GetProperty("daily");
            var hourly = root.GetProperty("hourly");
            double Number(JsonElement value)
            {
                double n = value.GetDouble();
                if (!double.IsFinite(n))
                    throw new InvalidDataException("Invalid weather value.");
                return n;
            }
            double? First(string name) => daily.TryGetProperty(name, out var values) && values.ValueKind == JsonValueKind.Array && values.GetArrayLength() > 0 && values[0].ValueKind == JsonValueKind.Number ? Number(values[0]) : null;
            var hours = new List<ForecastHour>();
            var days = new List<ForecastDay>();
            var times = hourly.GetProperty("time");
            var temperatures = hourly.GetProperty("temperature_2m");
            var codes = hourly.GetProperty("weather_code");
            int count = Math.Min(168, Math.Min(times.GetArrayLength(), Math.Min(temperatures.GetArrayLength(), codes.GetArrayLength())));
            for (int i = 0; i < count; i++)
            if (temperatures[i].ValueKind == JsonValueKind.Number && codes[i].TryGetInt32(out int code) && times[i].GetString() is { } time)
                hours.Add(new(time, Number(temperatures[i]), code));
            times = daily.GetProperty("time");
            var highs = daily.GetProperty("temperature_2m_max");
            var lows = daily.GetProperty("temperature_2m_min");
            codes = daily.GetProperty("weather_code");
            count = Math.Min(7, Math.Min(times.GetArrayLength(), Math.Min(highs.GetArrayLength(), Math.Min(lows.GetArrayLength(), codes.GetArrayLength()))));
            for (int i = 0; i < count; i++)
            if (highs[i].ValueKind == JsonValueKind.Number && lows[i].ValueKind == JsonValueKind.Number && codes[i].TryGetInt32(out int code) && times[i].GetString() is { } date)
                days.Add(new(date, Number(highs[i]), Number(lows[i]), code));
            return new(Number(current.GetProperty("temperature_2m")), current.GetProperty("weather_code").GetInt32(), current.GetProperty("is_day").GetInt32() == 1, First("temperature_2m_max"), First("temperature_2m_min"), hours, days, root.TryGetProperty("utc_offset_seconds", out var offset) && offset.TryGetInt32(out int seconds) ? Math.Clamp(seconds, -86400, 86400) : 0);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException or IndexOutOfRangeException)
        {
            throw new InvalidDataException("The weather provider returned an incomplete forecast.", e);
        }
    }
}
