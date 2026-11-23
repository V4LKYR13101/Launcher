using Android.Content;
using LauncherApp.Services.ImageSearch;
using SentenceTransformers.EmbeddingGemma2;

namespace LauncherApp.Services.DocumentSearch;

internal sealed class DocumentSearchService
{
    private const string ModelName = "embeddinggemma-2-text-270m.litertlm";
    internal const long DownloadBytes = 164_626_432;
    private static readonly ImageSearchModels.ModelFile[] ModelFiles =
    [
        new(ModelName, DownloadBytes, "2d079ee2f6f066b1f368e8d7c819f55214eaef1d0513b312321901f30ab286fb",
            "https://huggingface.co/litert-community/embeddinggemma-2-text-270m-litert-lm/resolve/9be6e8b90982095dc05c2bd162e4b954ee4dbac7/" + ModelName)
    ];

    internal enum Phase { Idle, Downloading, Scanning, Indexing, Removing }
    internal sealed record Progress(Phase Phase = Phase.Idle, long Downloaded = 0, int Done = 0, int Total = 0,
        DocumentIndex.Counts Counts = default, string? Error = null);
    private static DocumentSearchService? instance;
    private readonly Context context;
    private readonly ISharedPreferences settings;
    private readonly string directory, indexPath, modelPath;
    private readonly object sync = new();
    private readonly SemaphoreSlim work = new(1);
    private readonly SemaphoreSlim encoding = new(1);
    private CancellationTokenSource? operation;
    private SentenceEncoder? encoder;
    private volatile Progress progress;
    private volatile bool removing;
    private int revision;

    internal static DocumentSearchService For(Context context) =>
        LazyInitializer.EnsureInitialized(ref instance, () => new(context.ApplicationContext!));

    private DocumentSearchService(Context context)
    {
        this.context = context;
        settings = context.GetSharedPreferences("document-search", FileCreationMode.Private)!;
        directory = Path.Combine(context.NoBackupFilesDir!.AbsolutePath, "document-search");
        modelPath = Path.Combine(directory, ModelName);
        indexPath = Path.Combine(directory, "document-index-v1.sqlite");
        progress = new(Counts: new(settings.GetInt("indexed", 0), settings.GetInt("skipped", 0), settings.GetInt("passages", 0)));
    }

    internal bool Enabled => settings.GetBoolean("enabled", false);
    internal bool Ready => ImageSearchModels.InstalledBytes(directory, ModelFiles) == DownloadBytes;
    internal bool Searchable => Enabled && Ready && File.Exists(indexPath);
    internal int Revision => Volatile.Read(ref revision);
    internal Progress Status => removing ? progress with { Phase = Phase.Removing } : progress;
    internal long StorageBytes
    {
        get
        {
            try { return Directory.EnumerateFiles(directory).Sum(file => new FileInfo(file).Length); }
            catch (IOException) { return 0; }
        }
    }

    internal void SetEnabled(bool enabled)
    {
        if (removing)
            return;
        settings.Edit()!.PutBoolean("enabled", enabled)!.Apply();
        Interlocked.Increment(ref revision);
        if (enabled)
        {
            progress = progress with { Error = null };
            Resume();
        }
        else
        {
            Cancel();
            ImageSearchJob.CancelIfUnused(context);
            _ = ReleaseEncoder();
        }
    }

    internal void Resume()
    {
        if (!Enabled || removing)
            return;
        if (Ready && SharedStorage.CanRead(context))
            ImageSearchJob.Schedule(context, initial: !File.Exists(indexPath));
        else if (!Ready && progress is { Phase: Phase.Idle, Error: null })
            _ = Download();
    }

    internal void Cancel()
    {
        lock (sync)
            operation?.Cancel();
    }

    internal async Task Download()
    {
        if (Ready)
            return;
        await RunWork(Phase.Downloading, token => ImageSearchModels.Download(directory,
            bytes => progress = progress with { Downloaded = bytes }, token, ModelFiles, "EmbeddingGemma 2"));
        if (Enabled && Ready && SharedStorage.CanRead(context))
            ImageSearchJob.Schedule(context, initial: !File.Exists(indexPath));
    }

