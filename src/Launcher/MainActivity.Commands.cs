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
        Title(Localize(Resource.String.ui_online_cards));
        SettingRow(body, Localize(Resource.String.ui_rss_feeds), Localize(Resource.String.ui_value_enabled_sources_one_news_card, Sources.Count(s => s.Enabled)), ManageFeeds, "rss");
        RootActions(body, (Localize(Resource.String.ui_add_rss_feed_action), () => EditFeed()));
        Gap(body, 16);
        var label = Text(Localize(Resource.String.ui_search_providers_upper), 10, true, palette.Muted);
        label.LetterSpacing = .10f;
        body.AddView(label, Match());
        Paragraph(Localize(Resource.String.ui_keys_enable_inline_search_results_rss_feeds_do_not));
        foreach (var entry in new[] { ("brave", Resource.String.ui_brave_search_api_key), ("youtube", Resource.String.ui_youtube_data_api_key) })
            SettingRow(body, Localize(entry.Item2), IntegrationKey(entry.Item1).Length > 0 ? Localize(Resource.String.ui_configured) : Localize(Resource.String.ui_not_configured), () => Prompt(entry.Item2, "", value => { GetSharedPreferences("integrations", FileCreationMode.Private)!.Edit()!.PutString(entry.Item1, value.Trim())!.Apply(); onlineCardCache.Clear(); RenderBody(); }), "settings");
        body.AddView(Button(Localize(Resource.String.ui_brave_api_account), () => OpenUrl("https://api.search.brave.com/")), Match());
        body.AddView(Button(Localize(Resource.String.ui_youtube_api_setup), () => OpenUrl("https://developers.google.com/youtube/v3/getting-started")), Match());
    }
    private void RenderRootCommands()
    {
        if (query.Trim().Length > 0)
        {
            Title(Localize(Resource.String.ui_search_your_workspace));
            foreach (var result in LocalSearch.Find(state, query))
            {
                var item = result;
                SettingRow(body, item.Title, item.Kind + " · " + item.Preview, () => OpenSearchHit(new(null, item)), item.Kind == "Journey" ? "file" : "note");
            }
            var command = RootCommand.Parse(query);
            if (command.Kind.Length > 0)
                body.AddView(Button(Localize(Resource.String.ui_run) + query, () => ExecuteRootCommand(query), true), Match());
            else if (LocalSearch.Find(state, query).Length == 0)
                Paragraph(Localize(Resource.String.ui_no_saved_content_matches_search_apps_to_include_installed));
            return;
        }
        Title(Localize(Resource.String.ui_add_a_card));
        Paragraph(Localize(Resource.String.ui_choose_a_card_or_type_a_command_below));
        CardChoices(body, () => { HideKeyboard(); searching = false; query = ""; Render(); });
        if (query.Trim().Length > 0)
            body.AddView(Button(Localize(Resource.String.ui_run) + query, () => ExecuteRootCommand(query), true), Match());
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
                    Prompt(Resource.String.ui_timer_duration, "10 min", CreateTimer);
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
                    Prompt(command.Kind == "youtube" ? Resource.String.ui_youtube_query : Resource.String.ui_web_query, "", value => ExecuteRootCommand(command.Kind + " " + value));
                    return;
                }
                if (state.ExtraCards.Count >= 40)
                {
                    Toast(Localize(Resource.String.ui_remove_a_card_before_adding_another));
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
            Paragraph(Localize(Resource.String.ui_configure_your_api_key_for_inline_results_or_open), panel);
            RootActions(panel, (Localize(Resource.String.ui_online_card_settings), () => Navigate("Integrations")));
            RootActions(panel, (Localize(Resource.String.ui_open_search), () => OpenUrl(card.Kind == "youtube" ? "https://www.youtube.com/results?search_query=" + System.Uri.EscapeDataString(card.Query) : "https://www.qwant.com/?q=" + System.Uri.EscapeDataString(card.Query))));
            return;
        }
        if (onlineCardCache.TryGetValue(card.Id, out var cached))
        {
            if (cached.Error is not null)
                Paragraph(cached.Error, panel);
            else if (cached.Links.Length == 0)
                Paragraph(Localize(Resource.String.ui_no_results_returned), panel);
            else
            foreach (var link in cached.Links)
                SettingRow(panel, link.Title, link.Detail, () => OpenUrl(link.Url), "link");
        }
        else
        {
            Paragraph(Localize(Resource.String.ui_loading), panel);
            _ = LoadOnlineCard(card, key);
        }
        RootActions(panel, (onlineLoading.Contains(card.Id) ? Localize(Resource.String.ui_refreshing) : Localize(Resource.String.ui_refresh), () => _ = LoadOnlineCard(card, key)));
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
            Toast(Localize(Resource.String.ui_timer_alerts_are_available_check_android_notification_settings_if));
    }
}
