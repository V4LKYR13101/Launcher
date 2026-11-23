using Android.Content;
using Microsoft.ML.OnnxRuntime;

namespace LauncherApp.Services.ImageSearch;

internal sealed class ImageSearchService
{
    internal enum Phase { Idle, Downloading, Scanning, Indexing, Removing }
    internal sealed record Progress(Phase Phase = Phase.Idle, long Downloaded = 0, ImageSearchIndex.Counts Counts = default, string? Error = null);

    private static ImageSearchService? instance;
    private readonly Context context;
    private readonly ISharedPreferences preferences;
    private readonly string root, modelDirectory, indexPath;
    private readonly SemaphoreSlim work = new(1);
    private readonly SemaphoreSlim queries = new(1);
    private readonly object operations = new();
    private CancellationTokenSource? operation;
    private volatile bool removing;
    private volatile Progress progress = new();
    private int indexRevision;
    private InferenceSession? textModel;
    private ClipTokenizer? tokenizer;

    internal static ImageSearchService For(Context context) => LazyInitializer.EnsureInitialized(ref instance, () => new(context.ApplicationContext!));

    private ImageSearchService(Context context)
    {
        this.context = context;
        preferences = context.GetSharedPreferences("image-search", FileCreationMode.Private)!;
        root = Path.Combine(context.NoBackupFilesDir!.AbsolutePath, "image-search");
        modelDirectory = Path.Combine(root, "clip-vit-b32-5eca4f3");
        indexPath = Path.Combine(root, "clip-vit-b32-index-v1.sqlite");
        progress = new(Counts: new(preferences.GetInt("processed", 0), preferences.GetInt("total", 0), preferences.GetInt("skipped", 0)));
    }

    internal bool Enabled => preferences.GetBoolean("enabled", false);
    internal long ModelBytes => ImageSearchModels.InstalledBytes(modelDirectory);
    internal bool Ready => ModelBytes == ImageSearchModels.TotalBytes;
    internal bool HasIndex => File.Exists(indexPath);
    internal int IndexRevision => Volatile.Read(ref indexRevision);
    internal long StorageBytes => SumFileSizes(modelDirectory) + new[] { indexPath, indexPath + "-wal", indexPath + "-shm" }.Sum(FileBytes);
    internal Progress Status => removing ? progress with { Phase = Phase.Removing } : progress;

    private static long SumFileSizes(string directory)
    {
        try { return Directory.EnumerateFiles(directory).Sum(FileBytes); }
        catch (DirectoryNotFoundException) { return 0; }
    }

    private static long FileBytes(string path)
    {
        try { return new FileInfo(path).Length; }
        catch (FileNotFoundException) { return 0; }
        catch (DirectoryNotFoundException) { return 0; }
    }

    internal void SetEnabled(bool enabled)
    {
        if (enabled && removing)
            return;
        preferences.Edit()!.PutBoolean("enabled", enabled)!.Apply();
        if (enabled)
        {
            progress = progress with { Error = null };
            Resume();
        }
        else
        {
            Cancel();
            ImageSearchJob.CancelIfUnused(context);
            _ = ReleaseText();
        }
    }

    internal void Resume()
    {
        if (!Enabled)
            return;
        if (!Ready && progress.Error is null)
            _ = Prepare();
        else if (Ready && SharedStorage.CanRead(context))
            ImageSearchJob.Schedule(context, initial: !HasIndex);
    }

    internal void Cancel()
    {
        lock (operations)
            operation?.Cancel();
    }

    internal async Task Prepare()
    {
        await RunWork(Phase.Downloading, async token =>
        {
            await ImageSearchModels.Download(modelDirectory, bytes => progress = progress with { Downloaded = bytes }, token);
        });
        if (Enabled && Ready && SharedStorage.CanRead(context))
            ImageSearchJob.Schedule(context, initial: !HasIndex);
    }

