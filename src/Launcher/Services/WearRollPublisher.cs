using Android.Content;
using Android.Gms.Extensions;
using Android.Gms.Wearable;
using Android.OS;
using Launcher.Core;
using LauncherWear;

namespace LauncherApp.Services;

internal static class WearRollPublisher
{
    private sealed record Snapshot(string Clock, string Layout, string Date, string AmbientDate,
                                   bool Activity, int Count, int Percent, long Deadline);
    private static Context? context;
    private static Handler? main;
    private static long deadline;
    private static bool publishing;
    private static Snapshot? sent;
    private static long sentAt;
    private static bool scheduled;
    private static int publishPosted;
    private static bool force;
    private const long ProgressInterval = 8_000;

    internal static ISharedPreferences Settings(Context c) =>
        c.GetSharedPreferences(RollSync.Preferences, FileCreationMode.Private)!;

    // Subscribe once for the process. The app's downloader can keep working while its activity is stopped.
    internal static void Start(Context c, IEnumerable<WorkspaceTimer> timers)
    {
        if (context is null)
        {
            context = c.ApplicationContext!;
            main = new Handler(Looper.MainLooper!);
            ActiveDownloads.Changed += Changed;
        }
        deadline = timers.Where(t => t.EndsAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            .Select(t => t.EndsAt).DefaultIfEmpty(0).Min();
        Changed();
    }

    // Also resend after the watch companion is newly installed or upgraded.
    internal static void SyncNow(Context c, IEnumerable<WorkspaceTimer> timers)
    {
        force = true;
        Start(c, timers);
    }

    internal static string AccentFor(Context c, string accent) =>
        Settings(c).GetBoolean(RollSync.Accent, true) ? accent : "#E4FF00";

    private static void Changed()
    {
        if (main is null || Interlocked.Exchange(ref publishPosted, 1) != 0) return;
        main.Post(() =>
        {
            Interlocked.Exchange(ref publishPosted, 0);
            Publish();
        });
    }

    private static async void Publish()
    {
        if (context is null || publishing) return;
        var prefs = Settings(context);
        bool show = prefs.GetBoolean(RollSync.Activity, false);
        var transfers = show ? ActiveDownloads.Snapshot() : [];
        int percent = transfers.FirstOrDefault()?.Percent ?? -1;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var snapshot = new Snapshot(
            prefs.GetString(RollSync.Clock, "watch") ?? "watch",
            prefs.GetString(RollSync.Layout, "watch") ?? "watch",
            prefs.GetString(RollSync.Date, "watch") ?? "watch",
            prefs.GetString(RollSync.AmbientDate, "watch") ?? "watch",
            show, transfers.Length, percent, show && deadline > now ? deadline : 0);
        bool forced = force;
        bool progressOnly = !forced && sent is not null && (sent with { Percent = snapshot.Percent }) == snapshot;
        long age = SystemClock.ElapsedRealtime() - sentAt;
        if (!forced && sent == snapshot && (snapshot.Count == 0 || age < 45_000)) return;
        if (progressOnly && age < ProgressInterval)
        {
            if (!scheduled)
            {
                scheduled = true;
                main!.PostDelayed(() => { scheduled = false; Publish(); }, ProgressInterval - age);
            }
            return;
        }
        publishing = true;
        force = false;
        bool success = false;
        try
        {
            using var client = WearableClass.GetDataClient(context);
            using var request = PutDataMapRequest.Create(RollSync.Path)!;
            var map = request.DataMap!;
            map.PutString(RollSync.Clock, snapshot.Clock);
            map.PutString(RollSync.Layout, snapshot.Layout);
            map.PutString(RollSync.Date, snapshot.Date);
            map.PutString(RollSync.AmbientDate, snapshot.AmbientDate);
            map.PutBoolean(RollSync.Activity, snapshot.Activity);
            map.PutInt(RollSync.DownloadCount, snapshot.Count);
            map.PutInt(RollSync.DownloadPercent, snapshot.Percent);
            map.PutLong(RollSync.Deadline, snapshot.Deadline);
            map.PutLong(RollSync.SampledAt, now);
            using var itemRequest = request.AsPutDataRequest()!;
            if (!progressOnly) itemRequest.SetUrgent();
            using var item = await client.PutDataItem(itemRequest)!.AsAsync<IDataItem>();
            sent = snapshot;
            sentAt = SystemClock.ElapsedRealtime();
            success = true;
        }
        catch (Exception e) { Android.Util.Log.Debug("LauncherWear", "Watch face sync unavailable: " + e.GetType().Name); }
        finally
        {
            publishing = false;
            if (success)
            {
                // Reconcile changes that arrived while a Data Layer write was in flight.
                Changed();
            }
        }
    }
}