    internal async Task Index(CancellationToken outer = default)
    {
        if (!Ready || !SharedStorage.CanRead(context))
            return;
        await RunWork(Phase.Scanning, async token =>
        {
            var snapshot = await FileSearchCatalog.For(context).Refresh().ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            using var index = new DocumentIndex(indexPath);
            try
            {
                await index.Update(snapshot.Files, () => Enabled && SharedStorage.CanRead(context),
                    (done, total) => progress = progress with { Phase = Phase.Indexing, Done = done, Total = total },
                    EncodePassage, token).ConfigureAwait(false);
            }
            finally
            {
                var counts = index.ReadCounts();
                progress = progress with { Counts = counts };
                settings.Edit()!.PutInt("indexed", counts.Indexed)!.PutInt("skipped", counts.Skipped)!.PutInt("passages", counts.Passages)!.Apply();
                Interlocked.Increment(ref revision);
            }
        }, outer);
        await ReleaseEncoder();
    }

    private async Task RunWork(Phase phase, Func<CancellationToken, Task> action, CancellationToken outer = default)
    {
        if (!Enabled || removing || outer.IsCancellationRequested || !work.Wait(0))
            return;
        using var current = CancellationTokenSource.CreateLinkedTokenSource(outer);
        lock (sync)
            operation = current;
        progress = progress with { Phase = phase, Error = null, Downloaded = 0 };
        try
        {
            if (Enabled)
                await Task.Run(() => action(current.Token), current.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested)
        {
            if (phase == Phase.Downloading)
                progress = progress with { Error = "Download cancelled." };
        }
        catch (Exception error) when (IsSearchError(error) || error is HttpRequestException or OperationCanceledException)
        {
            Android.Util.Log.Warn("DocumentSearch", error.ToString());
            progress = progress with { Error = phase == Phase.Downloading
                ? "Download failed. Check your connection and available storage, then retry."
                : "Document indexing failed. Try indexing again." };
        }
        finally
        {
            lock (sync)
                operation = null;
            progress = progress with { Phase = Phase.Idle };
            work.Release();
        }
    }

    private async Task<float[]> EncodePassage(string passage, string title, CancellationToken token)
    {
        await encoding.WaitAsync(token).ConfigureAwait(false);
        try
        {
            encoder ??= await DocumentIndex.LoadEncoder(modelPath).ConfigureAwait(false);
            var vectors = await encoder.EncodeDocumentsAsync([passage], [title], token).ConfigureAwait(false);
            return vectors[0];
        }
        finally { encoding.Release(); }
    }

    internal async Task<string[]> Search(string query, Func<string, bool> isCandidate, CancellationToken token)
    {
        await encoding.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!Searchable || removing || !SharedStorage.CanRead(context))
                return [];
            using var index = new DocumentIndex(indexPath, readOnly: true);
            if (!index.HasPassages())
                return [];
            encoder ??= await DocumentIndex.LoadEncoder(modelPath).ConfigureAwait(false);
            var vectors = await encoder.EncodeQueriesAsync([query], cancellationToken: token).ConfigureAwait(false);
            return index.Search(vectors[0], isCandidate, token);
        }
        finally { encoding.Release(); }
    }

    internal async Task ReleaseEncoder()
    {
        await encoding.WaitAsync().ConfigureAwait(false);
        try
        {
            if (progress.Phase is not Phase.Indexing and not Phase.Scanning)
                CloseEncoder();
        }
        finally { encoding.Release(); }
    }

    internal async Task Remove()
    {
        if (removing)
            return;
        SetEnabled(false);
        removing = true;
        await work.WaitAsync().ConfigureAwait(false);
        await encoding.WaitAsync().ConfigureAwait(false);
        try
        {
            CloseEncoder();
            if (Directory.Exists(directory))
                await Task.Run(() => Directory.Delete(directory, recursive: true)).ConfigureAwait(false);
            settings.Edit()!.Remove("indexed")!.Remove("skipped")!.Remove("passages")!.Apply();
            progress = new();
            Interlocked.Increment(ref revision);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            progress = progress with { Error = "Couldn't remove the document search files. Try again." };
        }
        finally
        {
            removing = false;
            encoding.Release();
            work.Release();
        }
    }

    private void CloseEncoder()
    {
        encoder?.Dispose();
        encoder = null;
    }

    internal static bool IsSearchError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException
        or FormatException or InvalidOperationException or NotSupportedException or ArgumentException or TypeInitializationException
        or Java.Lang.RuntimeException or AggregateException;
}
