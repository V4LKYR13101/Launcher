using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Launcher.Core;

public sealed record AppRelease(string Version, long Build, string Url, string Sha256, long Bytes, string? Notes = null);
public sealed record AppReleases(AppRelease Android);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppReleases))]
internal partial class UpdateJsonContext : JsonSerializerContext;

public static class AppUpdates
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromMinutes(10) };

    public static Uri Validate(AppRelease release)
    {
        if (!System.Version.TryParse(release.Version, out _) || release.Build <= 0 ||
            !Uri.TryCreate(release.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Host != "kevlog.co.za" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
            !uri.AbsolutePath.StartsWith("/downloads/projects/launcher/", StringComparison.Ordinal) ||
            release.Bytes is <= 0 or > 256 * 1024 * 1024 || release.Sha256 is null ||
            release.Sha256.Length != 64 || !release.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("The update information is invalid.");
        return uri;
    }

    public static async Task<AppRelease> CheckAsync(CancellationToken token = default, HttpClient? client = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var releases = await (client ?? Client).GetFromJsonAsync(
            "https://kevlog.co.za/updates/launcher.json?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdateJsonContext.Default.AppReleases, timeout.Token);
        var release = releases?.Android ?? throw new IOException("Update information is unavailable.");
        Validate(release);
        return release;
    }

    public static async Task DownloadAsync(AppRelease release, string destination, Action<int> progress,
        CancellationToken token = default, HttpClient? client = null)
    {
        var uri = Validate(release);
        var temporary = destination + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        try
        {
            using var response = await (client ?? Client).GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(token);
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
            {
                var buffer = new byte[65536];
                long received = 0;
                int count, last = -1;
                while ((count = await source.ReadAsync(buffer, token)) != 0)
                {
                    received += count;
                    if (received > release.Bytes) throw new InvalidDataException("The update is larger than expected.");
                    digest.AppendData(buffer, 0, count);
                    await file.WriteAsync(buffer.AsMemory(0, count), token);
                    int percent = (int)(received * 100 / release.Bytes);
                    if (percent != last) { progress(percent); last = percent; }
                }
                if (received != release.Bytes || !CryptographicOperations.FixedTimeEquals(
                    digest.GetHashAndReset(), Convert.FromHexString(release.Sha256)))
                    throw new InvalidDataException("The update failed verification. Please try again.");
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}
