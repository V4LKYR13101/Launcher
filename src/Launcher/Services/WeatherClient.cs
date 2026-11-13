using System.Text.Json;

namespace LauncherApp.Services;

public sealed record WeatherPlace(string Name,string Region,string Country,double Latitude,double Longitude)
{
    public string Label=>string.Join(", ",new[]{Name,Region,Country}.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct());
}
public sealed record WeatherReading(double Temperature,int Code,bool Day,double? High,double? Low);

public static class WeatherClient
{
    private static readonly HttpClient Client=new(){Timeout=TimeSpan.FromSeconds(15)};
    public static async Task<WeatherPlace[]> Search(string query)
    {
        using var stream=await Client.GetStreamAsync("https://geocoding-api.open-meteo.com/v1/search?name="+System.Uri.EscapeDataString(query)+"&count=8&language=en&format=json");
        using var json=await JsonDocument.ParseAsync(stream);
        if(!json.RootElement.TryGetProperty("results",out var results))return [];
        string Read(JsonElement e,string name)=>e.TryGetProperty(name,out var p)?p.GetString()??"":"";
        return results.EnumerateArray().Select(e=>new WeatherPlace(Read(e,"name"),Read(e,"admin1"),Read(e,"country"),e.GetProperty("latitude").GetDouble(),e.GetProperty("longitude").GetDouble())).ToArray();
    }
    public static async Task<WeatherReading> Fetch(double latitude,double longitude)
    {
        string url=FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,weather_code,is_day&daily=temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=1");
        using var stream=await Client.GetStreamAsync(url);using var json=await JsonDocument.ParseAsync(stream);
        var current=json.RootElement.GetProperty("current");var daily=json.RootElement.GetProperty("daily");
        double? First(string name)=>daily.TryGetProperty(name,out var values)&&values.GetArrayLength()>0&&values[0].ValueKind==JsonValueKind.Number?values[0].GetDouble():null;
        return new(current.GetProperty("temperature_2m").GetDouble(),current.GetProperty("weather_code").GetInt32(),current.GetProperty("is_day").GetInt32()==1,First("temperature_2m_max"),First("temperature_2m_min"));
    }
}
