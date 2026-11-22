using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using Launcher.Core;
using LauncherApp.Services.Speech;

namespace LauncherApp.Services;

public sealed record Conversation(string Key, string Package, string Title, string Text, long Time, PendingIntent? Open, bool Dismissible = true);

[Service(Label = "Launcher notifications", Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE", Exported = true)]
[IntentFilter(["android.service.notification.NotificationListenerService"])]
public sealed class ConversationService : NotificationListenerService
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Conversation> Messages = new();
    private static readonly Dictionary<string, Conversation> Notifications = new();
    private static readonly Dictionary<string, BadgeSignal> BadgeSignals = new();
    private static readonly Dictionary<string, Notification.Action> ReplyActions = new();
    private static readonly Dictionary<string, string> Threads = new();
    public static event Action? Changed;
    private static ConversationService? current;

    public static IReadOnlyList<Conversation> Snapshot()
    {
        lock (Gate)
            return Messages.Values.OrderByDescending(m => m.Time).Take(100).ToArray();
    }
    public static IReadOnlyList<Conversation> NotificationSnapshot()
    {
        lock (Gate)
            return Notifications.Values.OrderByDescending(m => m.Time).Take(100).ToArray();
    }
    public static Dictionary<string, int> BadgeCounts()
    {
        lock (Gate)
            return NotificationBadges.Count(BadgeSignals.Values);
    }

    public static bool Enabled(Context context) =>
        (Android.Provider.Settings.Secure.GetString(context.ContentResolver, "enabled_notification_listeners") ?? "")
        .Split(':').Any(c => c.StartsWith(context.PackageName + "/", StringComparison.Ordinal));

    public static bool TryOpenActivity(Context context, Conversation notification)
    {
        if (notification.Open is not { } pending || OperatingSystem.IsAndroidVersionAtLeast(31) && !pending.IsActivity)
            return false;
        try
        {
            using var options = ActivityOptions.MakeBasic()!;
            if (OperatingSystem.IsAndroidVersionAtLeast(36))
                options.SetPendingIntentBackgroundActivityStartMode(BackgroundActivityStartMode.AllowIfVisible);
            else if (OperatingSystem.IsAndroidVersionAtLeast(34))
            {
#pragma warning disable CA1422
                options.SetPendingIntentBackgroundActivityStartMode(BackgroundActivityStartMode.Allowed);
#pragma warning restore CA1422
            }
            using var bundle = options.ToBundle();
            pending.Send(context, 0, null, null, null, null, bundle);
            return true;
        }
        catch (Exception e) when (e is PendingIntent.CanceledException or ActivityNotFoundException or Java.Lang.SecurityException)
        {
            return false;
        }
    }

    public override void OnListenerConnected()
    {
        base.OnListenerConnected();
        current = this;
        lock (Gate)
        {
            Messages.Clear();
            Notifications.Clear();
            BadgeSignals.Clear();
            ReplyActions.Clear();
            Threads.Clear();
        }
        foreach (var notification in GetActiveNotifications() ?? [])
            Capture(notification);
        Changed?.Invoke();
    }

    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        if (sbn is null)
            return;

        // Existing notification processing.
        Capture(sbn);
        Changed?.Invoke();

        // Optional spoken announcement.
        // Filtering, deduplication and errors are handled internally.
        NotificationWhispers.Handle(this, sbn);
    }

    public override void OnNotificationRemoved(StatusBarNotification? sbn)
    {
        if (sbn is null)
            return;
        lock (Gate)
        {
            Messages.Remove(sbn.Key!);
            Notifications.Remove(sbn.Key!);
            BadgeSignals.Remove(sbn.Key!);
            ReplyActions.Remove(sbn.Key!);
            Threads.Remove(sbn.Key!);
        }
        Changed?.Invoke();
    }

    public override void OnListenerDisconnected()
    {
        current = null;
        lock (Gate)
        {
            Messages.Clear();
            Notifications.Clear();
            BadgeSignals.Clear();
            ReplyActions.Clear();
            Threads.Clear();
        }
        Changed?.Invoke();
        base.OnListenerDisconnected();
    }

    private static void Capture(StatusBarNotification sbn)
    {
        var n = sbn.Notification;
        if (n is null)
            return;
        lock (Gate)
        {
            BadgeSignals[sbn.Key!] = new(sbn.Key!, sbn.PackageName!, sbn.GroupKey ?? sbn.Key!, n.Number,
                (n.Flags & NotificationFlags.GroupSummary) != 0, sbn.IsClearable);
            if (BadgeSignals.Count > 500)
                BadgeSignals.Remove(BadgeSignals.Keys.First());
        }
        if ((n.Flags & NotificationFlags.GroupSummary) != 0)
            return;
        var extras = n.Extras;
        var title = extras?.GetCharSequence("android.conversationTitle")?.ToString()
            ?? extras?.GetCharSequence(Notification.ExtraTitle)?.ToString() ?? "Message";
        var body = extras?.GetCharSequence(Notification.ExtraText)?.ToString() ?? (n.Category == Notification.CategoryMessage ? "New message" : "");
        title = title[..Math.Min(300, title.Length)];
        body = body[..Math.Min(4000, body.Length)];
        string thread = OperatingSystem.IsAndroidVersionAtLeast(26) ? n.ShortcutId ?? title : title;
        lock (Gate)
        {
            Threads[sbn.Key!] = thread;
            var reply = n.Actions?.FirstOrDefault(a => a.GetRemoteInputs()?.Any(i => i.AllowFreeFormInput) == true);
            if (reply is not null)
                ReplyActions[sbn.Key!] = reply;
            else
                ReplyActions.Remove(sbn.Key!);
        }
        if (current is { } service)
        {
            var state = new StateStore(service).Load();
            bool protectedSource = state.HideProtectedPreviews && state.Apps.Any(p => p.Key.StartsWith(sbn.PackageName + "/", StringComparison.Ordinal) && p.Value.BiometricLocked);
            if (state.KeepMessageHistory && !protectedSource && !state.ConversationExcludedApps.Contains(sbn.PackageName!) && (n.Category == Notification.CategoryMessage || extras?.ContainsKey("android.messages") == true))
            {
                try
                {
                    using var history = new MessageHistory(service);
                    bool added = false;
#pragma warning disable CA1422
                    foreach (var value in (extras?.GetParcelableArray("android.messages") ?? []).TakeLast(50))
#pragma warning restore CA1422
                        if (value is Bundle message && message.GetCharSequence("text")?.ToString() is { } text)
                        {
                            string sender = message.GetCharSequence("sender")?.ToString() ?? title;
                            history.Add(new(sbn.PackageName!, thread, title, sender, text, message.GetLong("time", sbn.PostTime), false));
                            added = true;
                        }
                    if (!added && body.Length > 0)
                        history.Add(new(sbn.PackageName!, thread, title, title, body, sbn.PostTime, false));
                }
                catch (Java.Lang.RuntimeException) { }
            }
        }
        lock (Gate)
        {
            var entry = new Conversation(sbn.Key!, sbn.PackageName!, title, body, sbn.PostTime, n.ContentIntent, sbn.IsClearable);
            Notifications[sbn.Key!] = entry;
            if (n.Category == Notification.CategoryMessage || extras?.ContainsKey("android.messages") == true)
                Messages[sbn.Key!] = entry;
            else
                Messages.Remove(sbn.Key!);
            if (Notifications.Count > 100)
                Notifications.Remove(Notifications.Values.MinBy(m => m.Time)!.Key);
            if (Messages.Count > 100)
                Messages.Remove(Messages.Values.MinBy(m => m.Time)!.Key);
        }
    }

    public static void Dismiss(string key)
    {
        try
        {
            current?.CancelNotification(key);
        }
        catch (Java.Lang.SecurityException) { }
    }
    public static bool Snooze(string key, long milliseconds)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26) || current is null)
            return false;
        try
        {
            current.SnoozeNotification(key, Math.Clamp(milliseconds, 60000, 86400000));
            return true;
        }
        catch (Java.Lang.RuntimeException) { return false; }
    }
    public static string Thread(Conversation notification)
    {
        lock (Gate)
            return Threads.GetValueOrDefault(notification.Key, notification.Title);
    }
    public static bool CanReply(string key)
    {
        lock (Gate)
            return ReplyActions.ContainsKey(key);
    }
    public static void CaptureAvailableHistory()
    {
        foreach (var item in current?.GetActiveNotifications() ?? [])
            Capture(item);
    }
    public static bool Reply(Context context, Conversation notification, string text)
    {
        if (((KeyguardManager?)context.GetSystemService(Context.KeyguardService))?.IsDeviceLocked == true)
            return false;
        Notification.Action? action;
        lock (Gate)
            ReplyActions.TryGetValue(notification.Key, out action);
        var inputs = action?.GetRemoteInputs()?.Where(i => i.AllowFreeFormInput).ToArray();
        if (action?.ActionIntent is null || inputs is not { Length: > 0 })
            return false;
        try
        {
            using var fill = new Intent();
            var results = new Bundle();
            foreach (var input in inputs)
                results.PutCharSequence(input.ResultKey, text);
            RemoteInput.AddResultsToIntent(inputs, fill, results);
            action.ActionIntent.Send(context, 0, fill);
            var saved = new StateStore(context).Load();
            bool protectedSource = saved.HideProtectedPreviews && saved.Apps.Any(p => p.Key.StartsWith(notification.Package + "/", StringComparison.Ordinal) && p.Value.BiometricLocked);
            if (saved.KeepMessageHistory && !protectedSource)
            {
                using var history = new MessageHistory(context);
                history.Add(new(notification.Package, Thread(notification), notification.Title, "You", text, DateTimeOffset.Now.ToUnixTimeMilliseconds(), true));
            }
            return true;
        }
        catch (Java.Lang.RuntimeException) { return false; }
    }
}
