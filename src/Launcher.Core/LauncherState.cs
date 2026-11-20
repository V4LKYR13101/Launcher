using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launcher.Core;

public sealed class Drawer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New drawer";
    public bool Expanded
    {
        get; set;
    }
    public string Icon { get; set; } = "";
    public string IconPackage { get; set; } = "";
    public string IconResource { get; set; } = "";
}

public sealed class AppRule
{
    public string DrawerId { get; set; } = "other";
    public string? Label
    {
        get; set;
    }
    public string SearchAliases { get; set; } = "";
    public bool Hidden
    {
        get; set;
    }
    public bool Pinned
    {
        get; set;
    }
    public bool Highlight
    {
        get; set;
    }
    public bool Wide
    {
        get; set;
    }
    public bool BiometricLocked
    {
        get; set;
    }
    public bool OpenInGrayscale
    {
        get; set;
    }
    public bool MuteBadge
    {
        get; set;
    }
    public string TileStyle { get; set; } = "Default";
    public bool UseOriginalIcon
    {
        get; set;
    }
    public string IconPackage { get; set; } = "";
    public string IconResource { get; set; } = "";
    public int Order
    {
        get; set;
    }
    public int DailyGoalMinutes
    {
        get; set;
    }
}

public sealed class WidgetSlot
{
    public int Id
    {
        get; set;
    }
    public int Height { get; set; } = 220;
    public bool AutoHeight { get; set; } = true;
    public bool Collapsed
    {
        get; set;
    }
}

