using Android.App.Usage;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.OS.Storage;

namespace LauncherApp.Services;
public sealed record UsageProfile(long[] Hours,long[] Days,string Version,string Source,long? Storage,long? Data);
public static class UsageProfiles
{
    public static UsageProfile Read(Context context,string package)
    {
        var manager=(UsageStatsManager)context.GetSystemService(Context.UsageStatsService)!;
        long now=DateTimeOffset.Now.ToUnixTimeMilliseconds(),today=new DateTimeOffset(DateTime.Today).ToUnixTimeMilliseconds(),start=new DateTimeOffset(DateTime.Today.AddDays(-6)).ToUnixTimeMilliseconds();
        var hours=new long[24];var days=new long[7];
        if(AppCatalog.HasUsageAccess(context))
        {
            
            void Add(long begin,long end)
            {
                begin=Math.Max(begin,start);end=Math.Min(end,now);
                while(begin<end)
                {
                    var local=DateTimeOffset.FromUnixTimeMilliseconds(begin).LocalDateTime;
                    long boundary=new DateTimeOffset(local.Date.AddHours(local.Hour+1)).ToUnixTimeMilliseconds();
                    long stop=Math.Min(end,Math.Max(begin+1,boundary));int day=(local.Date-DateTime.Today.AddDays(-6)).Days;
                    if(day>=0&&day<7)days[day]+=stop-begin;
                    if(local.Date==DateTime.Today)hours[local.Hour]+=stop-begin;begin=stop;
                }
            }
            foreach(var span in UsageTimeline.Read(context,start,now).Where(s=>s.Package==package))Add(span.Begin,span.End);
        }
        var pm=context.PackageManager!;var info=pm.GetPackageInfo(package,PackageInfoFlags.Activities)!;
        string source="Unknown install source";
        try{source=OperatingSystem.IsAndroidVersionAtLeast(30)?pm.GetInstallSourceInfo(package)?.InstallingPackageName??"Installed directly":pm.GetInstallerPackageName(package)??"Installed directly";}catch(Java.Lang.RuntimeException){}
        try{if(source.Contains('.'))source="Installed from "+(pm.GetApplicationLabel(pm.GetApplicationInfo(source,PackageInfoFlags.MetaData)!)??source);}catch(Java.Lang.Exception){}
        long? storage=null,data=null;
        if(OperatingSystem.IsAndroidVersionAtLeast(26)&&AppCatalog.HasUsageAccess(context))
        {
            try{var stats=((StorageStatsManager)context.GetSystemService(Context.StorageStatsService)!).QueryStatsForPackage(StorageManager.UuidDefault!,package,Android.OS.Process.MyUserHandle()!);storage=stats!.AppBytes+stats.DataBytes;}catch(Java.Lang.Exception){}
            try
            {
                var network=(NetworkStatsManager)context.GetSystemService(Context.NetworkStatsService)!;long bytes=0;
                foreach(var type in new[]{Android.Net.ConnectivityType.Wifi,Android.Net.ConnectivityType.Mobile})
                {
                    using var stats=network.QueryDetailsForUid(type,null,start,now,info.ApplicationInfo!.Uid);
                    var bucket=new NetworkStats.Bucket();while(stats?.HasNextBucket==true){stats.GetNextBucket(bucket);bytes+=bucket.RxBytes+bucket.TxBytes;}
                }
                data=bytes;
            }
            catch(Java.Lang.Exception){}
        }
        return new(hours,days,info.VersionName??"Unknown",source,storage,data);
    }
}
