
using System.Threading.Channels;
using Android.Content;
using Android.Media;
using Android.Util;
using SherpaOnnx;

namespace LauncherApp.Services.Speech;

public sealed class KokoroSpeechService
{
    private sealed record SpeechRequest(
        string Text,
        SpeechOptions Options,
        string Source,
        long Epoch);

    private sealed record PcmChunk(
        byte[] Data,
        int SampleRate);

    private readonly Context _context;
    private readonly Channel<SpeechRequest> _jobs;
    private readonly object _gate = new();
    private readonly Task _worker;

    private OfflineTts? _tts;
    private CancellationTokenSource? _activeCts;
    private AudioTrack? _track;

    private int _paused;
    private int _skipRequested;
    private long _epoch;
    private string _currentSource = "";

    public event Action<string>? StatusChanged;

    public KokoroSpeechService(Context context)
    {
        _context = context.ApplicationContext ?? context;

        _jobs = Channel.CreateUnbounded<SpeechRequest>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        _worker = Task.Run(ProcessQueueAsync);
    }

    public bool IsPaused =>
        Volatile.Read(ref _paused) != 0;

    public string CurrentSource
    {
        get
        {
            lock (_gate)
                return _currentSource;
        }
    }

    public void Enqueue(
        string text,
        SpeechOptions? options = null,
        string source = "Launcher")
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        options ??= new SpeechOptions();

