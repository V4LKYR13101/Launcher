
using System.Text;
using System.Text.RegularExpressions;

namespace LauncherApp.Services.Speech;

public static class SpeechTextChunker
{
    public static IReadOnlyList<string> Split(
        string text,
        int maxChars = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        if (maxChars < 50)
            throw new ArgumentOutOfRangeException(
                nameof(maxChars));

        text = text.Replace("\r\n", "\n");
        text = Regex.Replace(text, @"[ \t]+", " ");

        var sentences = Regex.Split(
            text.Trim(),
            @"(?<=[.!?。！？])\s+|\n+");

        var fragments = new List<string>();

        foreach (var sentence in sentences)
        {
            var remaining = sentence.Trim();

            while (remaining.Length > maxChars)
            {
                int cut = remaining.LastIndexOf(
                    ' ', maxChars);

                if (cut < maxChars / 2)
                    cut = maxChars;

                string fragment =
                    remaining[..cut].Trim();

                if (fragment.Length > 0)
                    fragments.Add(fragment);

                remaining =
                    remaining[cut..].TrimStart();
            }

            if (remaining.Length > 0)
                fragments.Add(remaining);
        }

        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var fragment in fragments)
        {
            if (current.Length > 0 &&
                current.Length + fragment.Length + 1 > maxChars)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
                current.Append(' ');

            current.Append(fragment);
        }

        if (current.Length > 0)
            chunks.Add(current.ToString());

        return chunks;
    }
}
