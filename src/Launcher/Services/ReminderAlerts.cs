using Android.App;
using Android.Content;
using Android.OS;
using Launcher.Core;
namespace LauncherApp.Services;

public static class ReminderAlerts
{
    private static PendingIntent Pending(Context context, WorkspaceReminder item) => PendingIntent.GetBroadcast(context, 0, new Intent(context, typeof(ReminderReceiver)).SetData(Android.Net.Uri.Parse("launcher://reminder/" + System.Uri.EscapeDataString(item.Id))).PutExtra("reminder", item.Id), PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    public static void Cancel(Context context, WorkspaceReminder item) => ((AlarmManager)context.GetSystemService(Context.AlarmService)!).Cancel(Pending(context, item));
    public static void Schedule(Context context, WorkspaceReminder item)
    {
        if (item.Completed || item.DueAt <= 0)
            return;
        var manager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        long at = SystemClock.ElapsedRealtime() + Math.Max(1, item.DueAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(31) || manager.CanScheduleExactAlarms())
            {
                manager.SetExactAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup, at, Pending(context, item));
                return;
            }
        }
        catch (Java.Lang.SecurityException) { }
        manager.SetAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup, at, Pending(context, item));
    }
    public static void Reconcile(Context context, IEnumerable<WorkspaceReminder> before, IEnumerable<WorkspaceReminder> after)
    {
        foreach (var item in before)
            Cancel(context, item);
        foreach (var item in after)
            Schedule(context, item);
    }
}
[BroadcastReceiver(Name = "com.launcher.home.ReminderReceiver", Exported = false)]
public sealed class ReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
            return;
        var owner = context.ApplicationContext ?? context;
        string? id = intent?.GetStringExtra("reminder");
        var pending = GoAsync();
        _ = Task.Run(() => { try { Complete(owner, id); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or Java.Lang.RuntimeException) { Android.Util.Log.Warn("LauncherReminder", "Reminder alert could not complete."); } finally { pending?.Finish(); } });
    }
    private static void Complete(Context context, string? id)
    {
        var store = new StateStore(context);
        WorkspaceReminder? item = null;
        bool ready = false;
        try
        {
            store.EditWorkspace(state =>
        {
            item = state.Reminders.FirstOrDefault(r => r.Id == id);
            if (item is null || item.Completed || item.DueAt <= 0 || item.DueAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1000)
                return false;
            item.LastAlertDueAt = item.DueAt;
            if (item.RepeatDays > 0)
            {
                DateTime next = DateTimeOffset.FromUnixTimeMilliseconds(item.DueAt).LocalDateTime;
                do
                {
                    next = next.AddDays(item.RepeatDays);
                } while (next <= DateTime.Now);
                item.DueAt = new DateTimeOffset(next, TimeZoneInfo.Local.GetUtcOffset(next)).ToUnixTimeMilliseconds();
            }
            else
                item.Completed = true;
            ready = true;
            return true;
        });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Android.Util.Log.Warn("LauncherReminder", "Reminder state could not be saved."); }
        if (item is null || !ready && (item.Completed || item.DueAt <= 0))
            return;
        if (!ready)
        {
            ReminderAlerts.Schedule(context, item);
            return;
        }
        if (item.RepeatDays > 0)
            ReminderAlerts.Schedule(context, item);
        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            manager.CreateNotificationChannel(new NotificationChannel("reminders", "Reminders", NotificationImportance.High));
        var notification = OperatingSystem.IsAndroidVersionAtLeast(26) ? new Notification.Builder(context, "reminders") : new Notification.Builder(context);
        notification.SetSmallIcon(global::Launcher.Resource.Drawable.launcher_icon)!.SetContentTitle(item.Title)!.SetContentText("Your Launcher reminder")!.SetAutoCancel(true)!.SetVisibility(NotificationVisibility.Private)!.SetContentIntent(PendingIntent.GetActivity(context, 0, new Intent(context, typeof(MainActivity)).PutExtra("open_root", true), PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable));
        try
        {
            manager.Notify("reminder:" + item.Id, 0, notification.Build());
        }
        catch (Java.Lang.SecurityException) { }
    }
}
