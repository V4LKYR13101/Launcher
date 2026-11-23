using Android.Content;
using Android.Content.PM;
using Android.OS.Storage;
using StorageEnvironment = Android.OS.Environment;

namespace LauncherApp.Services;

internal static class SharedStorage
{
    public static bool CanRead(Context context) => OperatingSystem.IsAndroidVersionAtLeast(30)
        ? StorageEnvironment.IsExternalStorageManager
        : context.CheckSelfPermission(Android.Manifest.Permission.ReadExternalStorage) == Permission.Granted;

    public static IEnumerable<(string Root, string Label)> MountedVolumes(Context context)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (root, label) in StorageRoots(context))
        {
            using var directory = new Java.IO.File(root);
            if (directory.CanonicalPath is { } canonical && seen.Add(canonical))
                yield return (canonical, label);
        }
    }

    public static IEnumerable<(string Path, string Name, string Folder)> Files(Context context, CancellationToken token) =>
        Files(MountedVolumes(context), token);

    public static IEnumerable<(string Path, string Name, string Folder)> Files(IEnumerable<(string Root, string Label)> volumes, CancellationToken token)
    {
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        foreach (var (root, label) in volumes)
        {
            var directories = new Stack<DirectoryInfo>();
            directories.Push(new DirectoryInfo(root));
            while (directories.TryPop(out var directory))
            {
                token.ThrowIfCancellationRequested();
                string folder = label + directory.FullName[root.Length..];
                FileSystemInfo[] entries;
                try { entries = directory.GetFileSystemInfos("*", options); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { continue; }

                foreach (var entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    if (entry is not DirectoryInfo child)
                        yield return (entry.FullName, entry.Name, folder);
                    else if (Contains(root, child.FullName))
                        directories.Push(child);
                }
            }
        }
    }

    private static IEnumerable<(string Root, string Label)> StorageRoots(Context context)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && context.GetSystemService(Context.StorageService) is StorageManager manager)
        {
            foreach (var volume in manager.StorageVolumes)
            {
                if (IsMounted(volume.State) && volume.Directory?.AbsolutePath is { } path)
                    yield return (path, volume.GetDescription(context) ?? "Shared storage");
            }
            yield break;
        }

        // On older Android versions, the app-specific external directories identify mounted volumes.
        string suffix = $"/Android/data/{context.PackageName}/files";
        foreach (var directory in context.GetExternalFilesDirs(null) ?? [])
        {
            if (directory?.AbsolutePath is not { } path || !path.EndsWith(suffix, StringComparison.Ordinal)
                || !IsMounted(StorageEnvironment.GetExternalStorageState(directory)))
                continue;

            string root = path[..^suffix.Length];
            string label = StorageEnvironment.InvokeIsExternalStorageRemovable(directory) ? Path.GetFileName(root) : "Internal shared storage";
            yield return (root, label);
        }
    }

    private static bool IsMounted(string? state) => state is StorageEnvironment.MediaMounted or StorageEnvironment.MediaMountedReadOnly;

    public static bool Contains(string root, string path)
    {
        if (!path.StartsWith(root + "/", StringComparison.Ordinal))
            return false;

        var relative = path.AsSpan(root.Length + 1);
        return !relative.Equals("Android/data", StringComparison.OrdinalIgnoreCase)
            && !relative.StartsWith("Android/data/", StringComparison.OrdinalIgnoreCase)
            && !relative.Equals("Android/obb", StringComparison.OrdinalIgnoreCase)
            && !relative.StartsWith("Android/obb/", StringComparison.OrdinalIgnoreCase);
    }
}
