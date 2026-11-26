using Android.App;
using Android.Content;
using Android.Gms.Extensions;
using Android.Gms.Wearable;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Wear.WatchFace.Complications.Data;
using AndroidX.Wear.WatchFace.Complications.Datasource;
using WatchResource = Launcher.Wear.Resource;

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
    internal static event Action? LanguageChanged;
    internal static void NotifyLanguageChanged() => LanguageChanged?.Invoke();

    // A phone can publish before the companion upgrade. Read the retained item when setup opens.
    internal static async Task RestoreLanguageAsync(Context context)
    {
        try
        {
            using var client = WearableClass.GetDataClient(context);
            using var items = await client.GetDataItems()!.AsAsync<DataItemBuffer>()
                .WaitAsync(TimeSpan.FromSeconds(8));
            using var p = Settings(context);
            string? language = null;
            long sampledAt = p.GetLong(RollSync.SampledAt, 0);
            foreach (var data in items.OfType<IDataItem>())
            {
                if (data.Uri?.Path != RollSync.Path)
                    continue;
                using var item = DataMapItem.FromDataItem(data);
                using var map = item?.DataMap;
                if (map is null || map.GetLong(RollSync.SampledAt) < sampledAt ||
                    WearText.Language(map.GetString(RollSync.Language)) is not { } candidate)
                    continue;

                language = candidate;
                sampledAt = map.GetLong(RollSync.SampledAt);
            }

            if (language is null || language == p.GetString(RollSync.Language, null))
                return;

            using var editor = p.Edit()!;
            editor.PutString(RollSync.Language, language)!.Apply();
            Refresh(context, RollStyleService.ComponentName);
            Refresh(context, RollActivityService.ComponentName);
            NotifyLanguageChanged();
        }
        catch (Exception e) when (e is Java.Lang.RuntimeException or TimeoutException)
        {
            // Keep the last language while the Data Layer is unavailable.
        }
    }

    internal static string DownloadLabel(ISharedPreferences p, WearText text, bool detail = false)
    {
        int count = p.GetInt(RollSync.DownloadCount, 0);
        int percent = p.GetInt(RollSync.DownloadPercent, -1);
        if (detail)
            return count == 1
                ? (percent < 0 ? text.Get(WatchResource.String.downloading) : text.Get(WatchResource.String.downloaded_percent, percent))
                : (percent < 0 ? text.Get(WatchResource.String.download_count, count) : text.Get(WatchResource.String.download_progress, count, percent));

        return percent < 0 ? text.Get(WatchResource.String.download_short) : $"↓ {percent}%";
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
            string? language = WearText.Language(map.GetString(RollSync.Language));
            bool languageChanged = language is not null && language != p.GetString(RollSync.Language, null);
            if (languageChanged)
                e!.PutString(RollSync.Language, language);
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
            if (languageChanged || before != RollData.Style(p)) RollData.Refresh(this, RollStyleService.ComponentName);
            if (languageChanged || refresh) RollData.Refresh(this, RollActivityService.ComponentName);
            if (languageChanged) RollData.NotifyLanguageChanged();
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
        listener.OnComplicationData(Data(RollData.Style(p), WearText.For(this, p)));
    }

    public override ComplicationData? GetPreviewData(ComplicationType type)
    {
        using var p = RollData.Settings(this);
        return Data("wwww", WearText.For(this, p));
    }

    private static ComplicationData Data(string value, WearText text) =>
        new ShortTextComplicationData.Builder(new PlainComplicationText.Builder(value).Build(),
            new PlainComplicationText.Builder(text.Get(WatchResource.String.roll_appearance)).Build()).Build();
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
        var text = WearText.For(this, p);
        var now = RollData.Now;
        long deadline = p.GetLong(RollSync.Deadline, 0);
        if (RollData.Downloading(p))
        {
            listener.OnComplicationData(Data(new PlainComplicationText.Builder(RollData.DownloadLabel(p, text)).Build(), text.Get(WatchResource.String.phone_download), p.GetLong(RollSync.SampledAt, 0) + 330_000));
        }
        else if (deadline > now)
        {
            var remaining = new TimeDifferenceComplicationText.Builder(TimeDifferenceStyle.ShortSingleUnit!,
                new CountDownTimeReference(Java.Time.Instant.OfEpochMilli(deadline)!)).Build();
            listener.OnComplicationData(Data(remaining, text.Get(WatchResource.String.phone_timer), deadline));
        }
        else
        {
            using var battery = (BatteryManager?)GetSystemService(BatteryService);
            int percent = battery?.GetIntProperty((int)BatteryProperty.Capacity) ?? -1;
            listener.OnComplicationData(percent is >= 0 and <= 100
                ? Data(new PlainComplicationText.Builder($"{percent}%").Build(), text.Get(WatchResource.String.watch_battery))
                : new NoDataComplicationData());
        }
    }
    public override ComplicationData? GetPreviewData(ComplicationType type)
    {
        using var p = RollData.Settings(this);
        return Data(new PlainComplicationText.Builder("↓ 48%").Build(),
            WearText.For(this, p).Get(WatchResource.String.phone_download));
    }
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
        var text = WearText.For(this, p);
        long left = p.GetLong(RollSync.Deadline, 0) - RollData.Now;
        string status = RollData.Downloading(p) ? RollData.DownloadLabel(p, text, true) : left > 0
            ? text.Get(WatchResource.String.phone_timer) + "\n" + TimeSpan.FromMilliseconds(left).ToString(left >= 3_600_000 ? @"h\:mm\:ss" : @"m\:ss")
            : text.Get(WatchResource.String.no_phone_activity);
        if (display.Text != status) display.Text = status;
        main!.PostDelayed(Tick, 1000);
    }
}
