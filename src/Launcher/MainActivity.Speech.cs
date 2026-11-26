
using Android.App;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.Services.Speech;

namespace LauncherApp;

public partial class MainActivity
{
    // ---------------------------------------------
    // RSS: Read a single article
    // ---------------------------------------------

    private void ReadArticleWithKokoro(CardLink article)
    {
        string title = article.Title.Trim();
        string detail = article.Detail.Trim();

        string text = string.IsNullOrWhiteSpace(detail)
            ? title
            : $"{title}. {detail}";

        if (string.IsNullOrWhiteSpace(text))
        {
            Toast(Localize(Resource.String.ui_this_story_has_no_readable_text));
            return;
        }

        KokoroReaderDialog.Show(this, text);
    }

    // ---------------------------------------------
    // RSS: Prepare a news briefing
    // ---------------------------------------------

    private void ReadLatestFeedsWithKokoro()
    {
        ReadFeedCache();

        var articles = Sources
            .Where(source => source.Enabled)
            .SelectMany(source =>
                feedCache.TryGetValue(source.Id, out var snapshot)
                    ? snapshot.Links
                    : Array.Empty<CardLink>())
            .DistinctBy(article => article.Url)
            .OrderByDescending(article => article.PublishedAt)
            .Take(Math.Clamp(state.FeedArticleCount, 1, 20))
            .ToArray();

        if (articles.Length == 0)
        {
            Toast(Localize(Resource.String.ui_no_rss_stories_are_available_refresh_your_feeds_first));
            return;
        }

        var parts = new List<string>
        {
            $"Here are your latest {articles.Length} stories."
        };

        for (int i = 0; i < articles.Length; i++)
        {
            var article = articles[i];

            string heading =
                $"Story {i + 1}. {article.Title}.";

            string detail = string.IsNullOrWhiteSpace(article.Detail)
                ? ""
                : $" {article.Detail}";

            parts.Add(heading + detail);
        }

        parts.Add("That concludes your news briefing.");

        KokoroReaderDialog.Show(
            this,
            string.Join("\n\n", parts));
    }

    // ---------------------------------------------
    // Notifications: Manual read-aloud options
    // ---------------------------------------------

    private void ShowNotificationSpeech(Conversation item)
    {
        if (!KokoroModelInstaller.IsInstalled(this))
        {
            OpenSpeechSettings(() => ShowNotificationSpeech(item));

            return;
        }

        var keyguard = (KeyguardManager?)
            GetSystemService(KeyguardService);

        if (keyguard?.IsDeviceLocked == true)
        {
            Toast(Localize(Resource.String.ui_unlock_your_phone_before_reading_messages));
            return;
        }

        // Reuse Launcher's existing biometric/app-lock
        // protection before exposing notification text.
        WithAppUnlock(item.Package, () =>
        {
            Choose(
                Resource.String.ui_read_notification,
                [
                    Localize(Resource.String.ui_announce_sender_only),
                    Localize(Resource.String.ui_read_message_content)
                ],
                selection =>
                {
                    if (selection is < 0 or > 1)
                        return;

                    string sender = item.Title.Trim();

                    if (string.IsNullOrWhiteSpace(sender) ||
                        sender.Equals(
                            "Message",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        sender = PackageLabel(item.Package);
                    }

                    string text;

                    if (selection == 0)
                    {
                        text = $"New notification from {sender}.";
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(item.Text))
                        {
                            Toast(Localize(Resource.String.ui_this_notification_has_no_readable_message));
                            return;
                        }

                        text = $"{item.Text}";
                    }

                    LauncherSpeech.Instance.Enqueue(
                        text,
                        SpeechPreferences.Load(this),
                        source: $"Notification: {sender}");

                    Toast(Localize(Resource.String.ui_added_to_kokoro_speech_queue));
                });
        });
    }
}
