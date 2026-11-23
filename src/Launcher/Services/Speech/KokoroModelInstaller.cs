
using System.Security.Cryptography;
using Android.Content;
using SharpCompress.Readers;

namespace LauncherApp.Services.Speech;

public sealed record ModelInstallProgress(
    string Message,
    int? Percentage = null);

public static class KokoroModelInstaller
{
    private const string FolderName = "kokoro-multi-lang-v1_0";

    private const string ArchiveUrl =
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/" +
        "tts-models/kokoro-multi-lang-v1_0.tar.bz2";

    private const long ExpectedArchiveSize = 349886476L;

    private const string ExpectedSha256 =
        "60703707bc05ccd48d60e1eb723f38b07d6e474795a0b6a588d7220400e0f7a4";

    private static readonly SemaphoreSlim InstallLock = new(1, 1);

    private static readonly HttpClient Http = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public static string GetModelDirectory(Context context)
    {
        return KokoroModelPaths.GetInstallDirectory(context);
    }

    public static bool IsInstalled(Context context)
    {
        return KokoroModelPaths.FindInstalled(context) != null;
    }

    internal static async Task RemoveAsync(Context context)
    {
        await InstallLock.WaitAsync().ConfigureAwait(false);
        try
        {
            LauncherSpeech.Instance.Stop();
            foreach (string path in KokoroModelPaths.InstallLocations(context).Distinct(StringComparer.Ordinal))
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
        }
        finally { InstallLock.Release(); }
    }

    public static async Task InstallAsync(
        Context context,
        IProgress<ModelInstallProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        bool entered = await InstallLock.WaitAsync(
            0, cancellationToken);

        if (!entered)
        {
            throw new InvalidOperationException(
                "A Kokoro model installation is already running.");
        }

        string? stagingRoot = null;

        try
        {
            if (IsInstalled(context))
            {
                progress?.Report(new(
                    "Kokoro is already installed.", 100));
                return;
            }

            string modelDirectory = GetModelDirectory(context);

            string root = Path.GetDirectoryName(modelDirectory)
                ?? throw new DirectoryNotFoundException();

            Directory.CreateDirectory(root);

            stagingRoot = Path.Combine(
                root,
                ".kokoro-install-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(stagingRoot);

            string archivePath = Path.Combine(
                stagingRoot,
                "download.tar.bz2");

            string extractionRoot = Path.Combine(
                stagingRoot,
                "extracted");

            Directory.CreateDirectory(extractionRoot);

            await DownloadArchiveAsync(
                archivePath,
                progress,
                cancellationToken);

            progress?.Report(new(
                "Verifying downloaded archive..."));

            await VerifyArchiveAsync(
                archivePath,
                cancellationToken);

            progress?.Report(new(
                "Extracting model and voice files..."));

            await ExtractArchiveAsync(
                archivePath,
                extractionRoot,
                cancellationToken);

            string extractedModel = Path.Combine(
                extractionRoot,
                FolderName);

            if (!KokoroModelPaths.IsComplete(extractedModel))
            {
                throw new InvalidDataException(
                    "The archive does not contain all required Kokoro files.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new(
                "Installing verified model files..."));

            string? backup = null;

            if (Directory.Exists(modelDirectory))
            {
                backup = Path.Combine(
                    root,
                    ".kokoro-backup-" +
                    Guid.NewGuid().ToString("N"));

                Directory.Move(modelDirectory, backup);
            }

            try
            {
                Directory.Move(
                    extractedModel,
                    modelDirectory);
            }
            catch
            {
                if (!Directory.Exists(modelDirectory) &&
                    backup != null &&
                    Directory.Exists(backup))
                {
                    Directory.Move(backup, modelDirectory);
                }

                throw;
            }

            if (backup != null && Directory.Exists(backup))
            {
                try
                {
                    Directory.Delete(backup, recursive: true);
                }
                catch (IOException)
                {
                    // Installation succeeded; cleanup can be retried.
                }
                catch (UnauthorizedAccessException)
                {
                    // Installation succeeded; retain old backup.
                }
            }

            progress?.Report(new(
                "Kokoro installation complete.", 100));
        }
        finally
        {
            if (stagingRoot != null &&
                Directory.Exists(stagingRoot))
            {
                try
                {
                    Directory.Delete(
                        stagingRoot,
                        recursive: true);
                }
                catch (IOException)
                {
                    // Do not mask the original installation result.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore staging cleanup failure.
                }
            }

            InstallLock.Release();
        }
    }

    private static async Task DownloadArchiveAsync(
        string destination,
        IProgress<ModelInstallProgress>? progress,
        CancellationToken ct)
    {
        using var transfer = ActiveDownloads.Start("Kokoro voice model", ExpectedArchiveSize);
        progress?.Report(new(
            "Connecting to the Kokoro model release...", 0));

        using var response = await Http.GetAsync(
            ArchiveUrl,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is long length &&
            length != ExpectedArchiveSize)
        {
            throw new InvalidDataException(
                $"Unexpected archive size: {length} bytes.");
        }

        await using var input =
            await response.Content.ReadAsStreamAsync(ct);

        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 131072,
            FileOptions.Asynchronous);

        byte[] buffer = new byte[131072];
        long received = 0;
        int lastPercentage = -1;

        while (true)
        {
            int count = await input.ReadAsync(
                buffer.AsMemory(), ct);

            if (count == 0)
                break;

            received += count;

            if (received > ExpectedArchiveSize)
            {
                throw new InvalidDataException(
                    "The downloaded archive is too large.");
            }

            await output.WriteAsync(
                buffer.AsMemory(0, count), ct);
            transfer.Report(received, ExpectedArchiveSize);

            int percentage = (int)(
                received * 100.0 / ExpectedArchiveSize);

            if (percentage != lastPercentage)
            {
                lastPercentage = percentage;

                progress?.Report(new(
                    $"Downloading Kokoro: {percentage}%",
                    percentage));
            }
        }

        await output.FlushAsync(ct);

        if (received != ExpectedArchiveSize)
        {
            throw new InvalidDataException(
                $"Incomplete download: {received} of " +
                $"{ExpectedArchiveSize} bytes.");
        }
    }