    internal Task Index(CancellationToken token = default) => RunWork(Phase.Scanning, current =>
    {
        if (Ready && SharedStorage.CanRead(context))
        {
            using var index = new ImageSearchIndex(indexPath);
            try
            {
                index.Update(context, modelDirectory, counts => progress = progress with { Phase = Phase.Indexing, Counts = counts }, current);
            }
            finally
            {
                var stored = index.ReadCounts();
                var counts = new ImageSearchIndex.Counts(stored.Processed, Math.Max(stored.Processed, progress.Counts.Total), stored.Skipped);
                progress = progress with { Counts = counts };
                SaveCounts(counts);
                Interlocked.Increment(ref indexRevision);
            }
        }
        return Task.CompletedTask;
    }, token);

    private async Task RunWork(Phase phase, Func<CancellationToken, Task> action, CancellationToken outer = default)
    {
        if (!Enabled || outer.IsCancellationRequested || !work.Wait(0))
            return;

        using var current = CancellationTokenSource.CreateLinkedTokenSource(outer);
        lock (operations)
            operation = current;
        progress = progress with { Phase = phase, Error = null };
        try
        {
            if (Enabled)
                await Task.Run(() => action(current.Token), current.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or HttpRequestException or InvalidDataException or
            OperationCanceledException or OnnxRuntimeException or TypeInitializationException or Java.Lang.RuntimeException)
        {
            Android.Util.Log.Warn("ImageSearch", error.ToString());
            string message = phase == Phase.Downloading ? "Model download failed. Check your connection and free storage, then retry."
                : "Image indexing failed. Try indexing again.";
            progress = progress with { Error = message };
        }
        finally
        {
            lock (operations)
                operation = null;
            progress = progress with { Phase = Phase.Idle };
            work.Release();
            if (phase == Phase.Downloading && current.IsCancellationRequested && Enabled && !Ready)
                _ = Prepare();
        }
    }

    internal async Task Remove()
    {
        if (removing)
            return;
        removing = true;
        SetEnabled(false);
        await work.WaitAsync().ConfigureAwait(false);
        progress = progress with { Phase = Phase.Removing, Error = null };
        await queries.WaitAsync().ConfigureAwait(false);
        try
        {
            CloseText();
            if (Directory.Exists(root))
                await Task.Run(() => Directory.Delete(root, recursive: true)).ConfigureAwait(false);
            SaveCounts(default);
            progress = new(Phase.Removing);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Android.Util.Log.Warn("ImageSearch", error.ToString());
            progress = progress with { Error = "Couldn't remove the image search data. Try again." };
        }
        finally
        {
            progress = progress with { Phase = Phase.Idle };
            removing = false;
            queries.Release();
            work.Release();
        }
    }

    private void SaveCounts(ImageSearchIndex.Counts counts) => preferences.Edit()!
        .PutInt("processed", counts.Processed)!.PutInt("total", counts.Total)!.PutInt("skipped", counts.Skipped)!.Apply();

    internal async Task<string[]> Search(string query, Func<string, bool> isCandidate, CancellationToken token)
    {
        await queries.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!Enabled || !Ready || !HasIndex || !SharedStorage.CanRead(context))
                return [];
            textModel ??= ClipEncoder.Open(modelDirectory, ImageSearchModels.Textual);
            tokenizer ??= new ClipTokenizer(modelDirectory);
            float[] vector = ClipEncoder.Text(textModel, tokenizer, query);
            token.ThrowIfCancellationRequested();
            using var index = new ImageSearchIndex(indexPath, readOnly: true);
            return index.Match(vector, isCandidate, token);
        }
        finally { queries.Release(); }
    }

    internal async Task ReleaseText()
    {
        await queries.WaitAsync().ConfigureAwait(false);
        try { CloseText(); }
        finally { queries.Release(); }
    }

    private void CloseText()
    {
        textModel?.Dispose();
        textModel = null;
        tokenizer = null;
    }
}
