using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LauncherApp.Services.ImageSearch;

// CLIP byte-level BPE format: https://github.com/openai/CLIP/blob/main/clip/simple_tokenizer.py
internal sealed partial class ClipTokenizer
{
    private const int ContextLength = 77;
    private static readonly char[] ByteAlphabet = MakeByteAlphabet();
    private readonly Dictionary<string, int> vocabulary;
    private readonly Dictionary<(string, string), int> ranks = new();

    internal ClipTokenizer(string directory)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "vocab.json")));
        vocabulary = document.RootElement.EnumerateObject().ToDictionary(token => token.Name, token => token.Value.GetInt32(), StringComparer.Ordinal);
        foreach (string line in File.ReadLines(Path.Combine(directory, "merges.txt")))
        {
            string[] pair = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (pair.Length == 2 && !line.StartsWith("#version", StringComparison.Ordinal))
                ranks.TryAdd((pair[0], pair[1]), ranks.Count);
        }
    }

    internal int[] Encode(string text)
    {
        var ids = new int[ContextLength];
        ids[0] = vocabulary["<|startoftext|>"];
        int position = 1;
        foreach (Match match in Tokens().Matches(text.ToLowerInvariant()))
        {
            foreach (string piece in Merge(Encoding.UTF8.GetBytes(match.Value)))
            {
                if (position == ContextLength - 1)
                    break;
                ids[position++] = vocabulary[piece];
            }
            if (position == ContextLength - 1)
                break;
        }
        ids[position] = vocabulary["<|endoftext|>"];
        return ids;
    }

    private List<string> Merge(byte[] bytes)
    {
        var pieces = bytes.Select(value => ByteAlphabet[value].ToString()).ToList();
        pieces[^1] += "</w>";
        while (pieces.Count > 1)
        {
            (string Left, string Right) best = default;
            int bestRank = int.MaxValue;
            for (int i = 1; i < pieces.Count; i++)
            {
                var pair = (pieces[i - 1], pieces[i]);
                if (ranks.TryGetValue(pair, out int rank) && rank < bestRank)
                {
                    best = pair;
                    bestRank = rank;
                }
            }
            if (bestRank == int.MaxValue)
                break;

            var merged = new List<string>(pieces.Count);
            for (int i = 0; i < pieces.Count; i++)
            {
                if (i + 1 < pieces.Count && pieces[i] == best.Left && pieces[i + 1] == best.Right)
                {
                    merged.Add(best.Left + best.Right);
                    i++;
                }
                else
                    merged.Add(pieces[i]);
            }
            pieces = merged;
        }
        return pieces;
    }

    private static char[] MakeByteAlphabet()
    {
        var alphabet = new char[256];
        int extra = 256;
        for (int value = 0; value < alphabet.Length; value++)
            alphabet[value] = (char)(value is >= 33 and <= 126 or >= 161 and <= 172 or >= 174 and <= 255 ? value : extra++);
        return alphabet;
    }

    [GeneratedRegex("'s|'t|'re|'ve|'m|'ll|'d|[\\p{L}]+|[\\p{N}]|[^\\s\\p{L}\\p{N}]+")]
    private static partial Regex Tokens();
}
