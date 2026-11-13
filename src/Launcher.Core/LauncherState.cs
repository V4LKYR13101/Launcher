using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launcher.Core;

public sealed class Drawer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New drawer";
    public bool Expanded { get; set; }
    public string Icon { get; set; } = "";
}

public sealed class AppRule
{
    public string DrawerId { get; set; } = "other";
    public string? Label { get; set; }
    public bool Hidden { get; set; }
    public bool Pinned { get; set; }
    public bool Highlight { get; set; }
    public bool Wide { get; set; }
    public bool BiometricLocked { get; set; }
    public bool OpenInGrayscale { get; set; }
    public int Order { get; set; }
    public int DailyGoalMinutes { get; set; }
}

public sealed class WidgetSlot
{
    public int Id { get; set; }
    public int Height { get; set; } = 220;
    public bool AutoHeight { get; set; } = true;
    public bool Collapsed { get; set; }
}

public sealed class LauncherState
{
    public int Version { get; set; } = 1;
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "#E4FF00";
    public int Columns { get; set; } = 4;
    public int TileHeight { get; set; } = 112;
    public int CornerRadius { get; set; } = 12;
    public bool CenterHeadings { get; set; }
    public bool UseWallpaper { get; set; }
    public string Alignment { get; set; } = "Top";
    public string IconStyle { get; set; } = "Monochrome";
    public bool MinimalStatusBar { get; set; } = true;
    public string StatusPlacement { get; set; } = "Top";
    public bool ShowMediaCard { get; set; } = true;
    public bool ConversationMessagesOnly { get; set; }
    public List<string> ConversationExcludedApps { get; set; } = new();
    public bool ShowLabels { get; set; } = true;
    public bool ShowUsage { get; set; } = true;
    public bool ShowNotificationBadges { get; set; } = true;
    public bool Focus { get; set; }
    public long FocusEndsAt { get; set; }
    public string Note { get; set; } = "";
    public bool NotesVisible { get; set; } = true;
    public bool CalendarVisible { get; set; } = true;
    public bool CalculatorVisible { get; set; } = true;
    public bool FocusCardVisible { get; set; } = true;
    public WeatherSettings Weather { get; set; } = new();
    public Dictionary<string, bool> CollapsedCards { get; set; } = new();
    public List<Drawer> Drawers { get; set; } = DefaultDrawers();
    public Dictionary<string, AppRule> Apps { get; set; } = new();
    public List<WidgetSlot> Widgets { get; set; } = new();

    public static List<Drawer> DefaultDrawers() =>
    [
        new() { Id = "essentials", Name = "Essentials", Expanded = true },
        new() { Id = "creativity", Name = "Creativity" },
        new() { Id = "messaging", Name = "Messaging", Expanded = true },
        new() { Id = "productivity", Name = "Productivity" },
        new() { Id = "lifestyle", Name = "Lifestyle" },
        new() { Id = "entertainment", Name = "Entertainment" },
        new() { Id = "social", Name = "Social" },
        new() { Id = "finance", Name = "Finance" },
        new() { Id = "other", Name = "App box" }
    ];

    public void ApplyPreset(string preset)
    {
        Columns = preset == "Studio" ? 5 : 4;
        CenterHeadings = preset == "Studio";
        UseWallpaper = preset == "Studio";
        TileHeight = preset == "Studio" ? 100 : 112;
        CornerRadius = 12;
        Theme = "Dark";
        IconStyle = "Monochrome";
    }

    public void Normalize()
    {
        if (Version != 1) throw new InvalidDataException("This backup uses an unsupported version.");
        Columns = Math.Clamp(Columns, 3, 6);
        TileHeight = Math.Clamp(TileHeight, 84, 156);
        CornerRadius = Math.Clamp(CornerRadius, 0, 28);
        if (Theme is not ("Dark" or "Light" or "Sun")) Theme = "Dark";
        if (Alignment is not ("Top" or "Center" or "Bottom")) Alignment = "Top";
        if(StatusPlacement is not ("Top" or "Safe"))StatusPlacement="Top";
        ConversationExcludedApps=(ConversationExcludedApps??[]).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct().Take(3000).ToList();
        if (IconStyle is not ("Monochrome" or "Grayscale" or "Original")) IconStyle = "Monochrome";
        if (Accent is null || !System.Text.RegularExpressions.Regex.IsMatch(Accent, "^#[0-9a-fA-F]{6}$")) Accent = "#E4FF00";
        Note = (Note ?? "")[..Math.Min(Note?.Length ?? 0, 20_000)];
        Drawers ??= DefaultDrawers();
        Drawers = Drawers.Where(d => d is not null && !string.IsNullOrWhiteSpace(d.Id)).Take(40).DistinctBy(d => d.Id).ToList();
        foreach (var d in Drawers)
        {
            d.Name = CleanName(d.Name, "Drawer");
            if(!DrawerIcons.All.Contains(d.Icon)) d.Icon=DrawerIcons.ForDrawer(d.Id);
        }
        Weather ??= new();
        Weather.City=CleanName(Weather.City,"");
        if(Weather.RefreshMinutes is not (0 or 30 or 60 or 180 or 360))Weather.RefreshMinutes=30;
        if(Weather.Latitude is < -90 or > 90 || Weather.Longitude is < -180 or > 180)
        {Weather.Latitude=null;Weather.Longitude=null;Weather.UpdatedAt=0;}
        if (!Drawers.Any(d => d.Id == "other")) Drawers.Add(new() { Id = "other", Name = "App box" });
        Apps ??= new();
        if (Apps.Count > 3000) throw new InvalidDataException("This backup contains too many apps.");
        foreach (var key in Apps.Where(p => p.Value is null).Select(p => p.Key).ToArray()) Apps.Remove(key);
        foreach (var rule in Apps.Values)
        {
            if (!Drawers.Any(d => d.Id == rule.DrawerId)) rule.DrawerId = "other";
            rule.Label = string.IsNullOrWhiteSpace(rule.Label) ? null : CleanName(rule.Label, "App");
            rule.DailyGoalMinutes = Math.Clamp(rule.DailyGoalMinutes, 0, 1440);
        }
        Widgets ??= new();
        CollapsedCards ??= new();
        Widgets = Widgets.Where(w => w is not null && w.Id > 0).DistinctBy(w => w.Id).Take(20).ToList();
        foreach (var w in Widgets) w.Height = Math.Clamp(w.Height, 64, 600);
    }