public sealed class LauncherState
{
    public const int MaximumJsonCharacters = 32_000_000;
    public int Version { get; set; } = 1;
    public string Theme { get; set; } = "Dark";
    public string HapticFeedback { get; set; } = "Subtle";
    public bool SystemPanelEnabled
    {
        get; set;
    }
    public bool SystemPanelStatus
    {
        get; set;
    }
    public bool SystemPanelBelowStatus { get; set; } = true;
    public bool SystemPanelAnimations { get; set; } = true;
    public string SystemPanelSide { get; set; } = "Right";
    public int SystemPanelOffset { get; set; } = 48;
    public string SystemPanelTheme { get; set; } = "Follow Launcher";
    public int SystemPanelOpacity { get; set; } = 100;
    public List<string> SystemPanelControls { get; set; } = ["Wi-Fi", "Bluetooth", "Sound", "Display", "Battery", "Settings"];
    public bool AutomaticLightTheme
    {
        get; set;
    }
    public bool FollowSystemTheme
    {
        get; set;
    }
    public bool ShowHomeNavigation { get; set; } = true;
    public List<string> HomeNavigationPages { get; set; } = ["Root", "Tiles", "Conversations"];
    public bool ShowPrivateSpace { get; set; } = true;
    public bool HidePrivateSpaceWhenLocked
    {
        get; set;
    }
    public bool HideProtectedPreviews { get; set; } = true;
    public bool SetupCompleted
    {
        get; set;
    }
    public List<PinnedAppShortcut> Shortcuts { get; set; } = [];
    public List<SavedArticle> SavedArticles { get; set; } = [];
    public List<string> ReadArticleUrls { get; set; } = [];
    public bool FeedUnreadOnly
    {
        get; set;
    }
    public double LightThemeLux { get; set; } = 1000;
    public double DarkThemeLux { get; set; } = 300;
    public int LightThemeDelaySeconds { get; set; } = 5;
    public string Accent { get; set; } = "#E4FF00";
    public int Columns { get; set; } = 4;
    public int TileHeight { get; set; } = 112;
    public int TileIconScale { get; set; } = 100;
    public int CornerRadius { get; set; } = 12;
    public bool CenterHeadings
    {
        get; set;
    }
    public bool DragDrawerHeadings
    {
        get; set;
    }
    public bool UseWallpaper
    {
        get; set;
    }
    public int WallpaperDim { get; set; } = 65;
    public string Alignment { get; set; } = "Top";
    public string IconStyle { get; set; } = "Monochrome";
    public bool MinimalStatusBar { get; set; } = true;
    public string StatusPlacement { get; set; } = "Top";
    public bool ShowMediaCard { get; set; } = true;
    public bool ConversationMessagesOnly
    {
        get; set;
    }
    public List<string> ConversationExcludedApps { get; set; } = new();
    public bool ShowLabels { get; set; } = true;
    public bool ShowUsage { get; set; } = true;
    public bool ShowNotificationBadges { get; set; } = true;
    public bool Focus
    {
        get; set;
    }
    public long FocusEndsAt
    {
        get; set;
    }
    public string Note { get; set; } = "";
    public List<WorkspaceNote> Notes { get; set; } = [];
    public List<string> DeletedNoteIds { get; set; } = [];
    public List<WorkspaceTimer> Timers { get; set; } = [];
    public List<WorkspaceReminder> Reminders { get; set; } = [];
    public List<WorkspaceCard> ExtraCards { get; set; } = [];
    public List<RssSource>? RssSources
    {
        get; set;
    }
    public int FeedArticleCount { get; set; } = 12;
    public bool FeedImages { get; set; } = true;
    public List<TravelTicket> Tickets { get; set; } = [];
    public List<string> RootOrder { get; set; } = ["calendar", "weather", "media", "focus", "notes", "timers", "calculator", "tickets"];
    public List<string> RootPinned { get; set; } = [];
    public List<string> RootHidden { get; set; } = [];
    public string IconPack { get; set; } = "";
    public bool KeepMessageHistory
    {
        get; set;
    }
    public bool NotesVisible { get; set; } = true;
    public bool CalendarVisible { get; set; } = true;
    public List<long> CalendarIds { get; set; } = [];
    public bool CalculatorVisible { get; set; } = true;
    public bool ResourcesVisible
    {
        get; set;
    }
    public bool PageTransitions { get; set; } = true;
    public bool LayeredInterface { get; set; } = true;
    public bool TiltDepth
    {
        get; set;
    }
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
        FollowSystemTheme = false;
        Columns = preset == "Studio" ? 5 : 4;
        CenterHeadings = preset == "Studio";
        UseWallpaper = preset == "Studio";
        TileHeight = preset == "Studio" ? 100 : 112;
        TileIconScale = 100;
        CornerRadius = 12;
        Theme = "Dark";
        IconStyle = "Monochrome";
    }

    public void Normalize()
    {
        if (Version != 1)
            throw new InvalidDataException("This backup uses an unsupported version.");
        SavedArticles = (SavedArticles ?? []).Where(a => a is not null && Uri.TryCreate(a.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0 && a.Url.Length <= 2048).DistinctBy(a => a.Url).Take(200).ToList();
        foreach (var article in SavedArticles)
        {
            article.Title = (article.Title ?? "Saved article")[..Math.Min(article.Title?.Length ?? 0, 300)];
            article.Detail = (article.Detail ?? "")[..Math.Min(article.Detail?.Length ?? 0, 500)];
        }
        ReadArticleUrls = (ReadArticleUrls ?? []).Where(s => !string.IsNullOrWhiteSpace(s) && s.Length <= 2048).Distinct().TakeLast(1000).ToList();
        Shortcuts = (Shortcuts ?? []).Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Package) && !string.IsNullOrWhiteSpace(s.ShortcutId)).DistinctBy(s => (s.Package, s.ShortcutId, s.ProfileSerial)).Take(200).ToList();
        foreach (var shortcut in Shortcuts)
        {
            shortcut.Label = CleanName(shortcut.Label, "Shortcut");
            shortcut.Package = shortcut.Package[..Math.Min(250, shortcut.Package.Length)];
            shortcut.ShortcutId = shortcut.ShortcutId[..Math.Min(1000, shortcut.ShortcutId.Length)];
        }
        Columns = Math.Clamp(Columns, 3, 6);
        CalendarIds = (CalendarIds ?? []).Where(id => id >= 0).Distinct().Take(100).ToList();
        FeedArticleCount = Math.Clamp(FeedArticleCount, 1, 50);
        TileHeight = Math.Clamp(TileHeight, 84, 156);
        TileIconScale = Math.Clamp(TileIconScale, 60, 150);
        CornerRadius = Math.Clamp(CornerRadius, 0, 28);
        WallpaperDim = Math.Clamp(WallpaperDim, 20, 90);
        if (Theme is not ("Dark" or "Light" or "Sun"))
            Theme = "Dark";
        SystemPanelOffset = Math.Clamp(SystemPanelOffset, 16, 160);
        SystemPanelOpacity = Math.Clamp(SystemPanelOpacity, 90, 100);
        if (SystemPanelSide is not ("Left" or "Right"))
            SystemPanelSide = "Right";
        if (SystemPanelTheme is not ("Follow Launcher" or "Dark" or "Light"))
            SystemPanelTheme = "Follow Launcher";
        string[] panelControls = ["Wi-Fi", "Bluetooth", "Sound", "Display", "Battery", "Settings"];
        SystemPanelControls = (SystemPanelControls ?? []).Where(panelControls.Contains).Distinct().Concat(panelControls).Distinct().ToList();
        if (HapticFeedback is not ("Off" or "Subtle" or "Crisp"))
            HapticFeedback = "Subtle";
        LightThemeLux = double.IsFinite(LightThemeLux) ? Math.Clamp(LightThemeLux, 1, 200000) : 1000;
        DarkThemeLux = double.IsFinite(DarkThemeLux) ? Math.Clamp(DarkThemeLux, 0, LightThemeLux - 1) : Math.Min(300, LightThemeLux - 1);
        LightThemeDelaySeconds = Math.Clamp(LightThemeDelaySeconds, 3, 30);
        HomeNavigationPages = (HomeNavigationPages ?? ["Root", "Tiles", "Conversations"]).Where(value => value is "Root" or "Tiles" or "Conversations").Distinct().ToList();
        if (Alignment is not ("Top" or "Center" or "Bottom"))
            Alignment = "Top";
        if (StatusPlacement is not ("Top" or "Safe"))
            StatusPlacement = "Top";
        ConversationExcludedApps = (ConversationExcludedApps ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Take(3000).ToList();
        if (IconStyle is not ("Monochrome" or "Grayscale" or "Original"))
            IconStyle = "Monochrome";
        if (Accent is null || !System.Text.RegularExpressions.Regex.IsMatch(Accent, "^#[0-9a-fA-F]{6}$"))
            Accent = "#E4FF00";
        Note = (Note ?? "")[..Math.Min(Note?.Length ?? 0, 20_000)];
        Notes = (Notes ?? []).Where(n => n is not null && !string.IsNullOrWhiteSpace(n.Id)).DistinctBy(n => n.Id).Take(200).ToList();
        DeletedNoteIds = (DeletedNoteIds ?? []).Distinct().TakeLast(1000).ToList();
        Notes.RemoveAll(n => DeletedNoteIds.Contains(n.Id));
        if (Notes.Count == 0 && !string.IsNullOrEmpty(Note))
        {
            Notes.Add(new()
            {
                Title = "Notes",
                Text = Note
            });
            Note = "";
        }
        foreach (var n in Notes)
        {
            n.Title = CleanName(n.Title, "Note");
            n.Text = (n.Text ?? "")[..Math.Min(n.Text?.Length ?? 0, 20000)];
        }
        Timers = (Timers ?? []).Where(t => t is not null && !string.IsNullOrWhiteSpace(t.Id)).DistinctBy(t => t.Id).Take(20).ToList();
        Reminders = (Reminders ?? []).Where(r => r is not null && !string.IsNullOrWhiteSpace(r.Id)).DistinctBy(r => r.Id).Take(200).ToList();
        foreach (var reminder in Reminders)
        {
            reminder.Title = CleanName(reminder.Title, "Reminder");
            reminder.DueAt = Math.Clamp(reminder.DueAt, 0, DateTimeOffset.UtcNow.AddYears(10).ToUnixTimeMilliseconds());
            if (reminder.RepeatDays is not (0 or 1 or 7))
                reminder.RepeatDays = 0;
        }
        foreach (var t in Timers)
        {
            t.Title = CleanName(t.Title, "Timer");
            t.EndsAt = Math.Clamp(t.EndsAt, 0, DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeMilliseconds());
            t.DurationMs = Math.Clamp(t.DurationMs, 1000, 86400000);
            t.RemainingMs = Math.Clamp(t.RemainingMs, 0, t.DurationMs);
        }
        ExtraCards = (ExtraCards ?? []).Where(c => c is not null && c.Kind is "rss" or "web" or "youtube").Take(40).ToList();
        foreach (var c in ExtraCards)
        {
            c.Id = CleanName(c.Id, Guid.NewGuid().ToString("N"));
            c.Title = CleanName(c.Title, c.Kind);
            c.Query = (c.Query ?? "")[..Math.Min(c.Query?.Length ?? 0, 2000)];
        }
        Tickets = (Tickets ?? []).Where(t => t is not null).Take(100).ToList();
        foreach (var t in Tickets)
        {
            t.Title = CleanName(t.Title, "Journey");
            t.Route = CleanName(t.Route, "");
            t.Reference = CleanName(t.Reference, "");
            t.Date = CleanName(t.Date, "");
            t.Attachment = (t.Attachment ?? "")[..Math.Min(t.Attachment?.Length ?? 0, 2000)];
        }
        RootOrder = (RootOrder ?? []).Where(s => !string.IsNullOrWhiteSpace(s) && s.Length <= 100).Distinct().Take(512).ToList();
        if (!RootOrder.Contains("overview"))
            RootOrder.Insert(0, "overview");
        RootPinned = (RootPinned ?? []).Distinct().Take(512).ToList();
        RootHidden = (RootHidden ?? []).Distinct().Take(512).ToList();
        if (RssSources is null)
        {
            var oldFeeds = ExtraCards.Where(c => c.Kind == "rss").ToArray();
            RssSources = RssSource.Defaults();
            foreach (var old in oldFeeds)
            {
                var match = RssSources.FirstOrDefault(s => s.Url == old.Query);
                if (match is not null)
                    match.Enabled = true;
                else
                    RssSources.Add(new()
                    {
                        Id = old.Id,
                        Name = CleanName(old.Title, "Feed"),
                        Url = old.Query,
                        Enabled = true
                    });
                if (RootPinned.Contains(old.Id) && !RootPinned.Contains("feeds"))
                    RootPinned.Add("feeds");
                RootOrder = RootOrder.Select(id => id == old.Id ? "feeds" : id).Distinct().ToList();
            }
        }
        ExtraCards.RemoveAll(c => c.Kind == "rss");
        RssSources = RssSources.Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Url)).DistinctBy(s => s.Id).Take(20).ToList();
        foreach (var source in RssSources)
        {
            source.Id = CleanName(source.Id, Guid.NewGuid().ToString("N"));
            source.Name = CleanName(source.Name, "Feed");
            source.Url = source.Url.Trim()[..Math.Min(source.Url.Trim().Length, 2048)];
        }
        Drawers ??= DefaultDrawers();
        Drawers = Drawers.Where(d => d is not null && !string.IsNullOrWhiteSpace(d.Id)).Take(40).DistinctBy(d => d.Id).ToList();
        foreach (var d in Drawers)
        {
            d.Name = CleanName(d.Name, "Drawer");
            d.IconPackage ??= "";
            d.IconResource ??= "";
            if (!DrawerIcons.All.Contains(d.Icon))
                d.Icon = DrawerIcons.ForDrawer(d.Id);
        }
        Weather ??= new();
        Weather.Hours = (Weather.Hours ?? []).Where(h => h is not null).Take(168).ToList();
        Weather.Days = (Weather.Days ?? []).Where(d => d is not null).Take(7).ToList();
        Weather.City = CleanName(Weather.City, "");
        if (Weather.RefreshMinutes is not (0 or 30 or 60 or 180 or 360))
            Weather.RefreshMinutes = 30;
        if (Weather.Latitude is < -90 or > 90 || Weather.Longitude is < -180 or > 180)
        {
            Weather.Latitude = null;
            Weather.Longitude = null;
            Weather.UpdatedAt = 0;
        }
        if (!Drawers.Any(d => d.Id == "other"))
            Drawers.Add(new()
            {
                Id = "other",
                Name = "App box"
            });
        Apps ??= new();
        if (Apps.Count > 3000)
            throw new InvalidDataException("This backup contains too many apps.");
        foreach (var key in Apps.Where(p => p.Value is null).Select(p => p.Key).ToArray())
            Apps.Remove(key);
        foreach (var rule in Apps.Values)
        {
            if (!Drawers.Any(d => d.Id == rule.DrawerId))
                rule.DrawerId = "other";
            rule.Label = string.IsNullOrWhiteSpace(rule.Label) ? null : CleanName(rule.Label, "App");
            rule.SearchAliases = (rule.SearchAliases ?? "")[..Math.Min(rule.SearchAliases?.Length ?? 0, 240)];
            rule.DailyGoalMinutes = Math.Clamp(rule.DailyGoalMinutes, 0, 1440);
            if (rule.TileStyle is not ("Default" or "Accent" or "Light" or "Dark" or "Outline"))
                rule.TileStyle = "Default";
            rule.IconPackage ??= "";
            rule.IconResource ??= "";
        }
        Widgets ??= new();
        CollapsedCards ??= new();
        Widgets = Widgets.Where(w => w is not null && w.Id > 0).DistinctBy(w => w.Id).Take(20).ToList();
        foreach (var w in Widgets)
            w.Height = Math.Clamp(w.Height, 64, 600);
    }

    public static string CleanName(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim()[..Math.Min(value.Trim().Length, 48)];

    public void RemoveDrawer(string id)
    {
        if (id == "other")
            return;
        Drawers.RemoveAll(d => d.Id == id);
        foreach (var r in Apps.Values.Where(r => r.DrawerId == id))
            r.DrawerId = "other";
    }

    public string Serialize()
    {
        string json = JsonSerializer.Serialize(this, StateJsonContext.Default.LauncherState);
        if (json.Length > MaximumJsonCharacters)
            throw new InvalidDataException("This collection is too large to save. Export or shorten some notes first.");
        return json;
    }
    public static LauncherState Deserialize(string json)
    {
        if (json.Length > MaximumJsonCharacters)
            throw new InvalidDataException("This backup exceeds the supported size.");
        var state = JsonSerializer.Deserialize(json, StateJsonContext.Default.LauncherState) ?? throw new InvalidDataException("Empty backup.");
        state.Normalize();
        return state;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LauncherState))]
