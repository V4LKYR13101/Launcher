using Android.Content;
using Android.Gms.Extensions;
using Android.Gms.Wearable;
using Android.Util;
using LauncherWear;
using System.Globalization;

namespace LauncherApp.Services;

internal static class WearAccentPublisher
{
    private static bool publishing;
    private static int? desiredColor;
    private static int? publishedColor;

    // All callers run on the main thread. Keep at most one Data Layer write in flight.
    public static async void Publish(Context context, string hex)
    {
        if (LauncherDistribution.IsPlay) return;
        if (hex.Length != 7 || hex[0] != '#' ||
            !int.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb))
            return;

        desiredColor = rgb | unchecked((int)0xFF000000);
        if (publishing || desiredColor == publishedColor)
            return;

        publishing = true;
        try
        {
            using var client = WearableClass.GetDataClient(context.ApplicationContext!);
            while (desiredColor is int color && color != publishedColor)
            {
                using var mapRequest = PutDataMapRequest.Create(AccentData.Path)!;
                mapRequest.DataMap!.PutInt(AccentData.ColorKey, color);
                using var request = mapRequest.AsPutDataRequest()!;
                request.SetUrgent();
                using var item = await client.PutDataItem(request)!.AsAsync<IDataItem>();
                publishedColor = color;
            }
        }
        catch (Exception error)
        {
            Log.Debug("LauncherWear", $"Wear accent sync unavailable: {error.Message}");
        }
        finally
        {
            publishing = false;
        }
    }
}