    public static string CleanName(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim()[..Math.Min(value.Trim().Length, 48)];

    public void RemoveDrawer(string id)
    {
        if (id == "other") return;
        Drawers.RemoveAll(d => d.Id == id);
        foreach (var r in Apps.Values.Where(r => r.DrawerId == id)) r.DrawerId = "other";
    }

    public string Serialize() => JsonSerializer.Serialize(this, StateJsonContext.Default.LauncherState);
    public static LauncherState Deserialize(string json)
    {
        if (json.Length > 2_000_000) throw new InvalidDataException("Backup exceeds the 2 MB limit.");
        var state = JsonSerializer.Deserialize(json, StateJsonContext.Default.LauncherState) ?? throw new InvalidDataException("Empty backup.");
        state.Normalize();
        return state;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LauncherState))]
internal partial class StateJsonContext : JsonSerializerContext;

public sealed record AppEntry(string Id, string PackageName, string ActivityName, string Label, int Category = -1);

public static class AppOrganization
{
    public static string GuessDrawer(AppEntry app)
    {
        var s = (app.PackageName + " " + app.Label).ToLowerInvariant();
        bool Has(params string[] terms) => terms.Any(t => System.Text.RegularExpressions.Regex.IsMatch(s,
            @"(?<![a-z])" + System.Text.RegularExpressions.Regex.Escape(t) + @"(?![a-z])"));
        if (Has("dialer", "contacts", "camera", "chrome", "browser", "settings", "phone")) return "essentials";
        if (Has("whatsapp", "telegram", "signal", "message", "messages", "messaging", "gmail", "outlook", "mail", "skype", "w4b")) return "messaging";
        if (Has("instagram", "facebook", "reddit", "twitter", "tiktok", "linkedin", "mastodon")) return "social";
        if (Has("bank", "banking", "wallet", "paypal", "finance", "invest", "money", "fnb", "capitec")) return "finance";
        if (Has("photo", "photos", "gallery", "paint", "design", "canva", "lightroom", "editor")) return "creativity";
        if (Has("music", "youtube", "netflix", "spotify", "podcast", "video", "game")) return "entertainment";
        if (Has("map", "maps", "uber", "fitness", "health", "weather", "travel", "shop", "shopping")) return "lifestyle";
        if (Has("calendar", "clock", "calculator", "document", "drive", "files", "notes", "todo", "office")) return "productivity";
        return app.Category switch { 0 or 1 or 2 => "entertainment", 3 => "creativity", 4 => "social", 5 => "lifestyle", 6 => "productivity", _ => "other" };
    }

    public static void Reconcile(LauncherState state, IEnumerable<AppEntry> apps)
    {
        int next = state.Apps.Values.Select(r => r.Order).DefaultIfEmpty(0).Max() + 1;
        foreach (var app in apps)
            if (!state.Apps.ContainsKey(app.Id))
            {
                string drawer = GuessDrawer(app);
                if (!state.Drawers.Any(d => d.Id == drawer)) drawer = "other";
                var siblings=state.Apps.Where(p=>p.Key.StartsWith(app.PackageName+"/",StringComparison.Ordinal)).Select(p=>p.Value).ToArray();
                state.Apps[app.Id] = new() { DrawerId = drawer, Order = next++,BiometricLocked=siblings.Any(r=>r.BiometricLocked),OpenInGrayscale=siblings.Any(r=>r.OpenInGrayscale) };
            }
    }

    public static IEnumerable<AppEntry> Visible(LauncherState state, IEnumerable<AppEntry> apps, string query = "") =>
        apps.Where(a => state.Apps.TryGetValue(a.Id, out var r) && !r.Hidden
            && (DisplayName(state, a).Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || a.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || a.PackageName.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(a => state.Apps[a.Id].Pinned).ThenBy(a => state.Apps[a.Id].Order).ThenBy(a => a.Label);

    public static string DisplayName(LauncherState state, AppEntry app) => state.Apps[app.Id].Label ?? app.Label;
    public static string FormatDuration(long milliseconds)
    {
        var minutes = Math.Max(0, milliseconds / 60000);
        return minutes >= 60 ? $"{minutes / 60}h {minutes % 60:00}m" : $"{minutes}m";
    }
}
