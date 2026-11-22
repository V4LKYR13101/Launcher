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
