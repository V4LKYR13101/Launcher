using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace LauncherApp.Services.DocumentSearch;

internal static class DocumentText
{
    private const int PassageLength = 1100;
    private const int Overlap = 140;

    internal static bool Supports(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".pdf" or ".docx" or ".txt" or ".md" or ".csv";

    internal static IEnumerable<string> Passages(string path, CancellationToken token)
    {
        var buffer = new StringBuilder(PassageLength);
        bool space = false;
        int retained = 0;
        foreach (string text in Sections(path, token))
        {
            token.ThrowIfCancellationRequested();
            foreach (char character in text)
            {
                if (char.IsWhiteSpace(character))
                {
                    space = buffer.Length > 0;
                    continue;
                }
                if (space)
                    buffer.Append(' ');
                space = false;
                buffer.Append(character);
                if (buffer.Length < PassageLength || char.IsHighSurrogate(character))
                    continue;

                string window = buffer.ToString();
                int split = window.LastIndexOf(' ');
                if (split < PassageLength - 100)
                    split = window.Length;
                yield return window[..split];
                int carry = window.LastIndexOf(' ', Math.Max(0, split - Overlap));
                if (carry < split - Overlap - 60)
                    carry = split - Overlap;
                while (carry < window.Length && char.IsLowSurrogate(window[carry]))
                    carry++;
                if (window[carry] == ' ')
                    carry++;
                buffer.Remove(0, carry);
                retained = split - carry;
            }
        }
        if (buffer.Length > retained)
            yield return buffer.ToString();
    }

    private static IEnumerable<string> Sections(string path, CancellationToken token)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".pdf":
                using (var pdf = PdfDocument.Open(path))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        token.ThrowIfCancellationRequested();
                        yield return ContentOrderTextExtractor.GetText(page) + "\n";
                    }
                }
                break;
            case ".docx":
                using (var package = ZipFile.OpenRead(path))
                {
                    var document = package.GetEntry("word/document.xml");
                    if (document is null)
                        yield break;
                    using var stream = document.Open();
                    using var reader = XmlReader.Create(stream, new XmlReaderSettings { MaxCharactersInDocument = 32_000_000 });
                    while (reader.Read())
                    {
                        token.ThrowIfCancellationRequested();
                        if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "p")
                            continue;
                        using var paragraphReader = reader.ReadSubtree();
                        var paragraph = XElement.Load(paragraphReader);
                        yield return string.Concat(paragraph.Descendants().Select(node => node.Name.LocalName switch
                        {
                            "t" => node.Value,
                            "br" or "tab" => " ",
                            _ => ""
                        })) + "\n";
                    }
                }
                break;
            default:
                using (var reader = new StreamReader(path))
                {
                    var buffer = new char[8192];
                    int read;
                    while ((read = reader.ReadBlock(buffer)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        yield return new string(buffer, 0, read);
                    }
                }
                break;
        }
    }
}
