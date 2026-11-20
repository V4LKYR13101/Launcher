using Android.Graphics;
using System.Net;
using System.Security.Cryptography;
namespace LauncherApp.Services;

public static class FeedImages
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly SemaphoreSlim Slots = new(3);
    private const int Limit = 2_000_000;
    public static async Task<Bitmap?> Load(string folder, string address, CancellationToken ct)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0)
            return null;
        bool acquired = false;
        try
        {
            await Slots.WaitAsync(ct);
            acquired = true;
            var directory = System.IO.Path.Combine(folder, "feed-images");
            Directory.CreateDirectory(directory);
            var file = System.IO.Path.Combine(directory, Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(address))));
            byte[]? bytes = null;
            if (File.Exists(file) && new FileInfo(file).Length <= Limit && DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < TimeSpan.FromDays(7))
                bytes = await File.ReadAllBytesAsync(file, ct);
            if (bytes is null)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(12));
                for (int redirects = 0; redirects <= 4; redirects++)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    request.Headers.UserAgent.ParseAdd("Launcher/1.3.4");
                    using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                    {
                        if (redirects == 4 || response.Headers.Location is not { } next)
                            return null;
                        uri = next.IsAbsoluteUri ? next : new Uri(uri, next);
                        if (uri.Scheme != "https" || uri.UserInfo.Length > 0)
                            return null;
                        continue;
                    }
                    if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > Limit)
                        return null;
                    if (response.Content.Headers.ContentType?.MediaType is { } type && !type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                        return null;
                    await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
                    using var output = new MemoryStream();
                    var buffer = new byte[16384];
                    int n;
                    while ((n = await input.ReadAsync(buffer, timeout.Token)) > 0)
                    {
                        if (output.Length + n > Limit)
                            return null;
                        output.Write(buffer, 0, n);
                    }
                    bytes = output.ToArray();
                    break;
                }
                if (bytes is null)
                    return null;
                await File.WriteAllBytesAsync(file, bytes, ct);
                // Keep the disposable image cache bounded to roughly 24 MB.
                long total = 0;
                foreach (var item in new DirectoryInfo(directory).EnumerateFiles().OrderByDescending(f => f.LastWriteTimeUtc))
                {
                    total += item.Length;
                    if (total > 24_000_000)
                        try
                        {
                            item.Delete();
                        }
                        catch (IOException) { }
                }
            }
            ct.ThrowIfCancellationRequested();
            return await Task.Run(() =>
            {
                using var dimensions = new BitmapFactory.Options { InJustDecodeBounds = true };
                BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, dimensions);
                if (dimensions.OutWidth < 8 || dimensions.OutHeight < 8 || (long)dimensions.OutWidth * dimensions.OutHeight > 60_000_000)
                    return null;
                int sample = 1;
                while (Math.Max(dimensions.OutWidth, dimensions.OutHeight) / sample > 320)
                    sample *= 2;
                using var options = new BitmapFactory.Options { InSampleSize = sample };
                return BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, options);
            }, ct);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException or ArgumentException or Java.Lang.RuntimeException) { return null; }
        finally { if (acquired) Slots.Release(); }
    }
}
