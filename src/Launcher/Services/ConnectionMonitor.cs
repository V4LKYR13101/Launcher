using Android;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;

namespace LauncherApp.Services;

public sealed class ConnectionMonitor : BroadcastReceiver, IBluetoothProfileServiceListener
{
    private readonly Context context;
    private readonly Action changed;
    private readonly Dictionary<ProfileType,IBluetoothProfile> profiles=new();
    private BluetoothAdapter? Adapter=>((BluetoothManager?)context.GetSystemService(Context.BluetoothService))?.Adapter;
    private bool registered;
    public bool CanReadBluetooth=>!OperatingSystem.IsAndroidVersionAtLeast(31)||context.CheckSelfPermission(Manifest.Permission.BluetoothConnect)==Permission.Granted;
    public bool CanReadWifiName=>context.CheckSelfPermission(Manifest.Permission.AccessFineLocation)==Permission.Granted;
    public ConnectionMonitor(Context context,Action changed){this.context=context;this.changed=changed;}
    public void Start()
    {
        if(!registered)
        {
            var filter=new IntentFilter();filter.AddAction(WifiManager.WifiStateChangedAction);filter.AddAction(WifiManager.NetworkStateChangedAction);
            filter.AddAction(BluetoothAdapter.ActionStateChanged);filter.AddAction(BluetoothAdapter.ActionConnectionStateChanged);
            filter.AddAction(BluetoothDevice.ActionAclConnected);filter.AddAction(BluetoothDevice.ActionAclDisconnected);
            if(OperatingSystem.IsAndroidVersionAtLeast(33))context.RegisterReceiver(this,filter,ReceiverFlags.Exported);
            else context.RegisterReceiver(this,filter);
            registered=true;
        }
        if(!CanReadBluetooth)return;
        try
        {
            foreach(var profile in new[]{ProfileType.Headset,ProfileType.A2dp})
                if(!profiles.ContainsKey(profile))Adapter?.GetProfileProxy(context,this,profile);
        }
        catch(Java.Lang.SecurityException){}
    }
    public void Stop()
    {
        if(registered){context.UnregisterReceiver(this);registered=false;}
        foreach(var (type,proxy) in profiles)
            try{Adapter?.CloseProfileProxy(type,proxy);}catch(Java.Lang.SecurityException){}
        profiles.Clear();
    }
    public override void OnReceive(Context? context,Intent? intent)=>changed();
    public void OnServiceConnected(ProfileType profile,IBluetoothProfile? proxy)
    {
        if(proxy is null)return;
        if(!registered){try{Adapter?.CloseProfileProxy(profile,proxy);}catch(Java.Lang.SecurityException){}return;}
        profiles[profile]=proxy;changed();
    }
    public void OnServiceDisconnected(ProfileType profile){profiles.Remove(profile);changed();}
    public (bool Active,string Detail) Wifi()
    {
        try
        {
            var wifi=(WifiManager?)context.ApplicationContext!.GetSystemService(Context.WifiService);
            bool active=wifi?.IsWifiEnabled==true;
            if(!active)return(false,"Off");
#pragma warning disable CA1422 // Current connection only; this API provides the permission-gated SSID on Android 7+.
            var info=wifi?.ConnectionInfo;
#pragma warning restore CA1422
            string? name=info?.SSID?.Trim('"');
            if(!string.IsNullOrWhiteSpace(name)&&name!="<unknown ssid>")return(true,name);
            var network=(ConnectivityManager?)context.GetSystemService(Context.ConnectivityService);
            bool connected=network?.GetNetworkCapabilities(network.ActiveNetwork)?.HasTransport(TransportType.Wifi)==true;
            return(true,connected?(CanReadWifiName?"Connected":"Connected · show names"):"On · not connected");
        }
        catch(Java.Lang.SecurityException){return(false,"Show connection details");}
    }
    public (bool Active,string Detail) Bluetooth()
    {
        if(!CanReadBluetooth)return(false,"Allow nearby devices");
        try
        {
            if(Adapter?.IsEnabled!=true)return(false,"Off");
            var names=profiles.Values.SelectMany(p=>p.ConnectedDevices??[]).DistinctBy(d=>d.Address)
                .Select(d=>d.Name).Where(n=>!string.IsNullOrWhiteSpace(n)).ToArray();
            return(true,names.Length>0?string.Join(", ",names):"On · not connected");
        }
        catch(Java.Lang.SecurityException){return(false,"Allow nearby devices");}
    }
    public string? AudioDeviceName()
    {
        if(!CanReadBluetooth)return null;
        try{return profiles.GetValueOrDefault(ProfileType.A2dp)?.ConnectedDevices?.Select(d=>d.Name).FirstOrDefault(n=>!string.IsNullOrWhiteSpace(n));}
        catch(Java.Lang.SecurityException){return null;}
    }
}
