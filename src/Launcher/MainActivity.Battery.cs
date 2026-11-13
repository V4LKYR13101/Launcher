using Android.Content;
using Android.OS;
using Android.Widget;
using LauncherApp.UI;

namespace LauncherApp;

public partial class MainActivity
{
    private BatteryView? batteryIcon;
    private TextView? batteryPercentText;
    private BatteryReceiver? batteryReceiver;
    private void StartBatteryUpdates()
    {
        if(batteryReceiver is not null)return;
        batteryReceiver=new BatteryReceiver(UpdateBatteryUi);
        if(OperatingSystem.IsAndroidVersionAtLeast(33))RegisterReceiver(batteryReceiver,new IntentFilter(Intent.ActionBatteryChanged),ReceiverFlags.NotExported);
        else RegisterReceiver(batteryReceiver,new IntentFilter(Intent.ActionBatteryChanged));
    }
    private void StopBatteryUpdates()
    {
        if(batteryReceiver is null)return;UnregisterReceiver(batteryReceiver);batteryReceiver=null;
    }
    private void UpdateBatteryUi(Intent? intent=null)
    {
        using var current=intent is null?RegisterReceiver(null,new IntentFilter(Intent.ActionBatteryChanged)):null;
        var data=intent??current;if(data is null)return;
        int scale=data.GetIntExtra(BatteryManager.ExtraScale,100),level=data.GetIntExtra(BatteryManager.ExtraLevel,0);
        int percent=scale>0?Math.Clamp(level*100/scale,0,100):0;
        bool charging=data.GetIntExtra(BatteryManager.ExtraStatus,0)==2;
        if(batteryPercentText is not null)batteryPercentText.Text=percent+"%";
        batteryIcon?.SetState(percent,charging);
    }
    private sealed class BatteryReceiver(Action<Intent?> changed):BroadcastReceiver
    {public override void OnReceive(Context? context,Intent? intent)=>changed(intent);}
}
