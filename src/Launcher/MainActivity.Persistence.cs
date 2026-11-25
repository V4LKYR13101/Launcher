using Android.App;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private readonly Dictionary<string, WorkspaceNote> pendingNotes = [];
    private Action? noteSave;
    private Task noteWrites = Task.CompletedTask;
    private void QueueNoteSave(WorkspaceNote note)
    {
        pendingNotes[note.Id] = new()
        {
            Id = note.Id,
            Title = note.Title,
            Text = note.Text,
            Checklist = note.Checklist,
            UpdatedAt = note.UpdatedAt
        };
        if (noteSave is not null)
            handler.RemoveCallbacks(noteSave);
        noteSave = FlushNoteSaves;
        handler.PostDelayed(noteSave, 450);
    }
    private void FlushNoteSaves()
    {
        if (noteSave is not null)
            handler.RemoveCallbacks(noteSave);
        noteSave = null;
        if (pendingNotes.Count == 0)
            return;
        var batch = pendingNotes.Values.ToArray();
        pendingNotes.Clear();
        noteWrites = noteWrites.ContinueWith(_ =>
        {
            try
            {
                store.EditNotes(saved =>
                {
                    foreach (var note in batch)
                    {
                        if (saved.DeletedNoteIds.Contains(note.Id))
                            continue;
                        var previous = saved.Notes.FirstOrDefault(n => n.Id == note.Id);
                        if (previous?.UpdatedAt > note.UpdatedAt)
                            continue;
                        if (previous is not null)
                            saved.Notes.Remove(previous);
                        if (saved.Notes.Count < 200)
                            saved.Notes.Add(note);
                    }
                });
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                RunOnUiThread(() => { foreach (var note in batch) if (!pendingNotes.TryGetValue(note.Id, out var newer) || newer.UpdatedAt < note.UpdatedAt) pendingNotes[note.Id] = note; if (!IsDestroyed) Toast(Localize(Resource.String.ui_your_draft_is_kept_open_saving_failed_check_available)); });
            }
        }, TaskScheduler.Default);
    }
    private void ShowRestoreOptions(LauncherState incoming)
    {
        bool appearance = true, organisation = true, notes = true, workspace = true;
        Sheet(Localize(Resource.String.ui_restore_a_backup), (panel, dialog) =>
        {
            Paragraph(Localize(Resource.String.ui_value_drawers_value_notes_value_timers_choose_what_to, incoming.Drawers.Count, incoming.Notes.Count, incoming.Timers.Count), panel);
            ToggleRow(panel, Localize(Resource.String.ui_appearance), true, v => appearance = v);
            ToggleRow(panel, Localize(Resource.String.ui_app_organisation), true, v => organisation = v);
            ToggleRow(panel, Localize(Resource.String.ui_notes), true, v => notes = v);
            ToggleRow(panel, Localize(Resource.String.ui_cards_journeys_and_timers), true, v => workspace = v);
            Paragraph(Localize(Resource.String.ui_android_widgets_permissions_message_history_consent_and_system_panel), panel);
        }, (footer, dialog) => DialogActions(footer, (Localize(Resource.String.ui_cancel), dialog.Dismiss), (Localize(Resource.String.ui_restore_selected), () =>
        {
            if (!appearance && !organisation && !notes && !workspace)
            {
                Toast(Localize(Resource.String.ui_choose_at_least_one_category));
                return;
            }
            WithProtectedSettings(async () =>
            {
                try
                {
                    FlushNoteSaves();
                    await noteWrites;
                    if (IsDestroyed)
                        return;
                    var restored = BackupRestore.Select(state, incoming, appearance, organisation, notes, workspace);
                    if (!new DeviceCapabilities(this).SecureLock)
                    foreach (var rule in restored.Apps.Values)
                        rule.BiometricLocked = false;
                    AppOrganization.Reconcile(restored, apps);
                    var oldTimers = state.Timers.ToArray();
                    var oldReminders = state.Reminders.ToArray();
                    store.Save(restored, false);
                    state = restored;
                    TimerAlerts.Reconcile(this, oldTimers, state.Timers);
                    ReminderAlerts.Reconcile(this, oldReminders, state.Reminders);
                    SystemPanelService.Refresh();
                    GrayscaleService.RulesChanged();
                    dialog.Dismiss();
                    Navigate("Tiles");
                    Toast(Localize(Resource.String.ui_selected_content_restored));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or Java.Lang.RuntimeException) { Toast(Localize(Resource.String.ui_could_not_restore_your_current_content_is_kept)); }
            });
        }
        )));
    }
}
