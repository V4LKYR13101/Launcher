namespace LauncherWear;

internal static class RollSync
{
    internal const string Path = "/launcher/roll/v1";
    internal const string Preferences = "launcher-roll";
    internal const string Clock = "clock";
    internal const string Layout = "layout";
    internal const string Date = "date";
    internal const string AmbientDate = "ambient_date";
    internal const string Accent = "accent";
    internal const string Activity = "activity";
    internal const string DownloadCount = "download_count";
    internal const string DownloadPercent = "download_percent";
    internal const string Deadline = "deadline";
    internal const string SampledAt = "sampled_at";
    internal static string Mode(string? value, params string[] choices) =>
        value is not null && choices.Contains(value, StringComparer.Ordinal) ? value : "watch";
    // ShortText is intended for <=7 characters. Keep the wire value fixed at four.
    internal static string Style(string clock, string layout, string date, string ambient) =>
        $"{(clock == "12" ? '1' : clock == "24" ? '2' : 'w')}{(layout == "daily" ? 'd' : layout == "pure" ? 'p' : 'w')}{Flag(date)}{Flag(ambient)}";
    private static char Flag(string value) => value == "on" ? 'y' : value == "off" ? 'n' : 'w';
}
