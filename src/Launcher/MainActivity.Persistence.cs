using Android.App;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private readonly Dictionary<string,WorkspaceNote> pendingNotes=[];
    private Action? noteSave;
    private Task noteWrites=Task.CompletedTask;
    private void QueueNoteSave(WorkspaceNote note)
    {
        pendingNotes[note.Id]=new(){Id=note.Id,Title=note.Title,Text=note.Text,Checklist=note.Checklist,UpdatedAt=note.UpdatedAt};
        if(noteSave is not null)handler.RemoveCallbacks(noteSave);
        noteSave=FlushNoteSaves;handler.PostDelayed(noteSave,450);
    }
    private void FlushNoteSaves()
    {
        if(noteSave is not null)handler.RemoveCallbacks(noteSave);noteSave=null;
        if(pendingNotes.Count==0)return;var batch=pendingNotes.Values.ToArray();pendingNotes.Clear();
        noteWrites=noteWrites.ContinueWith(_=>
        {
            try
            {
                store.EditNotes(saved=>
                {
                    foreach(var note in batch)
                    {
                        if(saved.DeletedNoteIds.Contains(note.Id))continue;
                        var previous=saved.Notes.FirstOrDefault(n=>n.Id==note.Id);
                        if(previous?.UpdatedAt>note.UpdatedAt)continue;
                        if(previous is not null)saved.Notes.Remove(previous);
                        if(saved.Notes.Count<200)saved.Notes.Add(note);
                    }
                });
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {RunOnUiThread(()=>{foreach(var note in batch)if(!pendingNotes.TryGetValue(note.Id,out var newer)||newer.UpdatedAt<note.UpdatedAt)pendingNotes[note.Id]=note;if(!IsDestroyed)Toast("Your draft is kept open. Saving failed; check available storage and try again.");});}
        },TaskScheduler.Default);
    }
    private void ShowRestoreOptions(LauncherState incoming)
    {
        bool appearance=true,organisation=true,notes=true,workspace=true;
        Sheet("Restore a backup",(panel,dialog)=>
        {
            Paragraph($"{incoming.Drawers.Count} drawers · {incoming.Notes.Count} notes · {incoming.Timers.Count} timers. Choose what to restore. Your current layout is kept as the previous saved state.",panel);
            ToggleRow(panel,"Appearance",true,v=>appearance=v);ToggleRow(panel,"App organisation",true,v=>organisation=v);ToggleRow(panel,"Notes",true,v=>notes=v);ToggleRow(panel,"Cards, journeys and timers",true,v=>workspace=v);
            Paragraph("Android widgets, permissions, message-history consent and system-panel access remain on this device.",panel);
        },(footer,dialog)=>DialogActions(footer,("Cancel",dialog.Dismiss),("Restore selected",()=>
        {
            if(!appearance&&!organisation&&!notes&&!workspace){Toast("Choose at least one category.");return;}
            WithProtectedSettings(async ()=>
            {
                try
                {
                    FlushNoteSaves();await noteWrites;if(IsDestroyed)return;var restored=BackupRestore.Select(state,incoming,appearance,organisation,notes,workspace);
                    if(!new DeviceCapabilities(this).SecureLock)foreach(var rule in restored.Apps.Values)rule.BiometricLocked=false;
                    AppOrganization.Reconcile(restored,apps);var oldTimers=state.Timers.ToArray();var oldReminders=state.Reminders.ToArray();store.Save(restored,false);state=restored;
                    TimerAlerts.Reconcile(this,oldTimers,state.Timers);ReminderAlerts.Reconcile(this,oldReminders,state.Reminders);SystemPanelService.Refresh();GrayscaleService.RulesChanged();dialog.Dismiss();Navigate("Tiles");Toast("Selected content restored.");
                }
                catch(Exception e)when(e is IOException or UnauthorizedAccessException or Java.Lang.RuntimeException){Toast("Could not restore. Your current content is kept.");}
            });
        })));
    }
}
