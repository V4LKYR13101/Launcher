using Android.Graphics;
using Microsoft.ML.OnnxRuntime;

namespace LauncherApp.Services.ImageSearch;

// YuNet detection and EdgeFace-S recognition using the exports in Lumen's model catalog.
internal sealed class PhotoFaceReader : IDisposable
{
    internal sealed record Face(float[] Embedding, byte[] Preview);
    private sealed record Candidate(RectF Bounds, float Score, float[] Landmarks);
    private static readonly float[] Template = [38.2946f, 51.6963f, 73.5318f, 51.5014f, 56.0252f, 71.7366f, 41.5493f, 92.3655f, 70.7299f, 92.2041f];
    private readonly InferenceSession detector, recognizer;
    private readonly Dictionary<string, int> layers;

    internal PhotoFaceReader(string models)
    {
        detector = ClipEncoder.Open(models, "yunet.onnx");
        layers = detector.OutputNames.Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index);
        try { recognizer = ClipEncoder.Open(models, "edgeface_s.onnx"); }
        catch { detector.Dispose(); throw; }
    }

    internal IReadOnlyList<Face> Read(Bitmap image, CancellationToken token)
    {
        var faces = new List<Face>();
        var kept = new List<Candidate>();
        foreach (var candidate in Detect(image).OrderByDescending(candidate => candidate.Score))
        {
            token.ThrowIfCancellationRequested();
            if (kept.Count >= 32)
                break;
            if (kept.Any(face => Overlap(face.Bounds, candidate.Bounds) > .3f))
                continue;
            kept.Add(candidate);
            using var crop = Align(image, candidate.Landmarks);
            if (crop is null)
                continue;
            float[] pixels = PhotoModelInput.Channels(crop, 112, 112, false, [1, 1, 1], [0, 0, 0]);
            using var input = OrtValue.CreateTensorValueFromMemory(pixels, [1, 3, 112, 112]);
            using var output = PhotoModelInput.Run(recognizer, input);
            var values = output[0].GetTensorDataAsSpan<float>().ToArray();
            float norm = MathF.Sqrt(values.Sum(value => value * value));
            if (values.Length != 512 || norm <= 0 || !float.IsFinite(norm))
                continue;
            for (int i = 0; i < values.Length; i++)
                values[i] /= norm;
            using var preview = new MemoryStream();
            crop.Compress(Bitmap.CompressFormat.Jpeg!, 75, preview);
            faces.Add(new(values, preview.ToArray()));
        }
        return faces;
    }

    private List<Candidate> Detect(Bitmap image)
    {
        int width = (image.Width + 31) / 32 * 32, height = (image.Height + 31) / 32 * 32;
        float[] pixels = PhotoModelInput.Channels(image, width, height, true, [1, 1, 1], [0, 0, 0]);
        using var input = OrtValue.CreateTensorValueFromMemory(pixels, [1, 3, height, width]);
        using var result = PhotoModelInput.Run(detector, input);
        var candidates = new List<Candidate>();
        foreach (int stride in new[] { 8, 16, 32 })
        {
            int columns = width / stride, cells = columns * (height / stride);
            var cls = result[layers[$"cls_{stride}"]].GetTensorDataAsSpan<float>();
            var obj = result[layers[$"obj_{stride}"]].GetTensorDataAsSpan<float>();
            var box = result[layers[$"bbox_{stride}"]].GetTensorDataAsSpan<float>();
            var points = result[layers[$"kps_{stride}"]].GetTensorDataAsSpan<float>();
            for (int i = 0; i < cells; i++)
            {
                float score = MathF.Sqrt(Math.Clamp(cls[i], 0, 1) * Math.Clamp(obj[i], 0, 1));
                if (score < .8f)
                    continue;
                int col = i % columns, row = i / columns;
                float x = (col + box[i * 4]) * stride, y = (row + box[i * 4 + 1]) * stride;
                float w = MathF.Exp(box[i * 4 + 2]) * stride, h = MathF.Exp(box[i * 4 + 3]) * stride;
                if (Math.Min(w, h) < 40 || x < 0 || y < 0 || x >= image.Width || y >= image.Height)
                    continue;
                var landmarks = new float[10];
                for (int k = 0; k < landmarks.Length; k += 2)
                {
                    landmarks[k] = (points[i * 10 + k] + col) * stride;
                    landmarks[k + 1] = (points[i * 10 + k + 1] + row) * stride;
                }
                candidates.Add(new(new RectF(x - w / 2, y - h / 2, x + w / 2, y + h / 2), score, landmarks));
            }
        }
        return candidates;
    }

    private static float Overlap(RectF a, RectF b)
    {
        float width = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
        float height = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
        float area = width * height;
        return area / (a.Width() * a.Height() + b.Width() * b.Height() - area);
    }

    private static Bitmap? Align(Bitmap image, float[] landmarks)
    {
        float sourceX = 0, sourceY = 0, targetX = 0, targetY = 0;
        for (int i = 0; i < 10; i += 2)
        {
            sourceX += landmarks[i] / 5;
            sourceY += landmarks[i + 1] / 5;
            targetX += Template[i] / 5;
            targetY += Template[i + 1] / 5;
        }
        float dot = 0, cross = 0, norm = 0;
        for (int i = 0; i < 10; i += 2)
        {
            float x = landmarks[i] - sourceX, y = landmarks[i + 1] - sourceY;
            float u = Template[i] - targetX, v = Template[i + 1] - targetY;
            dot += x * u + y * v;
            cross += x * v - y * u;
            norm += x * x + y * y;
        }
        if (norm <= 0 || !float.IsFinite(norm))
            return null;
        float a = dot / norm, b = cross / norm;
        using var matrix = new Matrix();
        matrix.SetValues([a, -b, targetX - a * sourceX + b * sourceY, b, a, targetY - b * sourceX - a * sourceY, 0, 0, 1]);
        var result = Bitmap.CreateBitmap(112, 112, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(result);
        using var paint = new Paint(PaintFlags.FilterBitmap);
        canvas.DrawBitmap(image, matrix, paint);
        return result;
    }

    public void Dispose()
    {
        recognizer.Dispose();
        detector.Dispose();
    }
}
