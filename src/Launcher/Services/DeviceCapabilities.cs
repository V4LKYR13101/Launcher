using Android.App;
using Android.Content;
using Android.OS;
namespace LauncherApp.Services;

public sealed class DeviceCapabilities(Context context)
{
    public bool LowMemoryDevice=>(context.GetSystemService(Context.ActivityService) as ActivityManager)?.IsLowRamDevice==true;
    public bool PowerSaving=>(context.GetSystemService(Context.PowerService) as PowerManager)?.IsPowerSaveMode==true;
    public bool AllowDepth=>!LowMemoryDevice&&!PowerSaving;
    public bool SecureLock=>(context.GetSystemService(Context.KeyguardService) as KeyguardManager)?.IsDeviceSecure==true;
    public bool Notifications=>!OperatingSystem.IsAndroidVersionAtLeast(33)||context.CheckSelfPermission(Android.Manifest.Permission.PostNotifications)==Android.Content.PM.Permission.Granted;
    public bool ExactTimers=>!OperatingSystem.IsAndroidVersionAtLeast(31)||(context.GetSystemService(Context.AlarmService) as AlarmManager)?.CanScheduleExactAlarms()==true;
}
