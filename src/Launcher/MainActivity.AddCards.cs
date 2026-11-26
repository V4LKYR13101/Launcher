using Android.Views;
using Android.Widget;
using LauncherApp.UI;
namespace LauncherApp;

public partial class MainActivity
{
    private void ShowAddCard() => Sheet(Localize(Resource.String.ui_add_a_card), (panel, dialog) =>
    {
        Paragraph(Localize(Resource.String.ui_choose_what_belongs_in_your_workspace), panel);
        CardChoices(panel, dialog.Dismiss);
    });
    private void CardChoices(LinearLayout panel, Action beforePick)
    {
        var choices = new (string Title, string Detail, string Glyph, Action Pick)[]
        {
            (Localize(Resource.String.ui_note),Localize(Resource.String.ui_write_something_down),"note",()=>CreateNote("")),
            (Localize(Resource.String.ui_checklist),Localize(Resource.String.ui_keep_track_of_small_tasks),"note",()=>EditChecklist()),
            (Localize(Resource.String.ui_reminder),Localize(Resource.String.ui_set_a_date_and_time),"clock",()=>EditReminder()),
            (Localize(Resource.String.ui_at_a_glance),Localize(Resource.String.ui_your_date_and_active_items),"calendar",()=>RestoreBuiltInCard("overview")),
            (Localize(Resource.String.ui_agenda),Localize(Resource.String.ui_your_selected_calendars),"calendar",()=>{state.CalendarVisible=true;RestoreBuiltInCard("calendar");}),
            (Localize(Resource.String.ui_timer),Localize(Resource.String.ui_set_a_duration),"clock",()=>Prompt(Resource.String.ui_timer_duration,"10 min",CreateTimer)),
            (Localize(Resource.String.ui_weather),Localize(Resource.String.ui_your_city_forecast),"cloud",()=>ExecuteRootCommand("weather")),
            (Localize(Resource.String.ui_now_playing),Localize(Resource.String.ui_playback_controls),"music",()=>ExecuteRootCommand("music")),
            (Localize(Resource.String.ui_phone_resources),Localize(Resource.String.ui_battery_memory_storage),"resources",AddResourcesCard),
            (Localize(Resource.String.ui_news_feeds),Localize(Resource.String.ui_rss_sources_in_one_card),"rss",ManageFeeds),
            (Localize(Resource.String.ui_web_search),Localize(Resource.String.ui_find_something_online),"search",()=>Prompt(Resource.String.ui_web_query,"",q=>ExecuteRootCommand("web "+q))),
            (Localize(Resource.String.ui_youtube),Localize(Resource.String.ui_videos_channels),"rectangle",()=>Prompt(Resource.String.ui_youtube_query,"",q=>ExecuteRootCommand("youtube "+q))),
            (Localize(Resource.String.ui_journey),Localize(Resource.String.ui_keep_a_ticket_handy),"file",()=>ExecuteRootCommand("ticket"))
        };
        LinearLayout? row = null;
        for (int i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            if (i % 2 == 0)
            {
                row = Stack(true);
                panel.AddView(row, Match());
            }
            var tile = Stack();
            tile.SetMinimumHeight(Dp(112));
            tile.SetPadding(Dp(14), Dp(14), Dp(12), Dp(14));
            tile.Background = Background(palette.Card, 12, palette.Divider);
            TouchFeedback(tile);
            tile.AddView(new GlyphView(this, choice.Glyph, palette.Action), new LinearLayout.LayoutParams(Dp(22), Dp(22)));
            var title = Text(choice.Title, 14, true);
            title.SetPadding(0, Dp(12), 0, Dp(4));
            tile.AddView(title, Match());
            var detail = Text(choice.Detail, 11, color: palette.Muted);
            detail.SetMaxLines(2);
            tile.AddView(detail, Match());
            tile.Click += (_, _) => { beforePick(); choice.Pick(); };
            var lp = new LinearLayout.LayoutParams(0, -1, 1);
            lp.SetMargins(i % 2 == 0 ? 0 : Dp(5), 0, i % 2 == 0 ? Dp(5) : 0, Dp(10));
            row!.AddView(tile, lp);
        }
        if (choices.Length % 2 != 0)
            row!.AddView(new View(this), new LinearLayout.LayoutParams(0, 1, 1));
    }
    private void RestoreBuiltInCard(string id)
    {
        state.RootHidden.Remove(id);
        state.CollapsedCards[id] = false;
        Save();
        Navigate("Root");
    }
}
