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
        catch (Java.Lang.RuntimeException) { agendaError = Localize(Resource.String.ui_calendar_is_unavailable_check_calendar_access_and_try_again); agendaEvents = []; }
        finally { agendaLoading = false; if (!IsDestroyed && resumed && page == "Root" && !searching && WorkspaceEditing.NoteId is null) RefreshBodyInPlace(); }
    }
    private void BuildAgenda(Android.Widget.LinearLayout panel)
    {
        if (!AgendaReader.Allowed(this))
        {
            Paragraph(Localize(Resource.String.ui_see_upcoming_events_from_calendars_you_choose_calendar_data), panel);
            panel.AddView(RootAction(Localize(Resource.String.ui_connect_calendar), () => RequestPermissions([Android.Manifest.Permission.ReadCalendar], 916), true), Match());
            return;
        }
        RefreshAgenda();
        if (agendaError is not null)
            Paragraph(agendaError, panel);
        if (agendaEvents.Length == 0)
            Paragraph(agendaLoading ? Localize(Resource.String.ui_reading_your_calendar) : Localize(Resource.String.ui_no_upcoming_events_in_the_next_seven_days), panel);
        foreach (var entry in agendaEvents)
        {
            var item = entry;
            SettingRow(panel, item.Title, AgendaTime(item) + (item.Location.Length > 0 ? " · " + item.Location : ""), () => OpenAgendaEvent(item), "calendar");
        }
        RootActions(panel, (Localize(Resource.String.ui_calendars), ShowCalendarChoices), (Localize(Resource.String.ui_refresh), () => RefreshAgenda(true)));
    }

    private string AgendaTime(AgendaEvent item)
    {
        var begins = DateTimeOffset.FromUnixTimeMilliseconds(item.Begins);
        if (item.AllDay)
            return Localize(Resource.String.ui_date_all_day, begins.UtcDateTime.ToString("ddd, d MMM", UiCulture));
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
            Sheet(Localize(Resource.String.ui_calendars_in_your_agenda), (panel, dialog) =>
            {
                Paragraph(Localize(Resource.String.ui_choose_the_calendars_to_show_clearing_every_selection_returns), panel);
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
            }, (footer, dialog) => DialogActions(footer, (Localize(Resource.String.ui_cancel), dialog.Dismiss), (Localize(Resource.String.ui_save_calendars), () => { state.CalendarIds = selected.ToList(); Save(); dialog.Dismiss(); RefreshAgenda(true); })));
        }
        catch (Java.Lang.RuntimeException) { Toast(Localize(Resource.String.ui_android_could_not_list_calendars)); }
    }
}
