using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
namespace LauncherApp.Services;

public sealed class FeedLoadException(string message) : IOException(message);
public static class FeedReader
{
    private const int Limit = 4_000_000;
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(25) };
    public static string Address(string value)
    {
        value = value.Trim();
        if (value.StartsWith("feed://", StringComparison.OrdinalIgnoreCase))
            value = "https://" + value[7..];
        if (!value.Contains("://"))
            value = "https://" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0 || uri.Host.Length == 0 || value.Length > 2048)
            throw new ArgumentException("Enter a full HTTPS RSS or Atom feed address.");
        return new UriBuilder(uri) { Fragment = "" }.Uri.AbsoluteUri;
    }
    public static async Task<CardLink[]> Fetch(string address, CancellationToken cancellation = default)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        lifetime.CancelAfter(TimeSpan.FromSeconds(25));
        var ct = lifetime.Token;
        var uri = new Uri(Address(address));
        for (int discovery = 0; discovery < 2; discovery++)
        {
            var (bytes, location, media) = await Download(uri, ct);
            uri = location;
            bool html = media?.Contains("html", StringComparison.OrdinalIgnoreCase) == true || Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 100)).TrimStart().StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase);
            if (html)
            {
                if (discovery > 0)
                    throw new FeedLoadException(Localize(Resource.String.ui_this_address_returns_a_web_page_not_an_rss));
                var found = Discover(Encoding.UTF8.GetString(bytes), uri);
                if (found is null)
                    throw new FeedLoadException(Localize(Resource.String.ui_no_rss_or_atom_feed_was_found_on_this));
                uri = found;
                continue;
            }
            try
            {
                using var stream = new MemoryStream(bytes);
                using var reader = XmlReader.Create(stream, new()
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null,
                    MaxCharactersInDocument = Limit,
                    MaxCharactersFromEntities = 1024
                });
                var document = XDocument.Load(reader);
                if (document.Root?.Name.LocalName is not ("rss" or "feed" or "RDF"))
                    throw new FeedLoadException(Localize(Resource.String.ui_this_address_does_not_return_a_recognised_rss_or));
                string Read(XElement entry, params string[] names) => entry.Elements().FirstOrDefault(e => names.Contains(e.Name.LocalName))?.Value ?? "";
                var links = new List<CardLink>();
                foreach (var entry in document.Descendants().Where(e => e.Name.LocalName is "item" or "entry").Take(100))
                {
                    var link = entry.Elements().Where(e => e.Name.LocalName == "link").FirstOrDefault(e => e.Attribute("href") is not null && (e.Attribute("rel")?.Value is null or "alternate"))
                        ?? entry.Elements().FirstOrDefault(e => e.Name.LocalName == "link" && e.Attribute("href") is null);
                    string href = link?.Attribute("href")?.Value ?? link?.Value ?? Read(entry, "guid");
                    var basis = uri;
                    foreach (var ancestor in entry.AncestorsAndSelf().Reverse())
                    if (ancestor.Attribute(XNamespace.Xml + "base")?.Value is { } xmlBase && Uri.TryCreate(basis, xmlBase, out var changed))
                        basis = changed;
                    if (!Uri.TryCreate(basis, href, out var article) || !OnlineCards.SafeLink(article.AbsoluteUri))
                        continue;
                    long published = DateTimeOffset.TryParse(Read(entry, "pubDate", "published", "date", "updated"), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var time) ? time.ToUnixTimeMilliseconds() : 0;
                    string title = Plain(Read(entry, "title"), 300);
                    if (title.Length == 0)
                        title = "Untitled article";
                    string source = Read(entry, "source");
                    links.Add(new(title, Plain(Read(entry, "description", "summary", "encoded", "content"), 250), article.AbsoluteUri, published, Plain(source, 100), Image(entry, uri)));
                }
                return links.ToArray();
            }
            catch (XmlException) { throw new FeedLoadException(Localize(Resource.String.ui_the_feed_returned_invalid_xml_check_its_address_or)); }
        }
        return [];
    }
    private static string Image(XElement entry, Uri basis)
    {
        string Resolve(string? value, XElement element)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            var relative = basis;
            foreach (var ancestor in element.AncestorsAndSelf().Reverse())
                if (ancestor.Attribute(XNamespace.Xml + "base")?.Value is { } xmlBase && Uri.TryCreate(relative, xmlBase, out var changed))
                    relative = changed;
            if (!Uri.TryCreate(relative, WebUtility.HtmlDecode(value), out var uri) || uri.UserInfo.Length > 0)
                return "";
            if (uri.Scheme == "http")
                uri = new UriBuilder(uri) { Scheme = "https", Port = uri.IsDefaultPort ? -1 : uri.Port }.Uri;
            return uri.Scheme == "https" && uri.AbsoluteUri.Length <= 2048 ? uri.AbsoluteUri : "";
        }
        var descendants = entry.Descendants().ToArray();
        var candidates = descendants.Where(e => e.Name.LocalName == "thumbnail")
            .Concat(descendants.Where(e => e.Name.LocalName is "content" or "enclosure" &&
                (e.Attribute("type")?.Value.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true || e.Attribute("medium")?.Value == "image" || Regex.IsMatch(e.Attribute("url")?.Value ?? "", @"\.(?:jpe?g|png|webp|gif)(?:[?#]|$)", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))))
            .Concat(descendants.Where(e => e.Name.LocalName == "link" && e.Attribute("rel")?.Value == "enclosure" && e.Attribute("type")?.Value.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true));
        foreach (var e in candidates)
        {
            var url = Resolve(e.Attribute("url")?.Value ?? e.Attribute("href")?.Value, e);
            if (url.Length > 0)
                return url;
        }
        foreach (var e in descendants.Where(e => e.Name.LocalName == "img"))
        {
            var url = Resolve(e.Attribute("src")?.Value, e);
            if (url.Length > 0)
                return url;
        }
        foreach (var e in entry.Elements().Where(e => e.Name.LocalName is "description" or "summary" or "encoded" or "content"))
        {
            foreach (Match tag in Regex.Matches(e.Value, @"<img\b[^>]*>", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
            {
                var attribute = Regex.Match(tag.Value, """\b(?:src|data-src)\s*=\s*(?:"(?<url>[^"]+)"|'(?<url>[^']+)')""", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                if (attribute.Success)
                {
                    var url = Resolve(attribute.Groups["url"].Value, e);
                    if (url.Length > 0)
                        return url;
                }
            }
        }
        return "";
    }
    private static async Task<(byte[] Bytes, Uri Location, string? Media)> Download(Uri uri, CancellationToken ct)
    {
        for (int redirects = 0; redirects <= 5; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Launcher/1.3.4");
            request.Headers.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml, text/xml, text/html;q=0.5");
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                if (redirects == 5 || response.Headers.Location is not { } next)
                    throw new FeedLoadException(Localize(Resource.String.ui_this_feed_redirects_too_many_times_edit_its_address));
                var destination = next.IsAbsoluteUri ? next : new Uri(uri, next);
                if (destination.Scheme != "https" || destination.UserInfo.Length > 0)
                    throw new FeedLoadException(Localize(Resource.String.ui_this_feed_redirects_to_an_unencrypted_address_use_its));
                uri = destination;
                continue;
            }
            if (!response.IsSuccessStatusCode)
                throw new FeedLoadException(Localize(Resource.String.ui_the_feed_server_returned_value_try_again_later_or, (int)response.StatusCode));
            if (response.Content.Headers.ContentLength > Limit)
                throw new FeedLoadException(Localize(Resource.String.ui_this_feed_exceeds_the_4_mb_size_limit));
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            using var output = new MemoryStream();
            var buffer = new byte[16384];
            int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (output.Length + count > Limit)
                    throw new FeedLoadException(Localize(Resource.String.ui_this_feed_exceeds_the_4_mb_size_limit));
                output.Write(buffer, 0, count);
            }
            return (output.ToArray(), uri, response.Content.Headers.ContentType?.MediaType);
        }
        throw new FeedLoadException(Localize(Resource.String.ui_could_not_follow_this_feed_address));
    }
    private static Uri? Discover(string html, Uri basis)
    {
        foreach (Match match in Regex.Matches(html, @"<link\b[^>]*>", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match attribute in Regex.Matches(match.Value, """(?<key>[\w:-]+)\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.None, TimeSpan.FromSeconds(1)))
                attributes[attribute.Groups["key"].Value] = WebUtility.HtmlDecode(attribute.Groups["value"].Value);
            if (attributes.TryGetValue("type", out var type) && (type.Equals("application/rss+xml", StringComparison.OrdinalIgnoreCase) || type.Equals("application/atom+xml", StringComparison.OrdinalIgnoreCase))
                && attributes.TryGetValue("href", out var href) && Uri.TryCreate(basis, href, out var result) && result.Scheme == "https" && result.UserInfo.Length == 0)
                return result;
        }
        return null;
    }
    private static string Plain(string text, int max)
    {
        text = WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", " ", RegexOptions.None, TimeSpan.FromSeconds(1)));
        text = Regex.Replace(text, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
        return text[..Math.Min(text.Length, max)];
    }
}
