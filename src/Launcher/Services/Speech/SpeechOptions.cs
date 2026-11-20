
namespace LauncherApp.Services.Speech;

public sealed record SpeechOptions
{
    public int VoiceId { get; init; } = 10;

    public float Speed { get; init; } = 1.0f;

    public float Volume { get; init; } = 1.0f;

    public float SilenceScale { get; init; } = 0.2f;
}
