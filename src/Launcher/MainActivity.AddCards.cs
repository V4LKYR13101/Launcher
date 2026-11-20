using Android.Views;
using Android.Widget;
using LauncherApp.UI;
namespace LauncherApp;

public partial class MainActivity
{
    private void ShowAddCard() => Sheet("Add a card", (panel, dialog) =>
    {
        Paragraph("Choose what belongs in your workspace.", panel);
        CardChoices(panel, dialog.Dismiss);
    });
    private void CardChoices(LinearLayout panel, Action beforePick)
    {
        var choices = new (string Title, string Detail, string Glyph, Action Pick)[]
        {
            ("Note","Write something down","note",()=>CreateNote("")),
            ("Checklist","Keep track of small tasks","note",()=>EditChecklist()),
            ("Reminder","Set a date and time","clock",()=>EditReminder()),
            ("At a glance","Your date and active items","calendar",()=>RestoreBuiltInCard("overview")),
            ("Agenda","Your selected calendars","calendar",()=>{state.CalendarVisible=true;RestoreBuiltInCard("calendar");}),
            ("Timer","Set a duration","clock",()=>Prompt("Timer duration","10 min",CreateTimer)),
            ("Weather","Your city & forecast","cloud",()=>ExecuteRootCommand("weather")),
            ("Now playing","Playback controls","music",()=>ExecuteRootCommand("music")),
            ("Phone resources","Battery, memory & storage","resources",AddResourcesCard),
            ("News & feeds","RSS sources in one card","rss",ManageFeeds),
            ("Web search","Find something online","search",()=>Prompt("Web query","",q=>ExecuteRootCommand("web "+q))),
            ("YouTube","Videos & channels","rectangle",()=>Prompt("YouTube query","",q=>ExecuteRootCommand("youtube "+q))),
            ("Journey","Keep a ticket handy","file",()=>ExecuteRootCommand("ticket"))
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
