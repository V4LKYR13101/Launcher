using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using AndroidX.Wear.WatchFace.Complications.Data;
using AndroidX.Wear.WatchFace.Complications.Datasource;

namespace LauncherWear;

[Service(Name = ComponentName, Exported = true, Label = "Launcher accent",
    Permission = "com.google.android.wearable.permission.BIND_COMPLICATION_PROVIDER")]
[IntentFilter(["android.support.wearable.complications.ACTION_COMPLICATION_UPDATE_REQUEST"])]
[MetaData("android.support.wearable.complications.SUPPORTED_TYPES", Value = "SMALL_IMAGE")]
[MetaData("android.support.wearable.complications.UPDATE_PERIOD_SECONDS", Value = "0")]
public sealed class AccentComplicationService : ComplicationDataSourceService
{
    internal const string ComponentName = "com.launcher.wear.AccentComplicationService";

    public override void OnComplicationRequest(ComplicationRequest request, IComplicationRequestListener listener)
    {
        using var preferences = GetSharedPreferences("launcher-theme", FileCreationMode.Private);
        listener.OnComplicationData(BuildData(preferences!.GetInt(AccentData.ColorKey, AccentData.DefaultColor)));
    }

    public override ComplicationData? GetPreviewData(ComplicationType type) => BuildData(AccentData.DefaultColor);

    private static ComplicationData BuildData(int color)
    {
        var bitmap = Bitmap.CreateBitmap(212, 212, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(bitmap);
        using var paint = new Paint(PaintFlags.AntiAlias);
        int rgb = color & 0x00FFFFFF;
        DrawHalo(canvas, paint, rgb, 44, 48);
        DrawHalo(canvas, paint, rgb, 121, 42);
        using var divider = new LinearGradient(18, 0, 194, 0,
            [rgb, rgb | unchecked((int)0xB0000000), rgb | unchecked((int)0xFF000000),
             rgb | unchecked((int)0xB0000000), rgb], [0f, .32f, .5f, .68f, 1f], Shader.TileMode.Clamp!);
        paint.SetShader(divider);
        canvas.DrawRect(18, 205, 194, 207, paint);
        var icon = Icon.CreateWithBitmap(bitmap)!;
        var image = new SmallImage.Builder(icon, SmallImageType.Photo!).Build();
        return new SmallImageComplicationData.Builder(image, ComplicationText.Empty!).Build();
    }

    private static void DrawHalo(Canvas canvas, Paint paint, int rgb, int centerY, int radius)
    {
        using var halo = new RadialGradient(106, centerY, radius,
            [rgb | 0x56000000, rgb | 0x26000000, rgb], [0f, .46f, 1f], Shader.TileMode.Clamp!);
        paint.SetShader(halo);
        canvas.DrawCircle(106, centerY, radius, paint);
    }
}
