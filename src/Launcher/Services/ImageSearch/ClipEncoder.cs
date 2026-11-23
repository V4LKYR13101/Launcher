using Android.Graphics;
using Android.Media;
using Microsoft.ML.OnnxRuntime;
using Path = System.IO.Path;

namespace LauncherApp.Services.ImageSearch;

internal static class ClipEncoder
{
    internal const int Dimensions = 512;
    private const int Side = 224;

    internal static InferenceSession Open(string directory, string model)
    {
        using var options = new SessionOptions { IntraOpNumThreads = 2, InterOpNumThreads = 1 };
        return new InferenceSession(Path.Combine(directory, model), options);
    }

    internal static float[] Text(InferenceSession session, ClipTokenizer tokenizer, string query)
    {
        int[] ids = tokenizer.Encode(query);
        using var input = session.InputMetadata.First().Value.ElementType == typeof(long)
            ? OrtValue.CreateTensorValueFromMemory(Array.ConvertAll(ids, id => (long)id), [1, ids.Length])
            : OrtValue.CreateTensorValueFromMemory(ids, [1, ids.Length]);
        return Run(session, input);
    }

    internal static float[]? Image(InferenceSession session, string path)
    {
        using var crop = Thumbnail(path, Side);
        if (crop is null)
            return null;
        const int plane = Side * Side;
        var argb = new int[plane];
        crop.GetPixels(argb, 0, Side, 0, 0, Side, Side);
        var pixels = new float[3 * plane];
        for (int i = 0; i < plane; i++)
        {
            int value = argb[i];
            pixels[i] = (((value >> 16) & 255) / 255f - 0.48145466f) / 0.26862954f;
            pixels[i + plane] = (((value >> 8) & 255) / 255f - 0.4578275f) / 0.26130258f;
            pixels[i + 2 * plane] = ((value & 255) / 255f - 0.40821073f) / 0.27577711f;
        }
        using var input = OrtValue.CreateTensorValueFromMemory(pixels, [1, 3, Side, Side]);
        return Run(session, input);
    }

    internal static Bitmap? Thumbnail(string path, int side)
    {
        using var probe = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeFile(path, probe)?.Dispose();
        int shortest = Math.Min(probe.OutWidth, probe.OutHeight);
        if (shortest <= 0)
            return null;

        int sample = 1;
        while (shortest / sample >= side * 2)
            sample *= 2;
        while (Math.Max(probe.OutWidth, probe.OutHeight) / sample > 4096)
            sample *= 2;
        using var options = new BitmapFactory.Options { InSampleSize = sample, InPreferredConfig = Bitmap.Config.Argb8888 };
        using var source = BitmapFactory.DecodeFile(path, options);
        if (source is null)
            return null;

        int cropSide = Math.Min(source.Width, source.Height);
        using var transform = new Matrix();
        ApplyOrientation(path, transform);
        transform.PostScale((float)side / cropSide, (float)side / cropSide);
        var crop = Bitmap.CreateBitmap(source, (source.Width - cropSide) / 2, (source.Height - cropSide) / 2, cropSide, cropSide, transform, true);
        return ReferenceEquals(crop, source) ? source.Copy(Bitmap.Config.Argb8888!, false) : crop;
    }

    internal static void ApplyOrientation(string path, Matrix transform)
    {
        int orientation;
        try
        {
            using var exif = new ExifInterface(path);
            orientation = exif.GetAttributeInt(ExifInterface.TagOrientation, 1);
        }
        catch (Java.IO.IOException) { return; }

        if (orientation is 2 or 4 or 5 or 7)
            transform.PostScale(-1, 1);
        transform.PostRotate(orientation switch { 3 or 4 => 180, 5 or 8 => 270, 6 or 7 => 90, _ => 0 });
    }

    private static float[] Run(InferenceSession session, OrtValue input)
    {
        using var options = new RunOptions();
        var inputs = new Dictionary<string, OrtValue> { [session.InputNames[0]] = input };
        using var output = session.Run(options, inputs, session.OutputNames);
        float[] result = output[0].GetTensorDataAsSpan<float>().ToArray();
        float norm = MathF.Sqrt(result.Sum(value => value * value));
        if (result.Length != Dimensions || norm <= 0 || !float.IsFinite(norm))
            throw new InvalidDataException("The image search model returned an invalid embedding.");
        for (int i = 0; i < result.Length; i++)
            result[i] /= norm;
        return result;
    }
}
