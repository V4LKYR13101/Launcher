using Android.Content;
using Android.Widget;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private AgendaEvent[] agendaEvents = [];
    private long agendaUpdated;
    private bool agendaLoading;
    private string? agendaError;
    private async void RefreshAgenda(bool force = false)
    {
        if (!AgendaReader.Allowed(this))
        {
            agendaEvents = [];
            return;
        }
        if (agendaLoading || !force && Now - agendaUpdated < 300000)
            return;
        agendaLoading = true;
        agendaUpdated = Now;
        try
        {
            var selected = state.CalendarIds.ToArray();
            var events = await Task.Run(() => AgendaReader.Read(this, selected));
            if (IsDestroyed)
                return;
            agendaEvents = events;
            agendaError = null;
        }
        catch (Java.Lang.RuntimeException) { agendaError = "Calendar is unavailable. Check calendar access and try again."; agendaEvents = []; }
        finally { agendaLoading = false; if (!IsDestroyed && resumed && page == "Root" && !searching && WorkspaceEditing.NoteId is null) RefreshBodyInPlace(); }
    }
    private void BuildAgenda(Android.Widget.LinearLayout panel)
    {
        if (!AgendaReader.Allowed(this))
        {
            Paragraph("See upcoming events from calendars you choose. Calendar data stays on this device.", panel);
            panel.AddView(RootAction("Connect calendar", () => RequestPermissions([Android.Manifest.Permission.ReadCalendar], 916), true), Match());
            return;
        }
        RefreshAgenda();
        if (agendaError is not null)
            Paragraph(agendaError, panel);
        if (agendaEvents.Length == 0)
            Paragraph(agendaLoading ? "Reading your calendar…" : "No upcoming events in the next seven days.", panel);
        foreach (var entry in agendaEvents)
        {
            var item = entry;
            SettingRow(panel, item.Title, AgendaTime(item) + (item.Location.Length > 0 ? " · " + item.Location : ""), () => OpenAgendaEvent(item), "calendar");
        }
        RootActions(panel, ("Calendars", ShowCalendarChoices), ("Refresh", () => RefreshAgenda(true)));
    }

    private string AgendaTime(AgendaEvent item)
    {
        var begins = DateTimeOffset.FromUnixTimeMilliseconds(item.Begins);
        if (item.AllDay)
            return begins.UtcDateTime.ToString("ddd, d MMM") + " · all day";
        if (item.Begins <= Now)
            return "Now";
        var local = begins.ToLocalTime();
        return local.ToString(local.Date == DateTime.Today ? "HH:mm" : "ddd, d MMM · HH:mm");
    }

    private void OpenAgendaEvent(AgendaEvent item) =>
        OpenIntent(new Intent(Intent.ActionView, Android.Net.Uri.Parse("content://com.android.calendar/events/" + item.Id)));

    private void ShowCalendarChoices()
    {
        if (!AgendaReader.Allowed(this))
        {
            RequestPermissions([Android.Manifest.Permission.ReadCalendar], 916);
            return;
        }
        try
        {
            var calendars = AgendaReader.Calendars(this);
            var selected = state.CalendarIds.ToHashSet();
            bool all = selected.Count == 0;
            Sheet("Calendars in your agenda", (panel, dialog) =>
            {
                Paragraph("Choose the calendars to show. Clearing every selection returns to all visible calendars.", panel);
                foreach (var calendar in calendars)
                {
                    long id = calendar.Id;
                    var box = new CheckBox(this) { Text = calendar.Name, Checked = all || selected.Contains(id), TextSize = 14, Typeface = regular };
                    box.SetTextColor(palette.Text);
                    box.ButtonTintList = AccentTint();
                    box.SetMinimumHeight(Dp(48));
                    if (all)
                        selected.Add(id);
                    box.CheckedChange += (_, e) => { if (e.IsChecked) selected.Add(id); else selected.Remove(id); };
                    panel.AddView(box, Match());
                }
            }, (footer, dialog) => DialogActions(footer, ("Cancel", dialog.Dismiss), ("Save calendars", () => { state.CalendarIds = selected.ToList(); Save(); dialog.Dismiss(); RefreshAgenda(true); })));
        }
        catch (Java.Lang.RuntimeException) { Toast("Android could not list calendars."); }
    }
}
