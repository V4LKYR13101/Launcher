
using Android.Content;

namespace LauncherApp.Services.Speech;

public static class KokoroModelPaths
{
    public const string FolderName = "kokoro-multi-lang-v1_0";

    public static string? FindInstalled(Context context)
        => InstallLocations(context).FirstOrDefault(IsComplete);

    internal static IEnumerable<string> InstallLocations(Context context)
    {
        var roots = new List<string>();

        // Internal, app-private storage.
        if (context.FilesDir?.AbsolutePath is { } internalRoot)
            roots.Add(internalRoot);

        // App-specific external storage. The actual location
        // is resolved by Android and may vary between devices.
        if (context.GetExternalFilesDir(null)?.AbsolutePath is { } externalRoot)
            roots.Add(externalRoot);

        foreach (string root in roots)
        {
            // Support both common project layouts.
            foreach (string relative in new[]
            {
                FolderName,
                Path.Combine("models", FolderName)
            })
            {
                yield return Path.Combine(root, relative);
            }
        }

    }

    public static string GetInstallDirectory(Context context)
    {
        // Reuse an existing working installation.
        string? existing = FindInstalled(context);

        if (existing != null)
            return existing;

        // For new installations, choose internal app storage.
        // Android determines its actual path on this device.
        string root = context.FilesDir?.AbsolutePath
            ?? throw new DirectoryNotFoundException(
                "Application storage is unavailable.");

        return Path.Combine(root, "models", FolderName);
    }

    public static bool IsComplete(string directory)
    {
        if (!Directory.Exists(directory))
            return false;

        foreach (string name in new[]
        {
            "model.onnx",
            "voices.bin",
            "tokens.txt",
            "lexicon-us-en.txt",
            "lexicon-zh.txt"
        })
        {
            string file = Path.Combine(directory, name);

            if (!File.Exists(file) ||
                new FileInfo(file).Length == 0)
                return false;
        }

        string espeak = Path.Combine(directory, "espeak-ng-data");

        return Directory.Exists(espeak) &&
               Directory.EnumerateFiles(
                   espeak, "*", SearchOption.AllDirectories).Any();
    }
}
