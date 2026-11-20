using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private void BuildChecklist(LinearLayout panel, WorkspaceNote note)
    {
        var items = Checklist.Read(note.Text);
        int completed = items.Count(i => i.Done);
        var progress = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleHorizontal) { Max = Math.Max(1, items.Length), Progress = completed, ProgressTintList = Android.Content.Res.ColorStateList.ValueOf(palette.Accent) };
        panel.AddView(progress, Match(Dp(3)));
        Gap(panel, 8);
        for (int i = 0; i < items.Length; i++)
        {
            int index = i;
            var item = items[i];
            var box = new CheckBox(this) { Text = item.Text, Checked = item.Done, TextSize = 15, Typeface = regular };
            box.SetTextColor(item.Done ? palette.Muted : palette.Text);
            box.ButtonTintList = AccentTint();
            box.SetMinimumHeight(Dp(48));
            box.CheckedChange += (_, e) => { note.Text = Checklist.Toggle(note.Text, index, e.IsChecked); note.UpdatedAt = Now; progress.Progress = Checklist.Read(note.Text).Count(entry => entry.Done); progress.ContentDescription = progress.Progress + " of " + items.Length + " completed"; box.SetTextColor(e.IsChecked ? palette.Muted : palette.Text); QueueNoteSave(note); };
            panel.AddView(box, Match());
        }
        RootActions(panel, ("Edit checklist", () => EditChecklist(note)));
    }
    private void EditChecklist(WorkspaceNote? note = null)
    {
        EditText? title = null, items = null;
        Sheet(note is null ? "New checklist" : "Edit checklist", (panel, dialog) =>
        {
            title = FormField(panel, "Title", note?.Title ?? "Checklist");
            items = FormField(panel, "Items · one per line", note?.Text ?? "", multiline: true);
            items.SetFilters([new Android.Text.InputFilterLengthFilter(19000)]);
        }, (footer, dialog) => DialogActions(footer, ("Cancel", dialog.Dismiss), ("Save checklist", () =>
        {
            if (note is null && state.Notes.Count >= 200)
            {
                Toast("The note limit is 200.");
                return;
            }
            var entry = note ?? new WorkspaceNote();
            entry.Title = LauncherState.CleanName(title?.Text, "Checklist");
            entry.Checklist = true;
            entry.Text = string.Join('\n', Checklist.Read(items?.Text ?? "").Select(i => (i.Done ? "[x] " : "[ ] ") + i.Text));
            entry.UpdatedAt = Now;
            if (note is null)
            {
                state.Notes.Add(entry);
                state.RootOrder.Add("note:" + entry.Id);
            }
            Save();
            dialog.Dismiss();
            Navigate("Root");
        }
        )));
    }
    private void BuildReminders(LinearLayout panel)
    {
        foreach (var reminder in state.Reminders.OrderBy(r => r.Completed).ThenBy(r => r.DueAt).Take(30))
        {
            var item = reminder;
            string detail = item.Completed ? "Completed" : DateTimeOffset.FromUnixTimeMilliseconds(item.DueAt).ToLocalTime().ToString("ddd, d MMM · HH:mm") + (item.RepeatDays == 0 ? "" : item.RepeatDays == 1 ? " · daily" : " · weekly");
            SettingRow(panel, item.Title, detail, () => EditReminder(item), "clock");
        }
        RootActions(panel, ("+ Reminder", () => EditReminder()));
    }
    private void EditReminder(WorkspaceReminder? reminder = null)
    {
        EditText? title = null;
        TextView? dateControl = null, timeControl = null, repeatControl = null, error = null;
        int repeat = reminder?.RepeatDays ?? 0;
        CheckBox? completed = null;
        DateTime when = DateTimeOffset.FromUnixTimeMilliseconds(reminder?.DueAt ?? Now + 3600000).LocalDateTime;
        void TrackPicker(Android.App.Dialog picker)
        {
            activeSheets.Add(picker);
            openSheetCount++;
            SyncDepthMotion();
            picker.DismissEvent += (_, _) => { activeSheets.Remove(picker); openSheetCount = Math.Max(0, openSheetCount - 1); SyncDepthMotion(); };
            picker.Show();
        }
        void UpdateWhen()
        {
            dateControl!.Text = "Date · " + when.ToString("ddd, d MMM yyyy");
            timeControl!.Text = "Time · " + when.ToString("t");
            if (error is not null)
                error.Visibility = Android.Views.ViewStates.Gone;
        }
        Sheet(reminder is null ? "New reminder" : "Edit reminder", (panel, dialog) =>
        {
            title = FormField(panel, "What", reminder?.Title ?? "", "e.g. Collect the parcel");
            dateControl = Button("Date", () => { var picker = new Android.App.DatePickerDialog(this, (_, value) => { when = value.Date.Date + when.TimeOfDay; UpdateWhen(); }, when.Year, when.Month - 1, when.Day); picker.DatePicker.MinDate = new DateTimeOffset(DateTime.Today).ToUnixTimeMilliseconds(); picker.DatePicker.MaxDate = new DateTimeOffset(DateTime.Today.AddYears(10)).ToUnixTimeMilliseconds(); TrackPicker(picker); });
            panel.AddView(dateControl, Match());
            Gap(panel, 8);
            timeControl = Button("Time", () => TrackPicker(new Android.App.TimePickerDialog(this, (_, value) => { when = when.Date.AddHours(value.HourOfDay).AddMinutes(value.Minute); UpdateWhen(); }, when.Hour, when.Minute, Android.Text.Format.DateFormat.Is24HourFormat(this))));
            panel.AddView(timeControl, Match());
            error = Text("", 13, color: palette.Accent);
            error.Visibility = Android.Views.ViewStates.Gone;
            panel.AddView(error, Match());
            UpdateWhen();
            repeatControl = Button("Repeat · " + (repeat == 0 ? "Once" : repeat == 1 ? "Daily" : "Weekly"), () => Choose("Repeat reminder", ["Once", "Daily", "Weekly"], i => { repeat = new[] { 0, 1, 7 }[i]; repeatControl!.Text = "Repeat · " + new[] { "Once", "Daily", "Weekly" }[i]; }, repeat == 0 ? 0 : repeat == 1 ? 1 : 2));
            Gap(panel, 8);
            panel.AddView(repeatControl, Match());
            completed = new CheckBox(this) { Text = "Completed", Checked = reminder?.Completed ?? false, TextSize = 14, Typeface = regular };
            completed.SetTextColor(palette.Text);
            completed.ButtonTintList = AccentTint();
            completed.SetMinimumHeight(Dp(48));
            panel.AddView(completed, Match());
            var access = new DeviceCapabilities(this);
            Paragraph(access.Notifications ? access.ExactTimers ? "Alerts are ready." : "Android may delay this alert until exact-alarm access is enabled." : "Notification access is off. Enable Timer alerts in Setup & feature health to receive alerts.", panel);
            if (reminder is not null)
                panel.AddView(Button("Delete reminder", () => Confirm("Delete reminder?", reminder.Title, "Delete", () => { ReminderAlerts.Cancel(this, reminder); state.Reminders.Remove(reminder); Save(); dialog.Dismiss(); RenderBody(); })), Match());
        }, (footer, dialog) => DialogActions(footer, ("Cancel", dialog.Dismiss), ("Save reminder", () =>
        {
            if (string.IsNullOrWhiteSpace(title?.Text))
            {
                title!.Error = "Enter a reminder.";
                return;
            }
            DateTime date = DateTime.SpecifyKind(when, DateTimeKind.Unspecified);
            void InvalidTime(string message)
            {
                error!.Text = message;
                error.Visibility = Android.Views.ViewStates.Visible;
            }
            if (TimeZoneInfo.Local.IsInvalidTime(date))
            {
                InvalidTime("This time is skipped by daylight saving. Choose another time.");
                return;
            }
            long due = new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date)).ToUnixTimeMilliseconds();
            if (due > DateTimeOffset.Now.AddYears(10).ToUnixTimeMilliseconds())
            {
                InvalidTime("Choose a date within the next ten years.");
                return;
            }
            if (completed?.Checked != true && due <= Now)
            {
                InvalidTime("Choose a future time.");
                return;
            }
            if (reminder is null && state.Reminders.Count >= 200)
            {
                Toast("The reminder limit is 200.");
                return;
            }
            var item = reminder ?? new WorkspaceReminder();
            if (reminder is not null)
                ReminderAlerts.Cancel(this, item);
            item.Title = LauncherState.CleanName(title?.Text, "Reminder");
            item.DueAt = due;
            item.RepeatDays = repeat;
            item.Completed = completed?.Checked == true;
            if (reminder is null)
                state.Reminders.Add(item);
            state.RootHidden.Remove("reminders");
            Save();
            ReminderAlerts.Schedule(this, item);
            dialog.Dismiss();
            Navigate("Root");
        }
        )));
    }
}