internal partial class StateJsonContext : JsonSerializerContext;

public sealed class PinnedAppShortcut
{
    public string Package { get; set; } = "";
    public string ShortcutId { get; set; } = "";
    public string Label { get; set; } = "Shortcut";
    public long ProfileSerial { get; set; } = -1;
}
public sealed class SavedArticle
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public long SavedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
public sealed record AppEntry(string Id, string PackageName, string ActivityName, string Label, int Category = -1, long ProfileSerial = -1, bool PrivateProfile = false, string ShortcutId = "");

public static class AppOrganization
{
    public static string GuessDrawer(AppEntry app)
    {
        var s = (app.PackageName + " " + app.Label).ToLowerInvariant();
        bool Has(params string[] terms) => terms.Any(t => System.Text.RegularExpressions.Regex.IsMatch(s,
            @"(?<![a-z])" + System.Text.RegularExpressions.Regex.Escape(t) + @"(?![a-z])"));
        if (Has("dialer", "contacts", "camera", "chrome", "browser", "settings", "phone"))
            return "essentials";
        if (Has("whatsapp", "telegram", "signal", "message", "messages", "messaging", "gmail", "outlook", "mail", "skype", "w4b"))
            return "messaging";
        if (Has("instagram", "facebook", "reddit", "twitter", "tiktok", "linkedin", "mastodon"))
            return "social";
        if (Has("bank", "banking", "wallet", "paypal", "finance", "invest", "money", "fnb", "capitec"))
            return "finance";
        if (Has("photo", "photos", "gallery", "paint", "design", "canva", "lightroom", "editor"))
            return "creativity";
        if (Has("music", "youtube", "netflix", "spotify", "podcast", "video", "game"))
            return "entertainment";
        if (Has("map", "maps", "uber", "fitness", "health", "weather", "travel", "shop", "shopping"))
            return "lifestyle";
        if (Has("calendar", "clock", "calculator", "document", "drive", "files", "notes", "todo", "office"))
            return "productivity";
        return app.Category switch
        {
            0 or 1 or 2 => "entertainment",
            3 => "creativity",
            4 => "social",
            5 => "lifestyle",
            6 => "productivity",
            _ => "other"
        };
    }

    public static void Reconcile(LauncherState state, IEnumerable<AppEntry> apps)
    {
        int next = state.Apps.Values.Select(r => r.Order).DefaultIfEmpty(0).Max() + 1;
        foreach (var app in apps)
            if (!state.Apps.ContainsKey(app.Id))
            {
                string drawer = GuessDrawer(app);
                if (!state.Drawers.Any(d => d.Id == drawer))
                    drawer = "other";
                var siblings = state.Apps.Where(p => p.Key.StartsWith(app.PackageName + "/", StringComparison.Ordinal)).Select(p => p.Value).ToArray();
                state.Apps[app.Id] = new()
                {
                    DrawerId = drawer,
                    Order = next++,
                    BiometricLocked = siblings.Any(r => r.BiometricLocked),
                    OpenInGrayscale = siblings.Any(r => r.OpenInGrayscale)
                };
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
        if (milliseconds > 0 && milliseconds < 60000)
            return milliseconds < 1000 ? "<1s" : $"{milliseconds / 1000}s";
        var minutes = Math.Max(0, milliseconds / 60000);
        return minutes >= 60 ? $"{minutes / 60}h {minutes % 60:00}m" : $"{minutes}m";
    }
}
