using Android.App;
using Android.Content;
using Android.Gms.Wearable;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Wear.WatchFace.Complications.Data;
using AndroidX.Wear.WatchFace.Complications.Datasource;

namespace LauncherWear;

internal static class RollData
{
    internal static ISharedPreferences Settings(Context c) =>
        c.GetSharedPreferences(RollSync.Preferences, FileCreationMode.Private)!;
    internal static string Style(ISharedPreferences p) => RollSync.Style(
        p.GetString(RollSync.Clock, "watch")!, p.GetString(RollSync.Layout, "watch")!,
        p.GetString(RollSync.Date, "watch")!, p.GetString(RollSync.AmbientDate, "watch")!);
    internal static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    internal static bool Downloading(ISharedPreferences p) =>
        p.GetInt(RollSync.DownloadCount, 0) > 0 && p.GetLong(RollSync.SampledAt, 0) > Now - 120_000;
    internal static string DownloadLabel(ISharedPreferences p, bool detail = false)
    {
        int count = p.GetInt(RollSync.DownloadCount, 0);
        int percent = p.GetInt(RollSync.DownloadPercent, -1);
        if (detail) return count == 1 ? (percent < 0 ? "Downloading…" : $"{percent}% downloaded")
            : (percent < 0 ? $"{count} downloads" : $"{count} downloads · {percent}% first");
        return percent < 0 ? "DL…" : $"↓ {percent}%";
    }
    internal static void Refresh(Context context, string provider)
    {
        using var name = new ComponentName(context.PackageName!, provider);
        ComplicationDataSourceUpdateRequester.Create(context, name).RequestUpdateAll();
    }
}

[Service(Exported = true)]
[IntentFilter(["com.google.android.gms.wearable.DATA_CHANGED"], DataScheme = "wear", DataHost = "*", DataPath = RollSync.Path)]
public sealed class RollListenerService : WearableListenerService
{
    public override void OnDataChanged(DataEventBuffer events)
    {
        foreach (var update in events)
        {
            if (update.Type != DataEvent.TypeChanged || update.DataItem?.Uri?.Path != RollSync.Path) continue;
            using var item = DataMapItem.FromDataItem(update.DataItem);
            using var map = item?.DataMap;
            if (map is null || !map.ContainsKey(RollSync.SampledAt)) continue;
            using var p = RollData.Settings(this);
            long receivedAt = map.GetLong(RollSync.SampledAt);
            if (receivedAt < p.GetLong(RollSync.SampledAt, 0)) continue;
            string before = RollData.Style(p);
            int oldCount = p.GetInt(RollSync.DownloadCount, 0);
            long oldDeadline = p.GetLong(RollSync.Deadline, 0);
            bool enabled = map.GetBoolean(RollSync.Activity);
            int count = enabled ? Math.Max(0, map.GetInt(RollSync.DownloadCount)) : 0;
            long deadline = enabled ? map.GetLong(RollSync.Deadline) : 0;
            using var e = p.Edit();
            e!.PutString(RollSync.Clock, map.GetString(RollSync.Clock));
            e.PutString(RollSync.Layout, map.GetString(RollSync.Layout));
            e.PutString(RollSync.Date, map.GetString(RollSync.Date));
            e.PutString(RollSync.AmbientDate, map.GetString(RollSync.AmbientDate));
            e.PutInt(RollSync.DownloadCount, count);
            e.PutInt(RollSync.DownloadPercent, Math.Clamp(map.GetInt(RollSync.DownloadPercent), -1, 100));
            e.PutBoolean(RollSync.Activity, enabled);
            e.PutLong(RollSync.Deadline, deadline);
            e.PutLong(RollSync.SampledAt, receivedAt);
            long now = RollData.Now;
            bool refresh = oldCount != count || oldDeadline != deadline
                || (count > 0 && now - p.GetLong("last_push", 0) >= 300_000);
            if (refresh) e.PutLong("last_push", now);
            e.Apply();
            if (before != RollData.Style(p)) RollData.Refresh(this, RollStyleService.ComponentName);
            if (refresh) RollData.Refresh(this, RollActivityService.ComponentName);
        }
    }
}

[Service(Name = ComponentName, Exported = true, Label = "Launcher Roll settings",
    Permission = "com.google.android.wearable.permission.BIND_COMPLICATION_PROVIDER")]
