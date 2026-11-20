
using Android.App;
using Android.Content;
using Android.Service.Notification;
using Android.Util;
using LauncherApp.Services;

namespace LauncherApp.Services.Speech;

public static class NotificationWhisperPreferences
{
    private const string PreferencesName = "launcher-notification-whispers";

    private static ISharedPreferences Preferences(Context context) =>
        context.GetSharedPreferences(
            PreferencesName,
            FileCreationMode.Private
        )!;

    public static bool Enabled(Context context) =>
        Preferences(context).GetBoolean("enabled", false);

    public static void SetEnabled(Context context, bool enabled)
    {
        Preferences(context)
            .Edit()!
            .PutBoolean("enabled", enabled)!
            .Apply();
    }

    public static bool MessagesOnly(Context context) =>
        Preferences(context).GetBoolean("messages-only", true);

    public static void SetMessagesOnly(Context context, bool value)
    {
        Preferences(context)
            .Edit()!
            .PutBoolean("messages-only", value)!
            .Apply();
    }

    public static bool IsPackageEnabled(Context context, string package) =>
        Preferences(context).GetBoolean("package:" + package, false);

    public static void SetPackageEnabled(
        Context context,
        string package,
        bool enabled)
    {
        Preferences(context)
            .Edit()!
            .PutBoolean("package:" + package, enabled)!
            .Apply();
    }

    public static string GetTitleFilter(Context context, string package) =>
        Preferences(context).GetString("title:" + package, "") ?? "";

    public static void SetTitleFilter(
        Context context,
        string package,
        string filter)
    {
        Preferences(context)
            .Edit()!
            .PutString("title:" + package, filter.Trim())!
            .Apply();
    }

    public static bool Allows(
        Context context,
        string package,
        string title)
    {
        if (!Enabled(context) || !IsPackageEnabled(context, package))
            return false;

        string filter = GetTitleFilter(context, package);

        return filter.Length == 0 ||
               title.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}

public static class NotificationWhispers
{
    private const int NicoleVoiceId = 6;
    private const int CooldownSeconds = 8;

    private static readonly object Gate = new();

    private static readonly Dictionary<string, int> Announced = new();

    private static DateTimeOffset _lastAnnouncement =
        DateTimeOffset.MinValue;

    public static void Handle(
        Context context,
        StatusBarNotification notification)
    {
        try
        {
            HandleCore(context, notification);
        }
        catch (Exception ex)
        {
            Log.Warn("LauncherWhisper", ex.ToString());
        }
    }

    private static void HandleCore(
        Context context,
        StatusBarNotification sbn)
    {
        if (!NotificationWhisperPreferences.Enabled(context))
            return;

        var notification = sbn.Notification;

        if (notification is null)
            return;

        string package = sbn.PackageName ?? "";

        if (package.Length == 0 || package == context.PackageName)
            return;

        if (!NotificationWhisperPreferences.IsPackageEnabled(
                context, package))
            return;

        if ((notification.Flags & NotificationFlags.GroupSummary) != 0)
            return;

        if ((notification.Flags & NotificationFlags.OngoingEvent) != 0)
            return;

        var extras = notification.Extras;

        bool isMessage =
            notification.Category == Notification.CategoryMessage ||
            extras?.ContainsKey("android.messages") == true;

        if (NotificationWhisperPreferences.MessagesOnly(context) &&
            !isMessage)
            return;

        // ExtraTitle is generally closer to the sender for messages.
        string title =
            extras?.GetCharSequence(Notification.ExtraTitle)?.ToString()
            ?? extras?.GetCharSequence("android.conversationTitle")?.ToString()
            ?? "";

        title = CleanSpeech(title);

        if (!NotificationWhisperPreferences.Allows(
                context, package, title))
            return;

        // Respect Launcher's protected-app preview setting.
        var state = new StateStore(context).Load();

        bool protectedSource =
            state.HideProtectedPreviews &&
            state.Apps.Any(app =>
                app.Key.StartsWith(
                    package + "/",
                    StringComparison.Ordinal) &&
                app.Value.BiometricLocked);

        if (protectedSource)
            return;

        if (!KokoroModelInstaller.IsInstalled(context))
            return;

        // Use message content only to identify changed notifications.
        // It is never included in the spoken announcement.
        string content =
            extras?.GetCharSequence(Notification.ExtraText)?.ToString()
            ?? "";

        string key = sbn.Key ?? package;
        int fingerprint = HashCode.Combine(
            sbn.PostTime,
            title,
            content);

        var now = DateTimeOffset.UtcNow;

        lock (Gate)
        {
            if (Announced.TryGetValue(key, out int previous) &&
                previous == fingerprint)
                return;

            if (now - _lastAnnouncement <
                TimeSpan.FromSeconds(CooldownSeconds))
                return;

            Announced[key] = fingerprint;
            _lastAnnouncement = now;

            if (Announced.Count > 256)
                Announced.Remove(Announced.Keys.First());
        }

        string announcement = isMessage
            ? title.Length > 0
                ? $"New message from {title}."
                : "You have a new message."
            : title.Length > 0
                ? $"Notification. {title}."
                : "You have a new notification.";

        var options = new SpeechOptions
        {
            VoiceId = NicoleVoiceId,
            Speed = 0.88f,
            Volume = 0.60f,
            SilenceScale = 1.0f
        };

        // Queue the whisper without cancelling other speech.
        LauncherSpeech.Instance.Enqueue(
            announcement,
            options,
            "Notification whisper");
    }

    private static string CleanSpeech(string text)
    {
        string cleaned = string.Join(
            " ",
            text.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

        return cleaned.Length > 80
            ? cleaned[..80]
            : cleaned;
    }
}
