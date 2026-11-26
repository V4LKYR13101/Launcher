using Android.Content;

namespace LauncherApp.Services.Speech;

public static class SpeechPreferences
{
    private const string Name = "kokoro-speech";

    public static SpeechOptions Load(Context context)
    {
        var preferences = context.GetSharedPreferences(
            Name,
            FileCreationMode.Private);

        if (preferences == null)
            return new SpeechOptions();

        return new SpeechOptions
        {
            VoiceId = preferences.GetInt("voice", 10),

            Speed = preferences.GetFloat(
                "speed", 1.0f),

            Volume = preferences.GetFloat(
                "volume", 1.0f),

            SilenceScale = preferences.GetFloat(
                "silence", 0.2f)
        };
    }

    public static void Save(
        Context context,
        SpeechOptions options)
    {
        var preferences = context.GetSharedPreferences(
            Name,
            FileCreationMode.Private);

        if (preferences == null)
            return;

        using var editor = preferences.Edit();
        if (editor is null)
            return;

        editor.PutInt("voice", options.VoiceId);
        editor.PutFloat("speed", options.Speed);
        editor.PutFloat("volume", options.Volume);
        editor.PutFloat(
            "silence", options.SilenceScale);

        editor.Apply();
    }
}
