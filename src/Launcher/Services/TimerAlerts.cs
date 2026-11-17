using Android.App;
using Android.Content;
using Android.OS;
using Launcher.Core;
using System.Security.Cryptography;
namespace LauncherApp.Services;
public static class TimerAlerts
{
    private static int Code(string id)=>BitConverter.ToInt32(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id)),0)&int.MaxValue;
    private static PendingIntent Pending(Context context,WorkspaceTimer timer)=>PendingIntent.GetBroadcast(context,Code(timer.Id),new Intent(context,typeof(TimerReceiver)).SetData(Android.Net.Uri.Parse("launcher://timer/"+System.Uri.EscapeDataString(timer.Id))).PutExtra("timer",timer.Id),PendingIntentFlags.UpdateCurrent|PendingIntentFlags.Immutable)!;
    public static void Reconcile(Context context,IEnumerable<WorkspaceTimer> oldTimers,IEnumerable<WorkspaceTimer> newTimers)
    {foreach(var timer in oldTimers)Cancel(context,timer);foreach(var timer in newTimers.Where(t=>t.EndsAt>0))Schedule(context,timer);}
    public static void Restore(Context context)
    {try{var saved=new StateStore(context).Load();foreach(var timer in saved.Timers.Where(t=>t.EndsAt>0))Schedule(context,timer);foreach(var reminder in saved.Reminders)ReminderAlerts.Schedule(context,reminder);}catch(Exception e)when(e is IOException or UnauthorizedAccessException or Java.Lang.RuntimeException){Android.Util.Log.Warn("LauncherTimer","Could not restore scheduled alerts.");}}
    public static void Cancel(Context context,WorkspaceTimer timer)
    {var alarms=(AlarmManager)context.GetSystemService(Context.AlarmService)!;alarms.Cancel(Pending(context,timer));alarms.Cancel(PendingIntent.GetBroadcast(context,Code(timer.Id),new Intent(context,typeof(TimerReceiver)).PutExtra("timer",timer.Id),PendingIntentFlags.UpdateCurrent|PendingIntentFlags.Immutable)!);}
    public static void Schedule(Context context,WorkspaceTimer timer)
    {
        var alarms=(AlarmManager)context.GetSystemService(Context.AlarmService)!;long at=SystemClock.ElapsedRealtime()+Math.Max(1,timer.EndsAt-DateTimeOffset.Now.ToUnixTimeMilliseconds());
        try{if(!OperatingSystem.IsAndroidVersionAtLeast(31)||alarms.CanScheduleExactAlarms()){alarms.SetExactAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup,at,Pending(context,timer));return;}}catch(Java.Lang.SecurityException){}
        alarms.SetAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup,at,Pending(context,timer));
    }
}
[BroadcastReceiver(Name="com.launcher.home.TimerReceiver",Exported=false)]
public sealed class TimerReceiver:BroadcastReceiver
{
    public override void OnReceive(Context? context,Intent? intent)
    {
        if(context is null)return;var owner=context.ApplicationContext??context;string? id=intent?.GetStringExtra("timer");var pending=GoAsync();
        _=Task.Run(()=>{try{Complete(owner,id);}catch(Exception e)when(e is IOException or UnauthorizedAccessException or Java.Lang.RuntimeException){Android.Util.Log.Warn("LauncherTimer","Timer alert could not complete.");}finally{pending?.Finish();}});
    }
    private static void Complete(Context context,string? id)
    {
        var store=new StateStore(context);WorkspaceTimer? timer=null;bool ready=false;
        try{store.EditWorkspace(state=>{timer=state.Timers.FirstOrDefault(t=>t.Id==id);if(timer is null||timer.EndsAt<=0||timer.EndsAt>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+1000)return false;timer.LastAlertEndsAt=timer.EndsAt;timer.EndsAt=0;timer.RemainingMs=0;ready=true;return true;});}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){Android.Util.Log.Warn("LauncherTimer","Timer state could not be saved.");}
        if(timer is null||!ready&&timer.EndsAt<=0)return;
        if(!ready){TimerAlerts.Schedule(context,timer);return;}
        var manager=(NotificationManager)context.GetSystemService(Context.NotificationService)!;
        if(OperatingSystem.IsAndroidVersionAtLeast(26))manager.CreateNotificationChannel(new NotificationChannel("timers","Timer alerts",NotificationImportance.High));
        var builder=OperatingSystem.IsAndroidVersionAtLeast(26)?new Notification.Builder(context,"timers"):new Notification.Builder(context);
        var open=PendingIntent.GetActivity(context,0,new Intent(context,typeof(MainActivity)).PutExtra("open_root",true),PendingIntentFlags.UpdateCurrent|PendingIntentFlags.Immutable);
        builder.SetSmallIcon(global::Launcher.Resource.Drawable.launcher_icon)!.SetContentTitle("Timer finished")!.SetContentText(timer.Title)!.SetContentIntent(open)!.SetAutoCancel(true)!.SetCategory(Notification.CategoryAlarm);
        builder.SetVisibility(NotificationVisibility.Private);
        try{manager.Notify("timer:"+timer.Id,0,builder.Build());}catch(Java.Lang.SecurityException){}
    }
}
[BroadcastReceiver(Name="com.launcher.home.TimerBootReceiver",Exported=true)]
[IntentFilter([Intent.ActionBootCompleted,Intent.ActionMyPackageReplaced])]
public sealed class TimerBootReceiver:BroadcastReceiver
{
    public override void OnReceive(Context? context,Intent? intent)
    {
        if(context is null)return;var owner=context.ApplicationContext??context;var pending=GoAsync();
        _=Task.Run(()=>{try{TimerAlerts.Restore(owner);}finally{pending?.Finish();}});
    }
}
