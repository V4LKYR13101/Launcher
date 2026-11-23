using System.Security.Cryptography;

namespace LauncherApp.Services.ImageSearch;

internal static class ImageSearchModels
{
    // OpenCLIP ViT-B/32 LAION-2B, int8 exports documented in Lumen AI's ModelCatalog.
    private const string Source = "https://huggingface.co/subhashhhhhh/lumen-clip-vit-b32/resolve/5eca4f3a8eb045c138d7dbadc9a478735a279fbd/";
    internal const string Visual = "visual_int8.onnx";
    internal const string Textual = "textual_int8.onnx";

    internal sealed record ModelFile(string Name, long Bytes, string Hash, string? Url = null);
    private static readonly ModelFile[] Files =
    [
        new(Visual, 95_777_877, "17755a1228aee44f4274eadac14a600a5e45ab9f6325b6ad070b5d0d75e05073"),
        new(Textual, 140_296_927, "e1631f9b00b2a14e5b268a9c73000b1eec3c79bd7d4876ce10fe4f0de1a03b1e"),
        new("vocab.json", 862_328, "5047b556ce86ccaf6aa22b3ffccfc52d391ea4accdab9c2f2407da5b742d4363"),
        new("merges.txt", 524_619, "9fd691f7c8039210e0fced15865466c65820d09b63988b0174bfe25de299051a")
    ];

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(2) };
    internal static long TotalBytes { get; } = Files.Sum(file => file.Bytes);

    internal static long InstalledBytes(string directory, ModelFile[]? files = null) =>
        (files ?? Files).Where(file => Installed(directory, file)).Sum(file => file.Bytes);

    private static bool Installed(string directory, ModelFile file) =>
        new FileInfo(Path.Combine(directory, file.Name)) is { Exists: true } info && info.Length == file.Bytes;

    internal static async Task Download(string directory, Action<long> progress, CancellationToken token, ModelFile[]? files = null, string name = "OpenCLIP image model")
    {
        Directory.CreateDirectory(directory);
        long position = InstalledBytes(directory, files);
        long total = (files ?? Files).Sum(file => file.Bytes);
        progress(position);
        if (position == total)
            return;
        using var transfer = ActiveDownloads.Start(name, total);
        transfer.Report(position, total);

        foreach (var file in files ?? Files)
        {
            token.ThrowIfCancellationRequested();
            if (Installed(directory, file))
                continue;

            string target = Path.Combine(directory, file.Name);
            string partial = target + ".partial";
            File.Delete(partial);
            using var storage = new Android.OS.StatFs(directory);
            if (storage.AvailableBytes < file.Bytes)
                throw new IOException("There isn't enough space for the image search model.");

            try
            {
                using var response = await Client.GetAsync(file.Url ?? Source + file.Name, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromMinutes(30));
                await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long bytes = 0;
                await using (var output = File.Create(partial))
                {
                    var buffer = new byte[128 * 1024];
                    int count;
                    while ((count = await input.ReadAsync(buffer.AsMemory(), deadline.Token)) != 0)
                    {
                        bytes += count;
                        if (bytes > file.Bytes)
                            throw new InvalidDataException("The model download exceeded its expected size.");
                        hash.AppendData(buffer, 0, count);
                        await output.WriteAsync(buffer.AsMemory(0, count), deadline.Token);
                        progress(position + bytes);
                        transfer.Report(position + bytes, total);
                    }
                    await output.FlushAsync(deadline.Token);
                }

                if (bytes != file.Bytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The model download failed its integrity check.");
                token.ThrowIfCancellationRequested();
                File.Move(partial, target, overwrite: true);
                position += bytes;
            }
            finally
            {
                File.Delete(partial);
            }
        }
    }
}
