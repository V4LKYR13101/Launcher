using Android.App;
using Android.Content;
using Android.OS;
namespace LauncherApp.Services;

public sealed record ResourceSnapshot(long At,long MemoryTotal,long MemoryAvailable,long StorageTotal,long StorageAvailable,double? Battery,double? Temperature,string BatteryState)
{
    public double? Memory=>MemoryTotal>0?100d*(MemoryTotal-MemoryAvailable)/MemoryTotal:null;
    public double? Storage=>StorageTotal>0?100d*(StorageTotal-StorageAvailable)/StorageTotal:null;
}

public static class PhoneResources
{
    public static ResourceSnapshot Read(Context context)
    {
        long total=0,available=0,storage=0,free=0;double? battery=null,temperature=null;string status="Unavailable";
        try
        {
            using var info=new ActivityManager.MemoryInfo();
            (context.GetSystemService(Context.ActivityService) as ActivityManager)?.GetMemoryInfo(info);
            total=Math.Max(0,info.TotalMem);available=Math.Clamp(info.AvailMem,0,total);
        }
        catch(Java.Lang.RuntimeException){}
        try
        {
            using var disk=new StatFs(context.FilesDir!.AbsolutePath);
            storage=Math.Max(0,disk.TotalBytes);free=Math.Clamp(disk.AvailableBytes,0,storage);
        }
        catch(Java.Lang.RuntimeException){}
        try
        {
            using var intent=context.RegisterReceiver(null,new IntentFilter(Intent.ActionBatteryChanged));
            if(intent?.GetBooleanExtra(BatteryManager.ExtraPresent,true)==true)
            {
                int level=intent.GetIntExtra(BatteryManager.ExtraLevel,-1),scale=intent.GetIntExtra(BatteryManager.ExtraScale,0);
                if(level>=0&&scale>0)battery=Math.Clamp(100d*level/scale,0,100);
                if(intent.HasExtra(BatteryManager.ExtraTemperature))temperature=intent.GetIntExtra(BatteryManager.ExtraTemperature,0)/10d;
                status=intent.GetIntExtra(BatteryManager.ExtraStatus,0) switch {2=>"Charging",3=>"On battery",4=>"Not charging",5=>"Full",_=>"Unavailable"};
            }
        }
        catch(Java.Lang.RuntimeException){}
        return new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),total,available,storage,free,battery,temperature,status);
    }
    public static string Size(long bytes)=>bytes>=1_000_000_000?$"{bytes/1_000_000_000d:0.#} GB":$"{bytes/1_000_000d:0} MB";
}
