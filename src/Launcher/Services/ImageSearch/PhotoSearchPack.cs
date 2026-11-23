using Android.Content;
using Microsoft.ML.OnnxRuntime;
using static LauncherApp.Services.ImageSearch.ImageSearchModels;

namespace LauncherApp.Services.ImageSearch;

internal sealed class PhotoSearchPack
{
    private const string HuggingFace = "https://huggingface.co/subhashhhhhh/";
    private const string OcrSource = HuggingFace + "lumen-ppocrv6-small/resolve/891518718e7870b86c9e4859404a149c5028e4de/";
    private static readonly ModelFile[] TextFiles =
    [
        new("det.onnx", 9_880_512, "d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e", OcrSource + "det.onnx"),
        new("rec_int8.onnx", 13_022_934, "67c0300e647071eac9a0b56de2a1a17dcf04928cab6e3c861049c677d267e686", OcrSource + "rec_int8.onnx"),
        new("rec.yml", 150_579, "ab078671bb49f06228eadccd34f1bb501e157f7a047095ffb943ba81512c77d1", OcrSource + "rec.yml")
    ];
    private static readonly ModelFile[] FaceFiles =
    [
        new("yunet.onnx", 229_738, "ebafce4e3c118d6554634be5c27ab333b4c047a9a8c3faf1d7cf93101c22f0f0",
            HuggingFace + "lumen-faces/resolve/009b05388d32e9d7d487fa2a78fb9e4a3c30b6f3/yunet.onnx"),
        new("edgeface_s.onnx", 14_786_593, "a33dd1563a9bed8f4e2ba1665cb22361e2b46c76014d11fd22eae53499195522",
            HuggingFace + "lumen-faces-edgeface/resolve/b34d2e9e5f90264294f83c9cfc8609885c58cca4/edgeface_s.onnx")
    ];
    private static PhotoSearchPack? text, faces;
    internal static PhotoSearchPack Text(Context context) => LazyInitializer.EnsureInitialized(ref text, () => new(context.ApplicationContext!, false));
    internal static PhotoSearchPack Faces(Context context) => LazyInitializer.EnsureInitialized(ref faces, () => new(context.ApplicationContext!, true));

    internal sealed record Progress(string? Activity = null, int? Percentage = null, int Indexed = 0, string? Error = null);
    private readonly SemaphoreSlim gate = new(1);
    private readonly SemaphoreSlim nameWrite = new(1);
    private readonly Context context;
    private readonly ISharedPreferences settings;
    private readonly ModelFile[] files;
    private readonly string directory, indexPath;
    private readonly bool people;
    private CancellationTokenSource? download;
    private volatile Progress status;
    private int revision;
    private volatile bool removing;

    private PhotoSearchPack(Context context, bool people)
    {
        this.context = context;
        this.people = people;
        files = people ? FaceFiles : TextFiles;
        string name = people ? "faces" : "ocr";
        directory = Path.Combine(context.NoBackupFilesDir!.AbsolutePath, "image-search-" + name);
        indexPath = Path.Combine(directory, "index-v1.sqlite");
        settings = context.GetSharedPreferences("image-search-" + name, FileCreationMode.Private)!;
        status = new(Indexed: settings.GetInt("indexed", 0));
    }

    internal bool Enabled => settings.GetBoolean("enabled", false);
    internal bool Ready => InstalledBytes(directory, files) == ModelBytes;
    internal bool Searchable => Enabled && Ready && File.Exists(indexPath);
    internal long ModelBytes => files.Sum(file => file.Bytes);
    internal long StorageBytes
    {
        get
        {
            try { return Directory.EnumerateFiles(directory).Sum(path => new FileInfo(path).Length); }
            catch (IOException) { return 0; }
        }
    }
    internal Progress Status => status;
    internal int Revision => Volatile.Read(ref revision);
    internal bool Downloading => download is not null;

    internal void SetEnabled(bool enabled)
    {
        if (removing)
            return;
        settings.Edit()!.PutBoolean("enabled", enabled)!.Apply();
        Interlocked.Increment(ref revision);
        if (enabled)
        {
            status = status with { Error = null };
            Resume();
        }
        else
        {
            CancelDownload();
            ImageSearchJob.CancelIfUnused(context);
        }
    }

    internal void Resume()
    {
        if (!Enabled)
            return;
        if (Ready && SharedStorage.CanRead(context))
            ImageSearchJob.Schedule(context, initial: !File.Exists(indexPath));
        else if (!Ready && status.Error is null && status.Activity is null)
            StartDownload();
    }

