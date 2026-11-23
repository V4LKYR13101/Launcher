using System.Text.Json;
using Android.Content;

namespace LauncherApp.Services;

internal static class DownloadHistory
{
    internal sealed record Entry(string Name, string MimeType, string Location, bool IsLegacyPath, DateTimeOffset CompletedAt);
    private const string EntriesKey = "history";
    private static readonly object Gate = new();

    // The service raises this on the main thread after saving the entry.
    internal static event Action? Changed;

    internal static ISharedPreferences Preferences(Context context) =>
        context.GetSharedPreferences("downloader", FileCreationMode.Private)!;

    internal static Entry[] Read(Context context)
    {
        string json = Preferences(context).GetString(EntriesKey, "[]") ?? "[]";
        try { return JsonSerializer.Deserialize<Entry[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    internal static void Record(Context context, Entry entry)
    {
        lock (Gate)
        {
            var entries = Read(context).Where(previous => previous.Location != entry.Location);
            Preferences(context).Edit()!.PutString(EntriesKey, JsonSerializer.Serialize(entries.Prepend(entry)))!.Apply();
        }
        Changed?.Invoke();
    }

    internal static bool IsSavedLegacyFile(Context context, string path) =>
        !OperatingSystem.IsAndroidVersionAtLeast(29) && Read(context).Any(entry => entry.IsLegacyPath && entry.Location == path);
}