    private static async Task VerifyArchiveAsync(
        string archive,
        CancellationToken ct)
    {
        await using var stream = File.OpenRead(archive);

        byte[] hash = await SHA256.HashDataAsync(
            stream, ct);

        string actual = Convert.ToHexStringLower(hash);

        if (!actual.Equals(
            ExpectedSha256,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Kokoro archive checksum verification failed.");
        }
    }

    private static async Task ExtractArchiveAsync(
        string archivePath,
        string destinationRoot,
        CancellationToken ct)
    {
        string modelRoot = Path.Combine(
            destinationRoot,
            FolderName);

        Directory.CreateDirectory(modelRoot);

        string safeRoot =
            Path.GetFullPath(modelRoot) +
            Path.DirectorySeparatorChar;

        using var reader = ReaderFactory.OpenReader(archivePath);

        int entryCount = 0;
        long extractedBytes = 0;

        while (reader.MoveToNextEntry())
        {
            ct.ThrowIfCancellationRequested();

            if (++entryCount > 10000)
            {
                throw new InvalidDataException(
                    "Too many archive entries.");
            }

            var entry = reader.Entry;

            string key = (entry.Key ?? "")
                .Replace('\\', '/');

            // Normalise leading ./ prefixes.
            while (key.StartsWith(
                "./", StringComparison.Ordinal))
            {
                key = key[2..];
            }

            // TAR directory entries commonly end with '/'.
            // The old code treated the final empty segment
            // as an unsafe path. Normalise it before validation.
            bool isDirectory =
                entry.IsDirectory ||
                key.EndsWith("/", StringComparison.Ordinal);

            if (isDirectory)
            {
                key = key.TrimEnd('/');
            }

            // Permit the harmless archive root itself.
            if (isDirectory &&
                (key.Length == 0 ||
                 key == "." ||
                 key == FolderName))
            {
                continue;
            }

            // Do not allow archive symlinks or hard links.
            if (!string.IsNullOrEmpty(entry.LinkTarget))
            {
                throw new InvalidDataException(
                    $"Archive links are not permitted: {key}");
            }

            string prefix = FolderName + "/";

            if (!key.StartsWith(
                prefix,
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Unexpected archive entry: {key}");
            }

            string relative = key[prefix.Length..];

            if (relative.Length == 0)
            {
                throw new InvalidDataException(
                    $"Empty archive entry path: {key}");
            }

            // Validate every path component.
            // Empty intermediate components and traversal
            // segments remain strictly prohibited.
            string[] segments = relative.Split('/');

            if (segments.Any(segment =>
                    segment.Length == 0 ||
                    segment == "." ||
                    segment == ".." ||
                    segment.Contains(':')))
            {
                throw new InvalidDataException(
                    $"Unsafe archive path: {key}");
            }

            string target = Path.GetFullPath(
                Path.Combine(
                    modelRoot,
                    relative.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));

            // The extracted path must remain within the
            // designated model directory.
            if (!target.StartsWith(
                safeRoot,
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Archive path escapes model directory: {key}");
            }

            if (isDirectory)
            {
                if (entry.Size > 0 && !entry.IsDirectory)
                {
                    throw new InvalidDataException(
                        $"Invalid directory entry: {key}");
                }

                Directory.CreateDirectory(target);
                continue;
            }

            if (entry.Size < 0 ||
                entry.Size > 1_000_000_000L)
            {
                throw new InvalidDataException(
                    $"Unexpected model file size: {key}");
            }

            extractedBytes += entry.Size;

            if (extractedBytes > 2_000_000_000L)
            {
                throw new InvalidDataException(
                    "Extracted archive exceeds safe size.");
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(target)!);

            using var entryStream = reader.OpenEntryStream();

            await using var output = new FileStream(
                target,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 131072,
                FileOptions.Asynchronous);

            await entryStream.CopyToAsync(
                output,
                131072,
                ct);
        }
    }
}
