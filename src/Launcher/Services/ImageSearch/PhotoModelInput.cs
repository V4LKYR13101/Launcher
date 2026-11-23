using Android.Graphics;
using Microsoft.ML.OnnxRuntime;

namespace LauncherApp.Services.ImageSearch;

internal static class PhotoModelInput
{
    internal static Bitmap? Decode(string path, int maxSide)
    {
        using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeFile(path, bounds)?.Dispose();
        int longest = Math.Max(bounds.OutWidth, bounds.OutHeight);
        if (longest < 1)
            return null;
        int sample = 1;
        while (longest / sample > maxSide * 2)
            sample *= 2;
        using var options = new BitmapFactory.Options { InSampleSize = sample, InPreferredConfig = Bitmap.Config.Argb8888 };
        var source = BitmapFactory.DecodeFile(path, options);
        if (source is null)
            return null;
        using var matrix = new Matrix();
        ClipEncoder.ApplyOrientation(path, matrix);
        float scale = Math.Min(1, (float)maxSide / Math.Max(source.Width, source.Height));
        matrix.PostScale(scale, scale);
        var result = Bitmap.CreateBitmap(source, 0, 0, source.Width, source.Height, matrix, true);
        if (!ReferenceEquals(result, source))
            source.Dispose();
        return result;
    }

    internal static float[] Channels(Bitmap frame, int width, int height, bool bgr, float[] scale, float[] offset)
    {
        int plane = width * height;
        var pixels = new int[frame.Width * frame.Height];
        var input = new float[plane * 3];
        frame.GetPixels(pixels, 0, frame.Width, 0, 0, frame.Width, frame.Height);
        for (int y = 0; y < frame.Height; y++)
        {
            for (int x = 0; x < frame.Width; x++)
            {
                int pixel = pixels[y * frame.Width + x];
                int index = y * width + x;
                input[index] = ((pixel >> (bgr ? 0 : 16)) & 255) * scale[0] + offset[0];
                input[index + plane] = ((pixel >> 8) & 255) * scale[1] + offset[1];
                input[index + plane * 2] = ((pixel >> (bgr ? 16 : 0)) & 255) * scale[2] + offset[2];
            }
        }
        return input;
    }

    internal static IDisposableReadOnlyCollection<OrtValue> Run(InferenceSession session, OrtValue input)
    {
        using var options = new RunOptions();
        return session.Run(options, new Dictionary<string, OrtValue> { [session.InputNames[0]] = input }, session.OutputNames);
    }
}
