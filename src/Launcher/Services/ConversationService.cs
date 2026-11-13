using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using Launcher.Core;

namespace LauncherApp.Services;

public sealed record Conversation(string Key, string Package, string Title, string Text, long Time, PendingIntent? Open,bool Dismissible=true);

[Service(Label = "Launcher notifications", Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE", Exported = true)]
[IntentFilter(["android.service.notification.NotificationListenerService"])]
public sealed class ConversationService : NotificationListenerService
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Conversation> Messages = new();
    private static readonly Dictionary<string, Conversation> Notifications = new();
    private static readonly Dictionary<string,BadgeSignal> BadgeSignals=new();
    public static event Action? Changed;
    private static ConversationService? current;

    public static IReadOnlyList<Conversation> Snapshot()
    {
        lock (Gate) return Messages.Values.OrderByDescending(m => m.Time).Take(100).ToArray();
    }
    public static IReadOnlyList<Conversation> NotificationSnapshot()
    {
        lock(Gate) return Notifications.Values.OrderByDescending(m=>m.Time).Take(100).ToArray();
    }
    public static Dictionary<string,int> BadgeCounts()
    {
        lock(Gate)return NotificationBadges.Count(BadgeSignals.Values);
    }

    public static bool Enabled(Context context) =>
        (Android.Provider.Settings.Secure.GetString(context.ContentResolver, "enabled_notification_listeners") ?? "")
        .Split(':').Any(c => c.StartsWith(context.PackageName + "/", StringComparison.Ordinal));

    public override void OnListenerConnected()
    {
        base.OnListenerConnected();
        current = this;
        lock (Gate) { Messages.Clear(); Notifications.Clear(); BadgeSignals.Clear(); }
        foreach (var notification in GetActiveNotifications() ?? []) Capture(notification);
        Changed?.Invoke();
    }

    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        if (sbn is not null) { Capture(sbn); Changed?.Invoke(); }
    }

    public override void OnNotificationRemoved(StatusBarNotification? sbn)
    {
        if (sbn is null) return;
        lock (Gate) { Messages.Remove(sbn.Key!); Notifications.Remove(sbn.Key!); BadgeSignals.Remove(sbn.Key!); }
        Changed?.Invoke();
    }

    public override void OnListenerDisconnected()
    {
        current = null;
        lock (Gate) { Messages.Clear(); Notifications.Clear(); BadgeSignals.Clear(); }
        Changed?.Invoke();
        base.OnListenerDisconnected();
    }

    private static void Capture(StatusBarNotification sbn)
    {
        var n = sbn.Notification;
        if(n is null)return;
        lock(Gate)
        {
            BadgeSignals[sbn.Key!]=new(sbn.Key!,sbn.PackageName!,sbn.GroupKey??sbn.Key!,n.Number,
                (n.Flags&NotificationFlags.GroupSummary)!=0,sbn.IsClearable);
            if(BadgeSignals.Count>500)BadgeSignals.Remove(BadgeSignals.Keys.First());
        }
        if((n.Flags&NotificationFlags.GroupSummary)!=0)return;
        var extras = n.Extras;
        var title = extras?.GetCharSequence("android.conversationTitle")?.ToString()
            ?? extras?.GetCharSequence(Notification.ExtraTitle)?.ToString() ?? "Message";
        var body = extras?.GetCharSequence(Notification.ExtraText)?.ToString() ?? (n.Category==Notification.CategoryMessage?"New message":"");
        lock (Gate)
        {
            var entry=new Conversation(sbn.Key!, sbn.PackageName!, title, body, sbn.PostTime, n.ContentIntent,sbn.IsClearable);
            Notifications[sbn.Key!]=entry;
            if(n.Category==Notification.CategoryMessage || extras?.ContainsKey("android.messages")==true) Messages[sbn.Key!]=entry;
            else Messages.Remove(sbn.Key!);
            if(Notifications.Count>100)Notifications.Remove(Notifications.Values.MinBy(m=>m.Time)!.Key);
            if (Messages.Count > 100)
                Messages.Remove(Messages.Values.MinBy(m => m.Time)!.Key);
        }
    }

    public static void Dismiss(string key)
    {
        try { current?.CancelNotification(key); }
        catch (Java.Lang.SecurityException) { }
    }
}
