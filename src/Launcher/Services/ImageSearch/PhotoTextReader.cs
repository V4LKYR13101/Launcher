using System.Text;
using Android.Graphics;
using Microsoft.ML.OnnxRuntime;
using Path = System.IO.Path;

namespace LauncherApp.Services.ImageSearch;

// PP-OCRv6 small CPU exports use BGR input and a CTC recognition head.
internal sealed class PhotoTextReader : IDisposable
{
    private readonly InferenceSession detector, recognizer;
    private readonly string[] characters;

    internal PhotoTextReader(string models)
    {
        characters = File.ReadLines(Path.Combine(models, "rec.yml"))
            .SkipWhile(line => line.Trim() != "character_dict:").Skip(1)
            .TakeWhile(line => line.StartsWith("  - ", StringComparison.Ordinal))
            .Select(line => line[4..])
            .Select(value => value.Length >= 2 && value[0] == '\'' && value[^1] == '\'' ? value[1..^1].Replace("''", "'") : value)
            .Prepend("").ToArray();
        if (characters.Length < 2)
            throw new InvalidDataException("The OCR character list is missing.");
        detector = ClipEncoder.Open(models, "det.onnx");
        try { recognizer = ClipEncoder.Open(models, "rec_int8.onnx"); }
        catch { detector.Dispose(); throw; }
    }

    internal string Read(Bitmap image, CancellationToken token)
    {
        float scale = Math.Min(1, 960f / Math.Max(image.Width, image.Height));
        int width = Math.Max(32, (int)MathF.Round(image.Width * scale / 32) * 32);
        int height = Math.Max(32, (int)MathF.Round(image.Height * scale / 32) * 32);
        var frame = Bitmap.CreateScaledBitmap(image, width, height, true);
        float[] pixels;
        try
        {
            pixels = PhotoModelInput.Channels(frame, width, height, true,
                [1 / (255f * .229f), 1 / (255f * .224f), 1 / (255f * .225f)],
                [-.485f / .229f, -.456f / .224f, -.406f / .225f]);
        }
        finally { if (!ReferenceEquals(image, frame)) frame.Dispose(); }
        using var input = OrtValue.CreateTensorValueFromMemory(pixels, [1, 3, height, width]);
        using var result = PhotoModelInput.Run(detector, input);
        var boxes = FindLines(result[0].GetTensorDataAsSpan<float>(), width, height, token);
        var text = new StringBuilder();
        foreach (var box in boxes)
        {
            token.ThrowIfCancellationRequested();
            string line = Recognize(image, box, width, height);
            if (line.Length > 0)
                text.AppendLine(line);
        }
        return text.ToString().Trim();
    }

    private static List<(int Left, int Top, int Right, int Bottom)> FindLines(ReadOnlySpan<float> map, int width, int height, CancellationToken token)
    {
        if (map.Length != width * height)
            throw new InvalidDataException("Unexpected OCR detector output.");
        var seen = new bool[map.Length];
        var work = new Stack<int>();
        var lines = new List<(int, int, int, int)>();
        for (int i = 0; i < map.Length && lines.Count < 300; i++)
        {
            if (i % width == 0)
                token.ThrowIfCancellationRequested();
            if (seen[i] || map[i] <= .2f)
                continue;
            work.Push(i);
            seen[i] = true;
            int left = width, right = 0, top = height, bottom = 0, count = 0;
            float confidence = 0;
            while (work.TryPop(out int pixel))
            {
                int x = pixel % width, y = pixel / width;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
                confidence += map[pixel];
                count++;
                for (int yy = Math.Max(0, y - 1); yy <= Math.Min(height - 1, y + 1); yy++)
                for (int xx = Math.Max(0, x - 1); xx <= Math.Min(width - 1, x + 1); xx++)
                {
                    int next = yy * width + xx;
                    if (!seen[next] && map[next] > .2f)
                    {
                        seen[next] = true;
                        work.Push(next);
                    }
                }
            }
            if (count < 4 || confidence / count < .45f)
                continue;
            int margin = Math.Max(2, (int)MathF.Ceiling(Math.Min(right - left + 1, bottom - top + 1) * .55f));
            lines.Add((Math.Max(0, left - margin), Math.Max(0, top - margin),
                Math.Min(width, right + margin + 1), Math.Min(height, bottom + margin + 1)));
        }
        return lines;
    }

    private string Recognize(Bitmap image, (int Left, int Top, int Right, int Bottom) box, int detWidth, int detHeight)
    {
        int left = box.Left * image.Width / detWidth, top = box.Top * image.Height / detHeight;
        int width = Math.Clamp(box.Right * image.Width / detWidth - left, 1, image.Width - left);
        int height = Math.Clamp(box.Bottom * image.Height / detHeight - top, 1, image.Height - top);
        bool vertical = height > width * 1.5f;
        int lineWidth = Math.Clamp((int)MathF.Ceiling(48f * (vertical ? (float)height / width : (float)width / height)), 8, 3200);
        using var matrix = new Matrix();
        if (vertical)
            matrix.PostRotate(-90);
        matrix.PostScale((float)lineWidth / (vertical ? height : width), 48f / (vertical ? width : height));
        var line = Bitmap.CreateBitmap(image, left, top, width, height, matrix, true);
        float[] pixels;
        try { pixels = PhotoModelInput.Channels(line, lineWidth, 48, true, [1 / 127.5f, 1 / 127.5f, 1 / 127.5f], [-1, -1, -1]); }
        finally { if (!ReferenceEquals(line, image)) line.Dispose(); }
        using var input = OrtValue.CreateTensorValueFromMemory(pixels, [1, 3, 48, lineWidth]);
        using var output = PhotoModelInput.Run(recognizer, input);
        var shape = output[0].GetTensorTypeAndShape();
        int classes = (int)shape.Shape[^1];
        var scores = output[0].GetTensorDataAsSpan<float>();
        if (classes != characters.Length + 1 || scores.Length % classes != 0)
            throw new InvalidDataException("Unexpected OCR recognition output.");
        var text = new StringBuilder();
        int previous = 0, count = 0;
        float confidence = 0;
        for (int offset = 0; offset < scores.Length; offset += classes)
        {
            int best = 0;
            for (int c = 1; c < classes; c++)
                if (scores[offset + c] > scores[offset + best])
                    best = c;
            if (best != 0 && best != previous)
            {
                text.Append(best < characters.Length ? characters[best] : " ");
                confidence += scores[offset + best];
                count++;
            }
            previous = best;
        }
        string value = text.ToString().Trim();
        return count > 0 && confidence / count >= .5f && value.Any(char.IsLetterOrDigit) ? value : "";
    }

    public void Dispose()
    {
        recognizer.Dispose();
        detector.Dispose();
    }
}