    internal void StartDownload()
    {
        if (Enabled && !Ready && !removing && gate.Wait(0))
            _ = Download();
    }

    internal void CancelDownload()
    {
        // All download controls and their completion run on Android's UI thread.
        download?.Cancel();
    }

    private async Task Download()
    {
        using var current = new CancellationTokenSource();
        download = current;
        status = status with { Activity = "Downloading model files…", Percentage = 0, Error = null };
        try
        {
            await Task.Run(() => ImageSearchModels.Download(directory,
                bytes => status = status with { Percentage = (int)(bytes * 100 / ModelBytes) }, current.Token, files,
                people ? "People search models" : "Image text model"));
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested)
        {
            status = status with { Error = "Download cancelled." };
        }
        catch (Exception error) when (error is IOException or InvalidDataException or HttpRequestException or OperationCanceledException or Java.Lang.RuntimeException)
        {
            Android.Util.Log.Warn("ImageSearch", error.ToString());
            status = status with { Error = "Download failed. Check your connection and free storage, then retry." };
        }
        finally
        {
            download = null;
            status = status with { Activity = null, Percentage = null };
            gate.Release();
        }
        if (!removing && Enabled && Ready && SharedStorage.CanRead(context))
            ImageSearchJob.Schedule(context, initial: !File.Exists(indexPath));
    }

    internal async Task Index(CancellationToken outer = default)
    {
        if (outer.IsCancellationRequested || !Enabled || !Ready || !SharedStorage.CanRead(context) || removing || !gate.Wait(0))
            return;
        status = status with { Activity = "Looking for new and changed images…", Percentage = null, Error = null };
        try
        {
            await Task.Run(() =>
            {
                using var index = new PhotoContentIndex(indexPath);
                try
                {
                    index.Update(context, directory, people, () => Enabled && SharedStorage.CanRead(context),
                        (done, total) => status = status with { Activity = $"Indexing images · {done:N0} of {total:N0}", Indexed = done,
                            Percentage = total > 0 ? (int)(done * 100L / total) : 100 }, outer);
                }
                finally
                {
                    int count = index.Count;
                    status = status with { Indexed = count };
                    settings.Edit()!.PutInt("indexed", count)!.Apply();
                    Interlocked.Increment(ref revision);
                }
            }, outer);
        }
        catch (OperationCanceledException) when (outer.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or InvalidDataException or OnnxRuntimeException or TypeInitializationException or Java.Lang.RuntimeException)
        {
            Android.Util.Log.Warn("ImageSearch", error.ToString());
            status = status with { Error = "Image indexing failed. Try indexing again." };
        }
        finally
        {
            status = status with { Activity = null, Percentage = null };
            gate.Release();
        }
    }

    internal async Task Remove()
    {
        if (removing)
            return;
        SetEnabled(false);
        removing = true;
        await gate.WaitAsync();
        await nameWrite.WaitAsync();
        status = new(Activity: "Removing model and index…");
        try
        {
            await Task.Run(() => { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); });
            settings.Edit()!.Remove("indexed")!.Apply();
            Interlocked.Increment(ref revision);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            status = status with { Error = "Couldn't remove the model files. Try again." };
        }
        finally
        {
            status = status with { Activity = null };
            removing = false;
            nameWrite.Release();
            gate.Release();
        }
    }

    internal string[] Match(string query, Func<string, bool> isCandidate, CancellationToken token)
    {
        if (!Searchable || !SharedStorage.CanRead(context))
            return [];
        using var index = new PhotoContentIndex(indexPath, readOnly: true);
        return people ? index.MatchPeople(query, isCandidate, token) : index.MatchText(query, isCandidate, token);
    }

    internal PhotoContentIndex.Person[] People(int offset)
    {
        if (!Searchable || !SharedStorage.CanRead(context))
            return [];
        using var index = new PhotoContentIndex(indexPath, readOnly: true);
        return index.People(offset);
    }

    internal async Task Name(long id, string name)
    {
        await nameWrite.WaitAsync();
        try
        {
            if (Searchable && SharedStorage.CanRead(context))
            {
                await Task.Run(() =>
                {
                    using var index = new PhotoContentIndex(indexPath);
                    index.Name(id, name);
                });
                Interlocked.Increment(ref revision);
            }
        }
        finally { nameWrite.Release(); }
    }
}
