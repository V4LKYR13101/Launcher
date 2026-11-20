namespace Launcher.Core;

public sealed class WeatherSettings
{
    public bool Enabled { get; set; } = true;
    public string City { get; set; } = "";
    public double? Latitude
    {
        get; set;
    }
    public double? Longitude
    {
        get; set;
    }
    public bool Fahrenheit
    {
        get; set;
    }
    public int RefreshMinutes { get; set; } = 30;
    public double? Temperature
    {
        get; set;
    }
    public double? High
    {
        get; set;
    }
    public double? Low
    {
        get; set;
    }
    public int Code
    {
        get; set;
    }
    public bool Day { get; set; } = true;
    public long UpdatedAt
    {
        get; set;
    }
    public List<ForecastHour> Hours { get; set; } = [];
    public List<ForecastDay> Days { get; set; } = [];
    public int UtcOffsetSeconds
    {
        get; set;
    }
}

public static class DrawerIcons
{
    public static readonly string[] All = ["arrow", "rectangle", "info", "portrait", "triangle-down", "diamond", "vertical", "dash", "triangle", "circle", "plus", "pause", "equals", "double-dot", "ring", "link", "small-circle", "zigzag", "wave", "star"];
    public static string ForDrawer(string id) => id switch
    {
        "essentials" => "star",
        "creativity" => "wave",
        "messaging" => "link",
        "productivity" => "pause",
        "lifestyle" => "triangle",
        "entertainment" => "rectangle",
        "social" => "circle",
        "finance" => "equals",
        _ => "plus"
    };
}

public static class WeatherCondition
{
    public static string Label(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mostly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        51 or 53 or 55 => "Drizzle",
        56 or 57 => "Freezing drizzle",
        61 or 63 or 65 => "Rain",
        66 or 67 => "Freezing rain",
        71 or 73 or 75 or 77 => "Snow",
        80 or 81 or 82 => "Rain showers",
        85 or 86 => "Snow showers",
        95 or 96 or 99 => "Thunderstorms",
        _ => "Conditions unavailable"
    };
    public static string Glyph(int code) => code switch
    {
        0 or 1 => "sun",
        2 or 3 or 45 or 48 => "cloud",
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "rain",
        95 or 96 or 99 => "storm",
        _ => "cloud"
    };
}
