using Android.Content;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private readonly Dictionary<string, (CardLink[] Links, string? Error, long At)> onlineCardCache = [];
    private readonly HashSet<string> onlineLoading = [];
    private string IntegrationKey(string name) => GetSharedPreferences("integrations", FileCreationMode.Private)!.GetString(name, "") ?? "";
    private void RenderIntegrations()
    {
        Title("Online cards");
        SettingRow(body, "RSS feeds", $"{Sources.Count(s => s.Enabled)} enabled sources · one news card", ManageFeeds, "rss");
        RootActions(body, ("+ Add RSS feed", () => EditFeed()));
        Gap(body, 16);
        var label = Text("SEARCH PROVIDERS", 10, true, palette.Muted);
        label.LetterSpacing = .10f;
        body.AddView(label, Match());
        Paragraph("Keys enable inline search results. RSS feeds do not need a key. Keys stay on this phone and are excluded from layout backups.");
        foreach (var entry in new[] { ("brave", "Brave Search API key"), ("youtube", "YouTube Data API key") })
            SettingRow(body, entry.Item2, IntegrationKey(entry.Item1).Length > 0 ? "Configured" : "Not configured", () => Prompt(entry.Item2, "", value => { GetSharedPreferences("integrations", FileCreationMode.Private)!.Edit()!.PutString(entry.Item1, value.Trim())!.Apply(); onlineCardCache.Clear(); RenderBody(); }), "settings");
        body.AddView(Button("Brave API account", () => OpenUrl("https://api.search.brave.com/")), Match());
        body.AddView(Button("YouTube API setup", () => OpenUrl("https://developers.google.com/youtube/v3/getting-started")), Match());
    }
    private void RenderRootCommands()
    {
        if (query.Trim().Length > 0)
        {
            Title("Search your workspace");
            foreach (var result in LocalSearch.Find(state, query))
            {
                var item = result;
                SettingRow(body, item.Title, item.Kind + " · " + item.Preview, () => OpenSearchHit(new(null, item)), item.Kind == "Journey" ? "file" : "note");
            }
            var command = RootCommand.Parse(query);
            if (command.Kind.Length > 0)
                body.AddView(Button("Run: " + query, () => ExecuteRootCommand(query), true), Match());
            else if (LocalSearch.Find(state, query).Length == 0)
                Paragraph("No saved content matches. Search Apps to include installed apps.");
            return;
        }
        Title("Add a card");
        Paragraph("Choose a card, or type a command below.");
        CardChoices(body, () => { HideKeyboard(); searching = false; query = ""; Render(); });
        if (query.Trim().Length > 0)
            body.AddView(Button("Run: " + query, () => ExecuteRootCommand(query), true), Match());
    }
    private void ExecuteRootCommand(string text)
    {
        var command = RootCommand.Parse(text);
        HideKeyboard();
        searching = false;
        switch (command.Kind)
        {
            case "note":
                CreateNote(command.Value);
                break;
            case "timer":
                if (command.Value.Length == 0)
                    Prompt("Timer duration", "10 min", CreateTimer);
                else
                    CreateTimer(command.Value);
                break;
            case "weather":
                state.RootHidden.Remove("weather");
                Save();
                if (command.Value.Length > 0)
                    SearchWeatherCity(command.Value);
                else if (state.Weather.City.Length == 0)
                    Navigate("Weather");
                else
                    Navigate("Root");
                break;
            case "media":
                state.RootHidden.Remove("media");
                state.ShowMediaCard = true;
                Save();
                Navigate("Root");
                break;
            case "resources":
                AddResourcesCard();
                break;
            case "ticket":
                EditTicket(new()
                {
                    Title = command.Value.Length > 0 ? command.Value : "Journey"
                }, true);
                break;
            case "rss":
                if (command.Value.Length > 0)
                    EditFeed(initialAddress: command.Value);
                else
                    ManageFeeds();
                break;
            case "web":
            case "youtube":
                if (command.Value.Length == 0)
                {
                    Prompt(command.Kind + " query", "", value => ExecuteRootCommand(command.Kind + " " + value));
                    return;
                }
                if (state.ExtraCards.Count >= 40)
                {
                    Toast("Remove a card before adding another.");
                    return;
                }
                var card = new WorkspaceCard { Kind = command.Kind, Query = command.Value, Title = command.Kind == "rss" ? "Feed" : command.Kind == "youtube" ? "YouTube" : "Web search" };
                state.ExtraCards.Add(card);
                state.RootOrder.Add(card.Id);
                Save();
                Navigate("Root");
                break;
        }
    }
    private void BuildOnlineCard(LinearLayout panel, WorkspaceCard card)
    {
        string key = card.Kind == "rss" ? "" : IntegrationKey(card.Kind == "web" ? "brave" : "youtube");
        if (card.Kind != "rss" && key.Length == 0)
        {
            Paragraph("Configure your API key for inline results, or open this search in your browser.", panel);
            RootActions(panel, ("Online card settings", () => Navigate("Integrations")));
            RootActions(panel, ("Open search", () => OpenUrl(card.Kind == "youtube" ? "https://www.youtube.com/results?search_query=" + System.Uri.EscapeDataString(card.Query) : "https://www.qwant.com/?q=" + System.Uri.EscapeDataString(card.Query))));
            return;
        }
        if (onlineCardCache.TryGetValue(card.Id, out var cached))
        {
            if (cached.Error is not null)
                Paragraph(cached.Error, panel);
            else if (cached.Links.Length == 0)
                Paragraph("No results returned.", panel);
            else
            foreach (var link in cached.Links)
                SettingRow(panel, link.Title, link.Detail, () => OpenUrl(link.Url), "link");
        }
        else
        {
            Paragraph("Loading…", panel);
            _ = LoadOnlineCard(card, key);
        }
        RootActions(panel, (onlineLoading.Contains(card.Id) ? "Refreshing…" : "Refresh", () => _ = LoadOnlineCard(card, key)));
    }
    private async Task LoadOnlineCard(WorkspaceCard card, string key)
    {
        if (!onlineLoading.Add(card.Id))
            return;
        try
        {
            onlineCardCache[card.Id] = (await Task.Run(() => OnlineCards.Fetch(card.Kind, card.Query, key)), null, Now);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or ArgumentException or KeyNotFoundException or InvalidOperationException or FormatException or System.Text.Json.JsonException or System.Xml.XmlException or TaskCanceledException)
        {
            onlineCardCache[card.Id] = ([], "Could not load results. Check the address, key or provider quota, then refresh.", Now);
        }
        finally { onlineLoading.Remove(card.Id); if (!IsDestroyed && resumed && page == "Root" && !searching && WorkspaceEditing.NoteId is null) RefreshBodyInPlace(); }
    }
    private void OpenTimerPermissions()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Android.Content.PM.Permission.Granted)
            RequestPermissions([Android.Manifest.Permission.PostNotifications], 914);
        else if (OperatingSystem.IsAndroidVersionAtLeast(31))
            OpenIntent(new Intent(Android.Provider.Settings.ActionRequestScheduleExactAlarm, Android.Net.Uri.Parse("package:" + PackageName)));
        else
            Toast("Timer alerts are available. Check Android notification settings if alerts are silent.");
    }
}
