using System.Net;
using System.Net.Http.Headers;
using System.Diagnostics.CodeAnalysis;

namespace LauncherApp.Services;

internal static class DirectFileTransfer
{
    private const int ChunkCount = 4;
    private const int BufferSize = 128 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        UseCookies = false,
        ConnectTimeout = RequestTimeout
    }) { Timeout = Timeout.InfiniteTimeSpan };

    internal sealed record FileDetails(string Name, string MimeType);
    private sealed class RangeRejected : Exception;
    private sealed class WebPage : Exception;
    internal static bool IsWebPage(Exception error) => error is WebPage;

    internal static bool TryGetUrl(string text, [NotNullWhen(true)] out Uri? url)
    {
        string trimmed = text.Trim();
        if (!trimmed.Any(char.IsWhiteSpace) && Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed) &&
            parsed.Scheme is "http" or "https" && parsed.Host.Length > 0 && parsed.UserInfo.Length == 0)
        {
            url = parsed;
            return true;
        }
        url = null;
        return false;
    }

    internal static async Task<FileDetails> Download(Uri url, string path, Action<long, long?> progress, CancellationToken token,
        Action<string>? setName = null)
    {
        using var probe = await Retry(() => Get(url, new RangeHeaderValue(0, 0), null, token), token);
        if (probe.StatusCode == HttpStatusCode.OK)
            return await DownloadWhole(url, path, progress, token, setName, probe);

        if (probe.StatusCode != HttpStatusCode.RequestedRangeNotSatisfiable)
            probe.EnsureSuccessStatusCode();
        var contentRange = probe.Content.Headers.ContentRange;
        var validator = Validator(probe);
        if (probe.StatusCode == HttpStatusCode.PartialContent && contentRange is { Unit: "bytes", From: 0, To: 0, Length: >= 4 * 1024 * 1024 } &&
            validator is not null && HasIdentityEncoding(probe))
        {
            var target = probe.RequestMessage!.RequestUri!;
            var details = Describe(probe, target);
            setName?.Invoke(details.Name);
            probe.Dispose();
            try
            {
                await DownloadRanges(target, path, contentRange.Length.Value, validator, progress, token);
                return details;
            }
            catch (RangeRejected) { token.ThrowIfCancellationRequested(); }
        }
        probe.Dispose();
        return await DownloadWhole(url, path, progress, token, setName);
    }

    private static async Task<FileDetails> DownloadWhole(Uri url, string path, Action<long, long?> progress,
        CancellationToken token, Action<string>? setName, HttpResponseMessage? initial = null)
    {
        return await Retry(async () =>
        {
            using var response = initial ?? await Get(url, null, null, token);
            initial = null;
            response.EnsureSuccessStatusCode();
            if (response.StatusCode != HttpStatusCode.OK || !HasIdentityEncoding(response))
                throw new InvalidDataException("The server did not return a complete file.");
            var details = Describe(response, response.RequestMessage!.RequestUri!);
            setName?.Invoke(details.Name);
            long? size = response.Content.Headers.ContentLength;
            long written = 0;
            await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);
            progress(0, size);
            await Copy(response, size, async (buffer, cancellation) =>
            {
                await output.WriteAsync(buffer, cancellation);
                written += buffer.Length;
                progress(written, size);
            }, token);
            return details;
        }, token);
    }

    private static async Task DownloadRanges(Uri url, string path, long size, RangeConditionHeaderValue validator,
        Action<long, long?> progress, CancellationToken token)
    {
        using var output = File.OpenHandle(path, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.Asynchronous);
        RandomAccess.SetLength(output, size);
        using var workers = CancellationTokenSource.CreateLinkedTokenSource(token);
        var workerToken = workers.Token;
        long written = 0;
        int rejected = 0;
        long chunkSize = size / ChunkCount;
        progress(0, size);
        try
        {
            await Task.WhenAll(Enumerable.Range(0, ChunkCount).Select(DownloadChunk));
        }
        catch (Exception) when (Volatile.Read(ref rejected) != 0 && !token.IsCancellationRequested)
        {
            throw new RangeRejected();
        }

        async Task DownloadChunk(int index)
        {
            long offset = index * chunkSize;
            long end = index == ChunkCount - 1 ? size - 1 : offset + chunkSize - 1;
            try
            {
                await Retry(async () =>
                {
                    if (offset > end)
                        return true;
                    using var response = await Get(url, new RangeHeaderValue(offset, end), validator, workerToken);
                    var range = response.Content.Headers.ContentRange;
                    var responseValidator = Validator(response);
                    if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.RequestedRangeNotSatisfiable ||
                        response.StatusCode == HttpStatusCode.PartialContent &&
                        (range?.Unit != "bytes" || range.From != offset || range.To != end || range.Length != size ||
                         response.RequestMessage?.RequestUri != url || !HasIdentityEncoding(response) ||
                         responseValidator is not null && !validator.Equals(responseValidator)))
                        throw new RangeRejected();
                    response.EnsureSuccessStatusCode();
                    if (response.StatusCode != HttpStatusCode.PartialContent)
                        throw new RangeRejected();
                    await Copy(response, end - offset + 1, async (buffer, cancellation) =>
                    {
                        await RandomAccess.WriteAsync(output, buffer, offset, cancellation);
                        offset += buffer.Length;
                        progress(Interlocked.Add(ref written, buffer.Length), size);
                    }, workerToken);
                    return true;
                }, workerToken);
            }
            catch (RangeRejected)
            {
                Interlocked.Exchange(ref rejected, 1);
                workers.Cancel();
                throw;
            }
        }
    }

    private static async Task<HttpResponseMessage> Get(Uri url, RangeHeaderValue? range,
        RangeConditionHeaderValue? validator, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = range;
        request.Headers.IfRange = validator;
        request.Headers.AcceptEncoding.ParseAdd("identity");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (IsTransient(response.StatusCode))
            {
                response.Dispose();
                response.EnsureSuccessStatusCode();
            }
            return response;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException(); }
    }

    private static async Task Copy(HttpResponseMessage response, long? remaining,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write, CancellationToken token)
    {
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        var buffer = new byte[BufferSize];
        while (true)
        {
            int count;
            timeout.CancelAfter(RequestTimeout);
            try { count = await input.ReadAsync(buffer, timeout.Token); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException(); }
            catch (IOException error) { throw new HttpRequestException("The connection ended during the download.", error); }
            finally { timeout.CancelAfter(Timeout.InfiniteTimeSpan); }
            if (count == 0)
            {
                if (remaining is > 0)
                    throw new EndOfStreamException("The server returned an incomplete file.");
                return;
            }
            if (count > remaining)
                throw new InvalidDataException("The server returned more bytes than expected.");
            await write(buffer.AsMemory(0, count), token);
            remaining -= count;
        }
    }

    private static async Task<T> Retry<T>(Func<Task<T>> operation, CancellationToken token)
    {
        for (int attempt = 1; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { return await operation(); }
            catch (Exception error) when (attempt < 3 && (error is TimeoutException or EndOfStreamException ||
                error is HttpRequestException http && (http.StatusCode is null || IsTransient(http.StatusCode.Value))))
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), token);
            }
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    private static bool HasIdentityEncoding(HttpResponseMessage response) =>
        response.Content.Headers.ContentEncoding.All(encoding => encoding.Equals("identity", StringComparison.OrdinalIgnoreCase));

    private static RangeConditionHeaderValue? Validator(HttpResponseMessage response) =>
        response.Headers.ETag is { IsWeak: false } etag ? new(etag) :
        response.Content.Headers.LastModified is { } modified ? new(modified) : null;

    private static FileDetails Describe(HttpResponseMessage response, Uri url)
    {
        var headers = response.Content.Headers;
        var disposition = headers.ContentDisposition;
        var mime = headers.ContentType?.MediaType ?? "application/octet-stream";
        string? supplied = disposition?.FileNameStar ?? disposition?.FileName;
        string pathName = PathName(url);
        if (mime is "text/html" or "application/xhtml+xml" && supplied is null &&
            !string.Equals(disposition?.DispositionType, "attachment", StringComparison.OrdinalIgnoreCase) &&
            !new[] { ".html", ".htm", ".xhtml" }.Contains(Path.GetExtension(pathName), StringComparer.OrdinalIgnoreCase))
            throw new WebPage();
        return new(SafeName(string.IsNullOrWhiteSpace(supplied) ? pathName : supplied), mime);
    }

    internal static string SuggestedName(Uri url) => SafeName(PathName(url));

    private static string PathName(Uri url) => Uri.UnescapeDataString(url.Segments.LastOrDefault() ?? "").TrimEnd('/');

    private static string SafeName(string name)
    {
        name = name.Trim('"').Replace('\\', '/').Split('/').Last();
        name = string.Concat(name.Where(character => !char.IsControl(character) &&
            !char.IsSurrogate(character) && "<>:\"|?*".IndexOf(character) < 0)).Trim(' ', '.');
        name = new string(name.Take(70).ToArray()).TrimEnd(' ', '.');
        return name.Length > 0 ? name : "download";
    }
}
