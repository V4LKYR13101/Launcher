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
        var entries=(pm.QueryIntentActivities(intent, PackageInfoFlags.MatchAll) ?? [])
            .Where(r => r.ActivityInfo?.PackageName != context.PackageName && r.ActivityInfo?.Enabled == true)
            .Select(r =>
            {
                var a = r.ActivityInfo!;
                return new AppEntry(a.PackageName + "/" + a.Name, a.PackageName!, a.Name!,
                    r.LoadLabel(pm) ?? a.PackageName!, OperatingSystem.IsAndroidVersionAtLeast(26) ? (int)a.ApplicationInfo!.Category : -1);
            }).DistinctBy(a => a.Id).ToList();
        var launcher=context.GetSystemService(Context.LauncherAppsService) as LauncherApps;
        foreach(var profile in ProfileCatalog.Profiles(context).Where(p=>!p.User.Equals(Android.OS.Process.MyUserHandle())&&!p.Quiet&&p.Unlocked))
        {
            try
            {
                foreach(var activity in launcher?.GetActivityList(null,profile.User)??[])
                {
                    var component=activity.ComponentName;if(component is null||component.PackageName==context.PackageName)continue;
                    string label=activity.Label?.ToString()??component.PackageName;if(!profile.Private)label+=" · Work";
                    entries.Add(new(component.PackageName+"/"+component.ClassName+"@profile:"+profile.Serial,component.PackageName,component.ClassName,label,-1,profile.Serial,profile.Private));
                }
            }
            catch(Java.Lang.SecurityException){}
        }
        var installedPackages=entries.Select(a=>a.PackageName).ToHashSet();
        foreach(var shortcut in new StateStore(context).Load().Shortcuts.Where(s=>installedPackages.Contains(s.Package)))
        {
            var profile=ProfileCatalog.Profiles(context).FirstOrDefault(p=>p.Serial==shortcut.ProfileSerial);
            if(shortcut.ProfileSerial>=0&&(profile is null||profile.Quiet||!profile.Unlocked))continue;
            entries.Add(new(shortcut.Package+"/shortcut:"+shortcut.ShortcutId+"@"+shortcut.ProfileSerial,shortcut.Package,"",shortcut.Label,-1,shortcut.ProfileSerial,profile?.Private==true,shortcut.ShortcutId));
        }
        return entries.OrderBy(a=>a.Label).ToList();
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
        return UsageTimeline.Read(context,start,now).GroupBy(s=>s.Package).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.End-s.Begin));
    }
}
