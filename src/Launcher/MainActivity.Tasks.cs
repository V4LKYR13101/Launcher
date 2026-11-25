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
            box.CheckedChange += (_, e) => { note.Text = Checklist.Toggle(note.Text, index, e.IsChecked); note.UpdatedAt = Now; progress.Progress = Checklist.Read(note.Text).Count(entry => entry.Done); progress.ContentDescription = progress.Progress + Localize(Resource.String.ui_of) + items.Length + Localize(Resource.String.ui_completed_2); box.SetTextColor(e.IsChecked ? palette.Muted : palette.Text); QueueNoteSave(note); };
            panel.AddView(box, Match());
        }
        RootActions(panel, (Localize(Resource.String.ui_edit_checklist), () => EditChecklist(note)));
    }
    private void EditChecklist(WorkspaceNote? note = null)
    {
        EditText? title = null, items = null;
        Sheet(note is null ? Localize(Resource.String.ui_new_checklist) : Localize(Resource.String.ui_edit_checklist), (panel, dialog) =>
        {
            title = FormField(panel, Localize(Resource.String.ui_title), note?.Title ?? "Checklist");
            items = FormField(panel, Localize(Resource.String.ui_items_one_per_line), note?.Text ?? "", multiline: true);
            items.SetFilters([new Android.Text.InputFilterLengthFilter(19000)]);
        }, (footer, dialog) => DialogActions(footer, (Localize(Resource.String.ui_cancel), dialog.Dismiss), (Localize(Resource.String.ui_save_checklist), () =>
        {
            if (note is null && state.Notes.Count >= 200)
            {
                Toast(Localize(Resource.String.ui_the_note_limit_is_200));
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
            string detail = item.Completed ? Localize(Resource.String.ui_completed) : DateTimeOffset.FromUnixTimeMilliseconds(item.DueAt).ToLocalTime().ToString("ddd, d MMM · HH:mm", UiCulture) + (item.RepeatDays == 0 ? "" : item.RepeatDays == 1 ? Localize(Resource.String.ui_daily_2) : Localize(Resource.String.ui_weekly_2));
            SettingRow(panel, item.Title, detail, () => EditReminder(item), "clock");
        }
        RootActions(panel, (Localize(Resource.String.ui_reminder_action), () => EditReminder()));
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
            dateControl!.Text = Localize(Resource.String.ui_date_2) + when.ToString("ddd, d MMM yyyy", UiCulture);
            timeControl!.Text = Localize(Resource.String.ui_time_2) + when.ToString("t");
            if (error is not null)
                error.Visibility = Android.Views.ViewStates.Gone;
        }
        Sheet(reminder is null ? Localize(Resource.String.ui_new_reminder) : Localize(Resource.String.ui_edit_reminder), (panel, dialog) =>
        {
            title = FormField(panel, Localize(Resource.String.ui_what), reminder?.Title ?? "", Localize(Resource.String.ui_e_g_collect_the_parcel));
            dateControl = Button(Localize(Resource.String.ui_date), () => { var picker = new Android.App.DatePickerDialog(this, (_, value) => { when = value.Date.Date + when.TimeOfDay; UpdateWhen(); }, when.Year, when.Month - 1, when.Day); picker.DatePicker.MinDate = new DateTimeOffset(DateTime.Today).ToUnixTimeMilliseconds(); picker.DatePicker.MaxDate = new DateTimeOffset(DateTime.Today.AddYears(10)).ToUnixTimeMilliseconds(); TrackPicker(picker); });
            panel.AddView(dateControl, Match());
            Gap(panel, 8);
            timeControl = Button(Localize(Resource.String.ui_time), () => TrackPicker(new Android.App.TimePickerDialog(this, (_, value) => { when = when.Date.AddHours(value.HourOfDay).AddMinutes(value.Minute); UpdateWhen(); }, when.Hour, when.Minute, Android.Text.Format.DateFormat.Is24HourFormat(this))));
            panel.AddView(timeControl, Match());
            error = Text("", 13, color: palette.Accent);
            error.Visibility = Android.Views.ViewStates.Gone;
            panel.AddView(error, Match());
            UpdateWhen();
            repeatControl = Button(Localize(Resource.String.ui_repeat_2) + (repeat == 0 ? Localize(Resource.String.ui_once) : repeat == 1 ? Localize(Resource.String.ui_daily) : Localize(Resource.String.ui_weekly)), () => Choose(Resource.String.ui_repeat_reminder, [Localize(Resource.String.ui_once), Localize(Resource.String.ui_daily), Localize(Resource.String.ui_weekly)], i => { repeat = new[] { 0, 1, 7 }[i]; repeatControl!.Text = Localize(Resource.String.ui_repeat_2) + new[] { "Once", "Daily", "Weekly" }[i]; }, repeat == 0 ? 0 : repeat == 1 ? 1 : 2));
            Gap(panel, 8);
            panel.AddView(repeatControl, Match());
            completed = new CheckBox(this) { Text = Localize(Resource.String.ui_completed), Checked = reminder?.Completed ?? false, TextSize = 14, Typeface = regular };
            completed.SetTextColor(palette.Text);
            completed.ButtonTintList = AccentTint();
            completed.SetMinimumHeight(Dp(48));
            panel.AddView(completed, Match());
            var access = new DeviceCapabilities(this);
            Paragraph(access.Notifications ? access.ExactTimers ? Localize(Resource.String.ui_alerts_are_ready) : Localize(Resource.String.ui_android_may_delay_this_alert_until_exact_alarm_access) : Localize(Resource.String.ui_notification_access_is_off_enable_timer_alerts_in_setup), panel);
            if (reminder is not null)
                panel.AddView(Button(Localize(Resource.String.ui_delete_reminder), () => Confirm(Localize(Resource.String.ui_delete_reminder_2), reminder.Title, Localize(Resource.String.ui_delete), () => { ReminderAlerts.Cancel(this, reminder); state.Reminders.Remove(reminder); Save(); dialog.Dismiss(); RenderBody(); })), Match());
        }, (footer, dialog) => DialogActions(footer, (Localize(Resource.String.ui_cancel), dialog.Dismiss), (Localize(Resource.String.ui_save_reminder), () =>
        {
            if (string.IsNullOrWhiteSpace(title?.Text))
            {
                title!.Error = Localize(Resource.String.ui_enter_a_reminder);
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
                InvalidTime(Localize(Resource.String.ui_this_time_is_skipped_by_daylight_saving_choose_another));
                return;
            }
            long due = new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date)).ToUnixTimeMilliseconds();
            if (due > DateTimeOffset.Now.AddYears(10).ToUnixTimeMilliseconds())
            {
                InvalidTime(Localize(Resource.String.ui_choose_a_date_within_the_next_ten_years));
                return;
            }
            if (completed?.Checked != true && due <= Now)
            {
                InvalidTime(Localize(Resource.String.ui_choose_a_future_time));
                return;
            }
            if (reminder is null && state.Reminders.Count >= 200)
            {
                Toast(Localize(Resource.String.ui_the_reminder_limit_is_200));
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