[IntentFilter(["android.support.wearable.complications.ACTION_COMPLICATION_UPDATE_REQUEST"])]
[MetaData("android.support.wearable.complications.SUPPORTED_TYPES", Value = "SHORT_TEXT")]
[MetaData("android.support.wearable.complications.UPDATE_PERIOD_SECONDS", Value = "0")]
public sealed class RollStyleService : ComplicationDataSourceService
{
    internal const string ComponentName = "com.launcher.wear.RollStyleService";
    public override void OnComplicationRequest(ComplicationRequest request, IComplicationRequestListener listener)
    {
        using var p = RollData.Settings(this);
        listener.OnComplicationData(Data(RollData.Style(p)));
    }
    public override ComplicationData? GetPreviewData(ComplicationType type) => Data("wwww");
    private static ComplicationData Data(string value) =>
        new ShortTextComplicationData.Builder(new PlainComplicationText.Builder(value).Build(),
            new PlainComplicationText.Builder("Launcher Roll appearance").Build()).Build();
}

[Service(Name = ComponentName, Exported = true, Label = "Launcher Activity",
    Permission = "com.google.android.wearable.permission.BIND_COMPLICATION_PROVIDER")]
[IntentFilter(["android.support.wearable.complications.ACTION_COMPLICATION_UPDATE_REQUEST"])]
[MetaData("android.support.wearable.complications.SUPPORTED_TYPES", Value = "SHORT_TEXT")]
[MetaData("android.support.wearable.complications.UPDATE_PERIOD_SECONDS", Value = "300")]
public sealed class RollActivityService : ComplicationDataSourceService
{
    internal const string ComponentName = "com.launcher.wear.RollActivityService";
    public override void OnComplicationRequest(ComplicationRequest request, IComplicationRequestListener listener)
    {
        using var p = RollData.Settings(this);
        var now = RollData.Now;
        long deadline = p.GetLong(RollSync.Deadline, 0);
        if (RollData.Downloading(p))
        {
            listener.OnComplicationData(Data(new PlainComplicationText.Builder(RollData.DownloadLabel(p)).Build(), "Phone download", p.GetLong(RollSync.SampledAt, 0) + 330_000));
        }
        else if (deadline > now)
        {
            var remaining = new TimeDifferenceComplicationText.Builder(TimeDifferenceStyle.ShortSingleUnit!,
                new CountDownTimeReference(Java.Time.Instant.OfEpochMilli(deadline)!)).Build();
            listener.OnComplicationData(Data(remaining, "Phone timer", deadline));
        }
        else
        {
            using var battery = (BatteryManager?)GetSystemService(BatteryService);
            int percent = battery?.GetIntProperty((int)BatteryProperty.Capacity) ?? -1;
            listener.OnComplicationData(percent is >= 0 and <= 100
                ? Data(new PlainComplicationText.Builder($"{percent}%").Build(), "Watch battery")
                : new NoDataComplicationData());
        }
    }
    public override ComplicationData? GetPreviewData(ComplicationType type) =>
        Data(new PlainComplicationText.Builder("↓ 48%").Build(), "Phone download");
    private ComplicationData Data(IComplicationText text, string description, long expiresAt = 0)
    {
        using var intent = new Intent(this, typeof(RollActivity));
        var tap = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var builder = new ShortTextComplicationData.Builder(text, new PlainComplicationText.Builder(description).Build())
            .SetTapAction(tap);
        if (expiresAt > 0)
            builder.SetValidTimeRange(TimeRange.Before(Java.Time.Instant.OfEpochMilli(expiresAt)!));
        return builder.Build();
    }
}

[Activity(Label = "Launcher Activity", Exported = false, Theme = "@android:style/Theme.DeviceDefault.NoActionBar")]
public sealed class RollActivity : Activity
{
    private Handler? main;
    private TextView? display;
    private bool visible;
    protected override void OnCreate(Bundle? saved)
    {
        base.OnCreate(saved);
        main = new Handler(Looper.MainLooper!);
        display = new TextView(this) { TextSize = 19, Gravity = GravityFlags.Center };
        int pad = (int)(Resources!.DisplayMetrics!.Density * 30);
        display.SetPadding(pad, pad, pad, pad);
        SetContentView(display);
    }
    protected override void OnResume() { base.OnResume(); visible = true; Tick(); }
    protected override void OnPause() { visible = false; main?.RemoveCallbacksAndMessages(null); base.OnPause(); }
    private void Tick()
    {
        if (!visible || display is null) return;
        using var p = RollData.Settings(this);
        long left = p.GetLong(RollSync.Deadline, 0) - RollData.Now;
        string status = RollData.Downloading(p) ? RollData.DownloadLabel(p, true) : left > 0
            ? "Phone timer\n" + TimeSpan.FromMilliseconds(left).ToString(left >= 3_600_000 ? @"h\:mm\:ss" : @"m\:ss")
            : "No active phone downloads or timers";
        if (display.Text != status) display.Text = status;
        main!.PostDelayed(Tick, 1000);
    }
}
