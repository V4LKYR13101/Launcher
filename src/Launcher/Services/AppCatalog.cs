using Android.App;
using Android.App.Usage;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Launcher.Core;

namespace LauncherApp.Services;

public static class AppCatalog
{
    public static List<AppEntry> Load(Context context)
    {
        var pm = context.PackageManager!;
        var intent = new Intent(Intent.ActionMain);
        intent.AddCategory(Intent.CategoryLauncher);
        return (pm.QueryIntentActivities(intent, PackageInfoFlags.MatchAll) ?? [])
            .Where(r => r.ActivityInfo?.PackageName != context.PackageName && r.ActivityInfo?.Enabled == true)
            .Select(r =>
            {
                var a = r.ActivityInfo!;
                return new AppEntry(a.PackageName + "/" + a.Name, a.PackageName!, a.Name!,
                    r.LoadLabel(pm) ?? a.PackageName!, OperatingSystem.IsAndroidVersionAtLeast(26) ? (int)a.ApplicationInfo!.Category : -1);
            }).DistinctBy(a => a.Id).OrderBy(a => a.Label).ToList();
    }

    public static bool HasUsageAccess(Context context)
    {
        var ops = (AppOpsManager?)context.GetSystemService(Context.AppOpsService);
        return ops?.CheckOpNoThrow("android:get_usage_stats", Android.OS.Process.MyUid(), context.PackageName!) == AppOpsManagerMode.Allowed;
    }

    public static Dictionary<string, long> UsageToday(Context context)
    {
        if (!HasUsageAccess(context)) return new();
        var manager = (UsageStatsManager?)context.GetSystemService(Context.UsageStatsService);
        long start = new DateTimeOffset(DateTime.Today).ToUnixTimeMilliseconds();
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        // Aggregate the event timeline within today's exact local boundaries. Daily
        // query buckets can include time before midnight, so they are not used here.
        var events = manager?.QueryEvents(start - 86_400_000, now);
        var totals = new Dictionary<string, long>();
        var active = new Dictionary<string, long>();
        if (events is null) return totals;
        var item = new UsageEvents.Event();
        while (events.HasNextEvent)
        {
            events.GetNextEvent(item);
            if ((int)item.EventType is 15 or 26) // screen non-interactive / device shutdown
            {
                foreach (var (key, beganAt) in active)
                    totals[key] = totals.GetValueOrDefault(key) + Math.Max(0, item.TimeStamp - beganAt);
                active.Clear();
                continue;
            }
            string? package = item.PackageName;
            if (package is null) continue;
            if ((int)item.EventType == 1) active.TryAdd(package, Math.Max(start, item.TimeStamp));
            else if ((int)item.EventType == 2 && active.Remove(package, out long began))
                totals[package] = totals.GetValueOrDefault(package) + Math.Max(0, item.TimeStamp - began);
        }
        foreach (var (key, beganAt) in active)
            totals[key] = totals.GetValueOrDefault(key) + Math.Max(0, now - beganAt);
        return totals;
    }
}
