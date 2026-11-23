using Android.Content;
using LauncherApp.Services.DocumentSearch;
using LauncherApp.Services.ImageSearch;

namespace LauncherApp.Services;

internal sealed record SearchFile(string Path, string Name, string Folder)
{
    public bool IsImage => ImageSearchIndex.IsSupported(Path) || SharedFileProvider.MimeType(Path).StartsWith("image/", StringComparison.Ordinal);
    public bool IsDocument => DocumentText.Supports(Path) || System.IO.Path.GetExtension(Path).ToLowerInvariant() is
        ".doc" or ".docm" or ".rtf" or ".odt" or ".xls" or ".xlsx" or ".xlsm" or ".ods" or ".tsv" or
        ".ppt" or ".pptx" or ".pptm" or ".odp" or ".epub" or ".htm" or ".html";
}

internal sealed class FileSearchCatalog
{
    private const string Format = "Launcher file catalog 1";
    private static FileSearchCatalog? instance;
    private readonly Context context;
    private readonly string path;
    private readonly object sync = new();
    private Snapshot? latest;
    private Task<Snapshot>? scan;

    internal sealed class Snapshot(string[] roots, SearchFile[] files)
    {
        private readonly Lazy<Dictionary<string, SearchFile>> images = new(() => files
            .Where(file => ImageSearchIndex.IsSupported(file.Path)).ToDictionary(file => file.Path, StringComparer.Ordinal));
        private readonly Lazy<Dictionary<string, SearchFile>> documents = new(() => files
            .Where(file => DocumentText.Supports(file.Path)).ToDictionary(file => file.Path, StringComparer.Ordinal));

        internal string[] Roots { get; } = roots;
        internal SearchFile[] Files { get; } = files;
        internal Dictionary<string, SearchFile> Images => images.Value;
        internal Dictionary<string, SearchFile> Documents => documents.Value;
    }

    internal static FileSearchCatalog For(Context context) =>
        LazyInitializer.EnsureInitialized(ref instance, () => new(context.ApplicationContext!));

    private FileSearchCatalog(Context context)
    {
        this.context = context;
        path = Path.Combine(context.NoBackupFilesDir!.AbsolutePath, "file-search-catalog-v1");
    }

    internal Task<Snapshot?> Cached() => Task.Run(() =>
    {
        if (!SharedStorage.CanRead(context))
            return null;
        var snapshot = GetSaved();
        if (snapshot is null)
            return null;
        var roots = MountedRoots();
        return RestrictToRoots(snapshot, roots);
    });

    internal Task<Snapshot> Refresh()
    {
        lock (sync)
            return scan is { IsCompleted: false } ? scan : scan = Task.Run(Scan);
    }

    private Snapshot Scan()
    {
        if (!SharedStorage.CanRead(context))
            throw new UnauthorizedAccessException();

        var volumes = SharedStorage.MountedVolumes(context).ToArray();
        var roots = volumes.Select(volume => volume.Root).Order(StringComparer.Ordinal).ToArray();
        var files = SharedStorage.Files(volumes, CancellationToken.None)
            .Select(file => new SearchFile(file.Path, file.Name, file.Folder))
            .DistinctBy(file => file.Path, StringComparer.Ordinal)
            .OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(file => file.Folder, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();

        if (!SharedStorage.CanRead(context))
            throw new UnauthorizedAccessException();

        var scanned = RestrictToRoots(new(roots, files), MountedRoots());
        var previous = GetSaved();
        if (previous is not null && scanned.Roots.AsSpan().SequenceEqual(previous.Roots) &&
            scanned.Files.AsSpan().SequenceEqual(previous.Files))
            return previous;

        try { Save(scanned); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Android.Util.Log.Warn("FileSearch", error.ToString());
        }
        Volatile.Write(ref latest, scanned);
        return scanned;
    }

    private string[] MountedRoots() =>
        SharedStorage.MountedVolumes(context).Select(volume => volume.Root).Order(StringComparer.Ordinal).ToArray();

    private static Snapshot RestrictToRoots(Snapshot snapshot, string[] roots)
    {
        if (snapshot.Roots.AsSpan().SequenceEqual(roots))
            return snapshot;
        string[] available = snapshot.Roots.Intersect(roots, StringComparer.Ordinal).ToArray();
        return new(available, snapshot.Files.Where(file => available.Any(root => SharedStorage.Contains(root, file.Path))).ToArray());
    }

    private Snapshot? GetSaved()
    {
        var existing = Volatile.Read(ref latest);
        if (existing is not null)
            return existing;
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (reader.ReadString() != Format)
                return null;
            int volumeCount = reader.ReadInt32();
            if (volumeCount < 0 || volumeCount > (stream.Length - stream.Position) / 2)
                return null;
            var roots = new string[volumeCount];
            for (int i = 0; i < roots.Length; i++)
                roots[i] = reader.ReadString();
            int fileCount = reader.ReadInt32();
            if (fileCount < 0 || fileCount > (stream.Length - stream.Position) / 4)
                return null;
            var files = new SearchFile[fileCount];
            for (int i = 0; i < files.Length; i++)
            {
                string filePath = reader.ReadString();
                files[i] = new(filePath, Path.GetFileName(filePath), reader.ReadString());
            }
            var restored = new Snapshot(roots, files);
            return Interlocked.CompareExchange(ref latest, restored, null) ?? restored;
        }
        catch (FileNotFoundException) { return null; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
        {
            Android.Util.Log.Warn("FileSearch", error.ToString());
            return null;
        }
    }

    private void Save(Snapshot snapshot)
    {
        string temporary = path + ".tmp";
        try
        {
            using (var writer = new BinaryWriter(File.Create(temporary)))
            {
                writer.Write(Format);
                writer.Write(snapshot.Roots.Length);
                foreach (string root in snapshot.Roots)
                    writer.Write(root);
                writer.Write(snapshot.Files.Length);
                foreach (var file in snapshot.Files)
                {
                    writer.Write(file.Path);
                    writer.Write(file.Folder);
                }
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}
