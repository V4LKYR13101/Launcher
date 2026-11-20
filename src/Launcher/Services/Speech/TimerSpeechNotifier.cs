using Android.App;
using Android.Util;

namespace LauncherApp.Services.Speech;

public static class TimerSpeechNotifier
{
    private const int WhisperVoiceId = 6; // af_nicole

    public static void TimerCompleted(
        string? message = null)
    {
        var context = Application.Context;

        if (!KokoroModelInstaller.IsInstalled(context))
        {
            Log.Warn(
                "LauncherTimer",
                "Kokoro is not installed. Skipping speech.");
            return;
        }

        string announcement = string.IsNullOrWhiteSpace(message)
            ? "Hey... your timer has finished. " +
              "Just a gentle reminder... " +
              "it's time to come back."
            : message.Trim();

        var options = new SpeechOptions
        {
            VoiceId = WhisperVoiceId,
            Speed = 0.88f,
            Volume = 0.65f,
            SilenceScale = 1.0f
        };

        try
        {
            var speech = LauncherSpeech.Instance;

            // Prioritise the timer over existing narration.
            speech.Stop();
            speech.Enqueue(
                announcement,
                options,
                "Timer");
        }
        catch (Exception ex)
        {
            Log.Error("LauncherTimer", ex.ToString());
        }
    }
}