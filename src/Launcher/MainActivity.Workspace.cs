using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.UI;
namespace LauncherApp;

public partial class MainActivity
{
    private readonly List<(WorkspaceTimer Timer, TextView Text)> workspaceTimerViews = [];
    private void RenderWorkspace()
    {
        workspaceTimerViews.Clear();
        var cards = new Dictionary<string, Action>();
        cards["overview"] = () => RootCard("overview", "At a glance", "calendar", BuildOverview, DateTime.Today.ToString("dddd, d MMMM"));
        if (state.CalendarVisible)
            cards["calendar"] = () => RootCard("calendar", "Agenda", "calendar", BuildAgenda, agendaEvents.FirstOrDefault()?.Title ?? "Your upcoming events");
        if (!string.IsNullOrEmpty(state.Weather.City))
            cards["weather"] = () => RootCard("weather", "Weather", "cloud", BuildForecast, WeatherSummary());
        if (state.ShowMediaCard && media.Controller is not null)
            cards["media"] = () => RootCard("media", "Now playing", "music", panel =>
        {
            var old = body;
            body = panel;
            AddMediaCard();
            body = old;
            if (panel.ChildCount == 0)
                Paragraph("Start playback in a media app to show its controls.", panel);
        }, media.Controller?.Metadata?.GetString(Android.Media.MediaMetadata.MetadataKeyTitle) ?? "No active player");
        if (state.FocusCardVisible)
            cards["focus"] = () => RootCard("focus", "Focus", "focus", panel =>
        {
            timerText = Text(FocusTime(), 38, true);
            panel.AddView(timerText, Match());
            var buttons = Stack(true);
            buttons.BaselineAligned = false;
            foreach (int minutes in new[] { 15, 25, 50 })
            {
                var choice = RootAction(minutes + " min", () => { state.Focus = true; state.FocusEndsAt = Now + minutes * 60000; Save(); Render(); });
                choice.Background = Background(Color.Transparent, 10, palette.Divider);
                var lp = new LinearLayout.LayoutParams(0, -1, 1);
                lp.SetMargins(0, Dp(8), Dp(6), 0);
                buttons.AddView(choice, lp);
            }
            panel.AddView(buttons, Match());
            if (state.Focus)
                RootActions(panel, ("End focus", () => { state.Focus = false; state.FocusEndsAt = 0; Save(); Render(); }
                ));
        }, FocusTime());
        if (state.NotesVisible)
        {
            cards["notes"] = () => RootCard("notes", "Notes", "note", panel => { Paragraph(Counted(state.Notes.Count, "saved note"), panel); panel.AddView(RootAction("+ New note", () => CreateNote(""), true), new LinearLayout.LayoutParams(-2, -2)); }, Counted(state.Notes.Count, "saved note"));
            foreach (var note in state.Notes.ToArray())
            {
                var item = note;
                string key = "note:" + item.Id;
                cards[key] = () => RootCard(key, item.Title, "note", panel =>
                {
                    if (item.Checklist)
                    {
                        BuildChecklist(panel, item);
                        return;
                    }
                    var input = new EditText(this) { Text = item.Text, Hint = "Write a note…", TextSize = 16, Typeface = regular, Gravity = GravityFlags.Top };
                    StyleEntry(input);
                    input.SetMinLines(3);
                    input.SetMaxLines(10);
                    input.SetFilters([new Android.Text.InputFilterLengthFilter(20000)]);
                    input.TextChanged += (_, _) => { item.Text = input.Text ?? ""; item.UpdatedAt = Math.Max(Now, item.UpdatedAt + 1); QueueNoteSave(item); };
                    input.FocusChange += (_, e) => WorkspaceEditing.NoteId = e.HasFocus ? item.Id : null;
                    panel.AddView(input, Match());
                    RootActions(panel, ("Rename", () => Prompt("Note title", item.Title, s => { item.Title = LauncherState.CleanName(s, "Note"); item.UpdatedAt = Now; Save(); RenderBody(); })));
                }, item.Text.Length == 0 ? "Empty note" : item.Text.Split('\n')[0]);
            }
        }
        cards["timers"] = () => RootCard("timers", "Timers", "clock", panel =>
        {
            panel.AddView(RootAction("+ New timer", () => Prompt("Timer duration", "10 min", CreateTimer), true), new LinearLayout.LayoutParams(-2, -2));
            foreach (var timer in state.Timers.ToArray())
            {
                var item = timer;
                var title = Text(item.Title, 12, color: palette.Muted);
                title.SetPadding(0, Dp(12), 0, Dp(4));
                panel.AddView(title, Match());
                var label = Text(TimerDisplay(item), 30, true, palette.Accent);
                workspaceTimerViews.Add((item, label));
                panel.AddView(label, Match());
                RootActions(panel, (item.EndsAt > 0 ? "Pause" : "Start", () => ToggleTimer(item)),
                    ("Reset", () => { TimerAlerts.Cancel(this, item); item.EndsAt = 0; item.RemainingMs = item.DurationMs; Save(); RenderBody(); }
                ),
                    ("Remove", () => { TimerAlerts.Cancel(this, item); state.Timers.Remove(item); Save(); RenderBody(); }
                ));
            }
        }, $"{state.Timers.Count(t => t.EndsAt > 0)} running · {state.Timers.Count} timers");
        if (state.Reminders.Count > 0)
            cards["reminders"] = () => RootCard("reminders", "Reminders", "clock", BuildReminders, Counted(state.Reminders.Count(r => !r.Completed), "upcoming reminder"));
        if (state.CalculatorVisible)
            cards["calculator"] = () => RootCard("calculator", "Calculator", "calculator", panel =>
        {
            var input = new EditText(this) { Hint = "(24 + 6) × 3", TextSize = 21, Typeface = regular };
            input.SetSingleLine(true);
            StyleEntry(input);
            var answer = Text("=", 30, true, palette.Accent);
            input.TextChanged += (_, _) => answer.Text = "= " + Calculator.Evaluate(input.Text ?? "");
            panel.AddView(input, Match());
            panel.AddView(answer, Match());
        }, "Quick arithmetic");
        if (state.ResourcesVisible)
            cards["resources"] = () => RootCard("resources", "Phone resources", "resources", BuildResources, "Battery, memory & storage");
        cards["feeds"] = () => RootCard("feeds", "News & feeds", "rss", BuildFeeds, Counted(Sources.Count(s => s.Enabled), "enabled source"));
        cards["tickets"] = () => RootCard("tickets", "Tickets", "file", BuildTickets, $"{state.Tickets.Count} saved journeys");
        foreach (var extra in state.ExtraCards.ToArray())
        {
            var item = extra;
            cards[item.Id] = () => RootCard(item.Id, item.Title.Length > 0 ? item.Title : item.Kind, "file", p => BuildOnlineCard(p, item), item.Query);
        }
        foreach (var widget in state.Widgets.ToArray())
        {
            var item = widget;
            cards["widget:" + item.Id] = () => RenderWidget(item);
        }
        var order = state.RootOrder.Concat(cards.Keys).Distinct().Where(cards.ContainsKey).OrderByDescending(k => state.RootPinned.Contains(k)).ToArray();
        foreach (string key in order)
        if (!state.RootHidden.Contains(key))
            cards[key]();
        RootActions(body, ("+ Add card", ShowAddCard), ("Arrange", ManageRootCards), ("+ Add widget", AddWidget));
    }
    private void RootOptions(string id, string title) => Choose(title, [state.RootPinned.Contains(id) ? "Unpin" : "Pin to top", "Move earlier", "Move later", "Hide card", "Remove card"], i =>
    {
        if (i == 0)
        {
            if (!state.RootPinned.Remove(id))
                state.RootPinned.Add(id);
        }
        else if (i is 1 or 2)
        {
            MoveTodayCard(id, i == 1 ? -1 : 1);
        }
        else if (i == 3)
        {
            if (!state.RootHidden.Contains(id))
                state.RootHidden.Add(id);
        }
        else
            Confirm("Remove " + title + "?", "Remove this card and its content?", "Remove", () =>
        {
            if (id.StartsWith("note:"))
            {
                string noteId = id[5..];
                state.Notes.RemoveAll(n => n.Id == noteId);
                state.DeletedNoteIds.Add(noteId);
            }
            else if (state.ExtraCards.RemoveAll(c => c.Id == id) == 0)
                state.RootHidden.Add(id);
            Save();
            RenderBody();
        });
        Save();
        RenderBody();
    });
    private void ManageRootCards() => Sheet("Root cards", (panel, dialog) =>
    {
        Paragraph("Hold any card header to pin, move, hide or remove it. Restore hidden cards below.", panel);
        foreach (string id in state.RootHidden.ToArray())
            panel.AddView(Button("Restore " + CardLabel(id), () => { if (id == "resources") state.ResourcesVisible = true; state.RootHidden.Remove(id); Save(); dialog.Dismiss(); RenderBody(); }), Match());
        panel.AddView(Button("Root settings", () => { dialog.Dismiss(); Navigate("Root settings"); }), Match());
    });
    private void CreateNote(string title)
    {
        if (state.Notes.Count >= 200)
        {
            Toast("The note limit is 200.");
            return;
        }
        if (title.Length == 0)
        {
            Prompt("Note title", "", CreateNote);
            return;
        }
        var note = new WorkspaceNote { Title = LauncherState.CleanName(title, "Note") };
        state.Notes.Add(note);
        state.RootOrder.Add("note:" + note.Id);
        state.CollapsedCards["note:" + note.Id] = false;
        Save();
        Navigate("Root");
    }
    private void CreateTimer(string duration)
    {
        try
        {
            if (state.Timers.Count >= 20)
            {
                Toast("The timer limit is 20.");
                return;
            }
            long ms = RootCommand.TimerDuration(duration);
            state.Timers.Add(new()
            {
                Title = LauncherState.CleanName(duration + " timer", "Timer"),
                DurationMs = ms,
                RemainingMs = ms
            });
            Save();
            Navigate("Root");
        }
        catch (ArgumentException e) { Toast(e.Message); }
    }
    private string TimerDisplay(WorkspaceTimer timer)
    {
        long ms = timer.EndsAt > 0 ? Math.Max(0, timer.EndsAt - Now) : timer.RemainingMs;
        var time = TimeSpan.FromMilliseconds(ms);
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }
    private void UpdateWorkspaceTimers()
    {
        foreach (var (timer, label) in workspaceTimerViews)
        {
            string value = TimerDisplay(timer);
            if (label.Text != value)
                label.Text = value;
        }
    }
    private void ToggleTimer(WorkspaceTimer timer)
    {
        if (timer.EndsAt > 0)
        {
            timer.RemainingMs = Math.Max(0, timer.EndsAt - Now);
            timer.EndsAt = 0;
            TimerAlerts.Cancel(this, timer);
        }
        else
        {
            if (timer.RemainingMs <= 0)
                timer.RemainingMs = timer.DurationMs;
            timer.EndsAt = Now + timer.RemainingMs;
            TimerAlerts.Schedule(this, timer);
        }
        Save();
        RenderBody();
    }
    private string WeatherSummary() => string.IsNullOrEmpty(state.Weather.City) ? "Choose a city" : state.Weather.City + " · " + Degrees(state.Weather.Temperature) + " · " + WeatherCondition.Label(state.Weather.Code);
    private void BuildForecast(LinearLayout panel)
    {
        if (string.IsNullOrEmpty(state.Weather.City))
        {
            Paragraph("Choose a city to show the forecast.", panel);
            RootActions(panel, ("Set location", () => Navigate("Weather")));
            return;
        }
        var city = Text(state.Weather.City, 13, true);
        city.SetSingleLine(true);
        city.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        panel.AddView(city, Match());
        var current = Stack(true);
        current.SetGravity(GravityFlags.CenterVertical);
        current.SetPadding(0, Dp(8), 0, Dp(8));
        current.AddView(Text(Degrees(state.Weather.Temperature), 42, true, palette.Accent), new LinearLayout.LayoutParams(-2, -2));
        var condition = Stack();
        condition.SetPadding(Dp(16), 0, 0, 0);
        condition.AddView(Text(WeatherCondition.Label(state.Weather.Code), 14, true, palette.Accent), Match());
        var range = Text("High " + Degrees(state.Weather.High) + "  /  Low " + Degrees(state.Weather.Low), 11, color: palette.Muted);
        range.SetPadding(0, Dp(6), 0, 0);
        condition.AddView(range, Match());
        current.AddView(condition, new LinearLayout.LayoutParams(0, -2, 1));
        panel.AddView(current, Match());
        var hours = (state.Weather.Hours ?? []).Where(h => string.CompareOrdinal(h.Time, DateTime.UtcNow.AddSeconds(state.Weather.UtcOffsetSeconds).ToString("yyyy-MM-ddTHH:00")) >= 0).Take(24).ToArray();
        if (hours.Length > 0)
            panel.AddView(new HistoryChart(this, hours.Select(h => (float)(state.Weather.Fahrenheit ? h.Temperature * 1.8 + 32 : h.Temperature)).ToArray(), hours.Select(h => h.Time.Split('T').Last()).ToArray(), palette.Accent, palette.Text, unit: "°"), Match(Dp(155)));
        foreach (var day in state.Weather.Days ?? [])
        {
            var row = Stack(true);
            row.SetGravity(GravityFlags.CenterVertical);
            string label = DateTime.TryParse(day.Date, out var date) ? date.ToString("ddd") : day.Date;
            row.AddView(Text(label, 12, true), new LinearLayout.LayoutParams(Dp(42), -2));
            row.AddView(new GlyphView(this, WeatherCondition.Glyph(day.Code), palette.Muted), new LinearLayout.LayoutParams(Dp(17), Dp(17)));
            var description = Text(WeatherCondition.Label(day.Code), 11, color: palette.Muted);
            description.SetPadding(Dp(8), 0, Dp(6), 0);
            description.SetSingleLine(true);
            description.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            row.AddView(description, new LinearLayout.LayoutParams(0, -2, 1));
            row.AddView(Text(Degrees(day.High) + " / " + Degrees(day.Low), 12));
            row.SetMinimumHeight(Dp(38));
            row.SetPadding(0, Dp(7), 0, Dp(7));
            panel.AddView(row, Match());
        }

    }
}