        if (options.VoiceId < 0 ||
            options.Speed <= 0 ||
            options.Volume < 0 ||
            options.Volume > 1 ||
            options.SilenceScale < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options));
        }

        lock (_gate)
        {
            _jobs.Writer.TryWrite(
                new SpeechRequest(
                    text,
                    options,
                    source,
                    _epoch));
        }

        Notify(Localize(Resource.String.ui_queued_value, source));
    }

    public void Pause()
    {
        lock (_gate)
        {
            Volatile.Write(ref _paused, 1);

            try
            {
                _track?.Pause();
            }
            catch (InvalidOperationException)
            {
                // Track is already stopping.
            }
        }

        Notify(Localize(Resource.String.ui_speech_paused));
    }

    public void Resume()
    {
        lock (_gate)
        {
            Volatile.Write(ref _paused, 0);

            try
            {
                _track?.Play();
            }
            catch (InvalidOperationException)
            {
                // No playable track yet.
            }
        }

        Notify(Localize(Resource.String.ui_speech_resumed));
    }

    public void Skip()
    {
        Interlocked.Exchange(ref _skipRequested, 1);
        Notify(Localize(Resource.String.ui_skipping_current_chunk));
    }

    public void Stop()
    {
        lock (_gate)
        {
            _epoch++;

            _activeCts?.Cancel();

            Volatile.Write(ref _paused, 0);
            Interlocked.Exchange(ref _skipRequested, 0);

            try
            {
                _track?.Pause();
                _track?.Flush();
            }
            catch (InvalidOperationException)
            {
                // Playback already stopped.
            }

            while (_jobs.Reader.TryRead(out _))
            {
                // Discard waiting requests.
            }
        }

        Notify(Localize(Resource.String.ui_speech_stopped));
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var request in
            _jobs.Reader.ReadAllAsync())
        {
            using var cts = new CancellationTokenSource();

            lock (_gate)
            {
                if (request.Epoch != _epoch)
                    continue;

                _activeCts = cts;
                _currentSource = request.Source;
            }

            try
            {
                await SpeakRequestAsync(
                    request, cts.Token);
            }
            catch (OperationCanceledException)
                when (cts.IsCancellationRequested)
            {
                Notify(Localize(Resource.String.ui_reading_cancelled));
            }
            catch (Exception ex)
            {
                Log.Error("LauncherKokoro", ex.ToString());
                Notify(Localize(Resource.String.ui_speech_error_value, ex.Message));
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_activeCts, cts))
                    {
                        _activeCts = null;
                        _currentSource = "";
                    }
                }
            }
        }
    }

    private OfflineTts GetModel()
    {
        if (_tts != null)
            return _tts;

        var root = _context
            .GetExternalFilesDir(null)?.AbsolutePath
            ?? throw new DirectoryNotFoundException(
                "Android app files directory unavailable.");

        string folder = KokoroModelPaths.FindInstalled(_context)
    ?? throw new DirectoryNotFoundException(
        "Kokoro model is not installed.");

        var model = Path.Combine(folder, "model.onnx");
        var voices = Path.Combine(folder, "voices.bin");
        var tokens = Path.Combine(folder, "tokens.txt");
        var dataDir = Path.Combine(
            folder, "espeak-ng-data");

        var lexiconUs = Path.Combine(
            folder, "lexicon-us-en.txt");

        var lexiconZh = Path.Combine(
            folder, "lexicon-zh.txt");

        foreach (var file in new[]
        {
            model, voices, tokens, lexiconUs, lexiconZh
        })
        {
            if (!File.Exists(file))
                throw new FileNotFoundException(
                    "Missing Kokoro model asset", file);
        }

        if (!Directory.Exists(dataDir))
            throw new DirectoryNotFoundException(dataDir);

        Notify(Localize(Resource.String.ui_loading_kokoro_model));

        var config = new OfflineTtsConfig();

        config.Model.Kokoro.Model = model;
        config.Model.Kokoro.Voices = voices;
        config.Model.Kokoro.Tokens = tokens;
        config.Model.Kokoro.DataDir = dataDir;

        config.Model.Kokoro.Lexicon =
            string.Join(",", lexiconUs, lexiconZh);

        config.Model.NumThreads = 3;
        config.Model.Provider = "cpu";
        config.Model.Debug = 0;
        config.MaxNumSentences = 1;

        var engine = new OfflineTts(config);

        if (engine.NumSpeakers <= 0)
        {
            engine.Dispose();

            throw new InvalidOperationException(
                "Kokoro failed to initialise.");
        }

        _tts = engine;

        Notify(
            Localize(Resource.String.ui_kokoro_ready_value_voices, _tts.NumSpeakers));

        return _tts;
    }

    private async Task SpeakRequestAsync(
        SpeechRequest request,
        CancellationToken token)
    {
        var chunks = SpeechTextChunker.Split(request.Text);

        if (chunks.Count == 0)
            return;

        Notify(
            Localize(Resource.String.ui_reading_value_value_chunks, request.Source, chunks.Count));

        var ready = Channel.CreateBounded<PcmChunk>(
            new BoundedChannelOptions(2)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(token);

        var ct = linkedCts.Token;

        Task producer = Task.Run(
            () => GenerateChunksAsync(
                chunks, request.Options, ready.Writer, ct));

        AudioTrack? track = null;
        long submittedFrames = 0;

        try
        {
            await foreach (var chunk in
                ready.Reader.ReadAllAsync(ct))
            {
                ct.ThrowIfCancellationRequested();

                if (track == null)
                {
                    track = CreateAudioTrack(
                        chunk.SampleRate);

                    lock (_gate)
                    {
                        _track = track;

                        if (Volatile.Read(ref _paused) == 0)
                            track.Play();
                    }
                }
                else if (track.SampleRate !=
                         chunk.SampleRate)
                {
                    throw new InvalidOperationException(
                        "Kokoro sample rate changed.");
                }

                var result = await WriteChunkAsync(
                    track, chunk.Data, ct);

                if (result.Skipped)
                {
                    submittedFrames =
                        (uint)track.PlaybackHeadPosition;
                }
                else
                {
                    submittedFrames += result.Frames;
                }
            }

            await producer;

            if (track != null)
            {
                await DrainAsync(
                    track, submittedFrames, ct);
            }

            Notify(Localize(Resource.String.ui_finished_value, request.Source));
        }
        finally
        {
            linkedCts.Cancel();

            // A native inference call cannot be forcibly
            // interrupted. Wait for it to return safely.
            try
            {
                await producer;
            }
            catch
            {
                // The channel reports producer errors.
            }

            lock (_gate)
            {
                if (ReferenceEquals(_track, track))
                    _track = null;
            }

            if (track != null)
            {
                try
                {
                    track.Stop();
                    track.Flush();
                }
                catch (InvalidOperationException)
                {
                    // Already stopped.
                }

                track.Release();
                track.Dispose();
            }

            Interlocked.Exchange(
                ref _skipRequested, 0);
        }
    }

    private async Task GenerateChunksAsync(
        IReadOnlyList<string> chunks,
        SpeechOptions options,
        ChannelWriter<PcmChunk> writer,
        CancellationToken token)
    {
        try
        {
            var tts = GetModel();

            if (options.VoiceId >= tts.NumSpeakers)
                throw new ArgumentOutOfRangeException(
                    nameof(options.VoiceId));

            var generation = new OfflineTtsGenerationConfig
            {
                Sid = options.VoiceId,
                Speed = options.Speed,
                SilenceScale = options.SilenceScale
            };

            for (int index = 0; index < chunks.Count; index++)
            {
                token.ThrowIfCancellationRequested();

                // Only this worker calls the native engine.
                var audio = tts.GenerateWithConfig(
                    chunks[index], generation, null);

                float[] samples;
                int sampleRate;

                try
                {
                    samples = audio.Samples;
                    sampleRate = audio.SampleRate;
                }
                finally
                {
                    audio.Dispose();
                }

                token.ThrowIfCancellationRequested();

                if (samples.Length == 0 ||
                    sampleRate <= 0)
                {
                    throw new InvalidOperationException(
                        $"Empty audio for chunk {index + 1}");
                }

                byte[] pcm = ConvertToPcm16(
                    samples, options.Volume);

                await writer.WriteAsync(
                    new PcmChunk(pcm, sampleRate),
                    token);

                Notify(
                    Localize(Resource.String.ui_generated_value_value, index + 1, chunks.Count));
            }

            writer.TryComplete();
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
            writer.TryComplete();
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
        }
    }

    private static byte[] ConvertToPcm16(
        float[] samples,
        float volume)
    {
        var data = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            float value = Math.Clamp(
                samples[i] * volume, -1f, 1f);

            short pcm = (short)Math.Round(
                value * 32767f);

            data[i * 2] = (byte)pcm;
            data[i * 2 + 1] =
                (byte)((ushort)pcm >> 8);
        }

        return data;
    }

    private static AudioTrack CreateAudioTrack(
        int sampleRate)
    {
        int minBuffer = AudioTrack.GetMinBufferSize(
            sampleRate,
            ChannelOut.Mono,
            Android.Media.Encoding.Pcm16bit);

        if (minBuffer <= 0)
            throw new InvalidOperationException(
                "Could not initialise Android audio output.");

        int bufferSize = Math.Max(
            minBuffer, sampleRate * 2);

        // Keep the same streaming AudioTrack setup on all supported Android versions.
#pragma warning disable CA1422
        return new AudioTrack(
            Android.Media.Stream.Music,
            sampleRate,
            ChannelOut.Mono,
            Android.Media.Encoding.Pcm16bit,
            bufferSize,
            AudioTrackMode.Stream);
#pragma warning restore CA1422
    }

    private readonly record struct WriteResult(
        long Frames,
        bool Skipped);

    private async Task<WriteResult> WriteChunkAsync(
        AudioTrack track,
        byte[] data,
        CancellationToken token)
    {
        int offset = 0;
        long framesWritten = 0;

        while (offset < data.Length)
        {
            token.ThrowIfCancellationRequested();

            if (Interlocked.Exchange(
                ref _skipRequested, 0) != 0)
            {
                lock (_gate)
                {
                    track.Pause();
                    track.Flush();

                    if (Volatile.Read(ref _paused) == 0)
                        track.Play();
                }

                return new WriteResult(0, true);
            }

            await WaitIfPausedAsync(token);

            int count = Math.Min(
                8192, data.Length - offset);

            int written = track.Write(
                data,
                offset,
                count,
                Android.Media.WriteMode.NonBlocking);

            if (written < 0)
                throw new IOException(
                    $"AudioTrack write failed: {written}");

            if (written == 0)
            {
                await Task.Delay(10, token);
                continue;
            }

            offset += written;
            framesWritten += written / 2;
        }

        return new WriteResult(
            framesWritten, false);
    }

    private async Task DrainAsync(
        AudioTrack track,
        long submittedFrames,
        CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();

            if (Interlocked.Exchange(
                ref _skipRequested, 0) != 0)
            {
                lock (_gate)
                {
                    track.Pause();
                    track.Flush();
                }

                return;
            }

            await WaitIfPausedAsync(token);

            long played =
                (uint)track.PlaybackHeadPosition;

            if (played >= submittedFrames)
                return;

            await Task.Delay(25, token);
        }
    }

    private async Task WaitIfPausedAsync(
        CancellationToken token)
    {
        while (Volatile.Read(ref _paused) != 0)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(40, token);
        }
    }

    private void Notify(string message)
    {
        Log.Debug("LauncherKokoro", message);

        try
        {
            StatusChanged?.Invoke(message);
        }
        catch (Exception ex)
        {
            Log.Warn(
                "LauncherKokoro",
                $"Status listener failed: {ex.Message}");
        }
    }
}


// One engine shared by Launcher pages.
public static class LauncherSpeech
{
    private static readonly Lazy<KokoroSpeechService>
        Shared = new(() => new KokoroSpeechService(
            Android.App.Application.Context));

    public static KokoroSpeechService Instance =>
        Shared.Value;
}
