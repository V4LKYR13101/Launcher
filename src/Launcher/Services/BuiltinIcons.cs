using Android.Content;
using Android.Graphics;
using Launcher.Core;
using LauncherApp.UI;
namespace LauncherApp.Services;

public static class BuiltinIcons
{
    public static readonly IconPack[] Libraries = [
        new("builtin:material-outlined","Google Material · Outlined"),new("builtin:material-rounded","Google Material · Rounded"),new("builtin:material-filled","Google Material · Filled"),new("builtin:material-sharp","Google Material · Sharp"),
        new("builtin:phosphor-light","Phosphor · Light"),new("builtin:phosphor-thin","Phosphor · Thin"),new("builtin:phosphor-regular","Phosphor · Regular"),new("builtin:phosphor-bold","Phosphor · Bold"),new("builtin:phosphor-fill","Phosphor · Fill"),new("builtin:bootstrap","Bootstrap")];
    private static readonly Dictionary<string, Dictionary<string, string>> Catalogs = [];
    private static readonly Dictionary<string, Typeface> Fonts = [];
    public static bool IsBuiltin(string id) => Libraries.Any(l => l.Package == id);
    public static Dictionary<string, string> Map(Context context, string id)
    {
        if (!IsBuiltin(id))
            return [];
        if (Catalogs.TryGetValue(id, out var cached))
            return cached;
        using var reader = new StreamReader(context.Assets!.Open("icon-libraries/" + id[8..] + ".tsv"));
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var pair = line.Split('\t');
            if (pair.Length == 2 && int.TryParse(pair[1], System.Globalization.NumberStyles.HexNumber, null, out var code))
                result[pair[0]] = char.ConvertFromUtf32(code);
        }
        Catalogs[id] = result;
        return result;
    }
    public static FontIconDrawable? Get(Context context, string id, string name)
    {
        if (!Map(context, id).TryGetValue(name, out var glyph))
            return null;
        if (!Fonts.TryGetValue(id, out var font))
            Fonts[id] = font = Typeface.CreateFromAsset(context.Assets, "icon-libraries/" + id[8..] + ".ttf")!;
        return new(font, glyph);
    }
    public static string? Match(Context context, string id, AppEntry app)
    {
        var names = Map(context, id);
        // Brand matches are exact. Unmatched apps retain their own identity.
        string[] candidates = app.PackageName switch
        {
            "com.android.chrome" => ["google-chrome-logo", "browser-chrome"],
            "org.mozilla.firefox" => ["firefox-logo", "browser-firefox"],
            "com.whatsapp" => ["whatsapp-logo", "whatsapp"],
            "org.telegram.messenger" => ["telegram-logo", "telegram"],
            "com.spotify.music" => ["spotify-logo", "spotify"],
            "com.google.android.youtube" => ["youtube-logo", "youtube"],
            "com.instagram.android" => ["instagram-logo", "instagram"],
            "com.facebook.katana" => ["facebook-logo", "facebook"],
            "com.linkedin.android" => ["linkedin-logo", "linkedin"],
            "com.twitter.android" => ["x-logo", "twitter-x", "twitter-logo"],
            "com.openai.chatgpt" => ["open-ai-logo", "openai"],
            "com.google.android.googlequicksearchbox" => ["google-logo", "google"],
            "com.microsoft.teams" => ["microsoft-teams-logo", "microsoft-teams"],
            _ => []
        };
        foreach (string candidate in candidates)
        if (names.ContainsKey(candidate))
            return candidate;
        // Generic utility glyphs only match the corresponding utility's exact name.
        string label = app.Label.Trim().ToLowerInvariant();
        candidates = label switch
        {
            "camera" => ["camera_alt", "camera", "camera-fill"],
            "calculator" => ["calculate", "calculator"],
            "calendar" => ["calendar_month", "calendar", "calendar3"],
            "clock" => ["schedule", "clock"],
            "contacts" => ["contacts", "address-book", "person-lines-fill"],
            "phone" => ["call", "phone", "telephone"],
            "settings" => ["settings", "gear", "gear-six"],
            "files" or "my files" => ["folder_open", "folder-open", "folder"],
            _ => []
        };
        return candidates.FirstOrDefault(names.ContainsKey);
    }
}
