using Android.Content;
using Android.Hardware;
namespace LauncherApp.Services;
public sealed class AmbientLightMonitor:Java.Lang.Object,ISensorEventListener
{
    private readonly SensorManager? manager;
    private readonly Sensor? sensor;
    public bool Available=>sensor is not null;
    public bool Running {get;private set;}
    public float? Lux {get;private set;}
    public AmbientLightMonitor(Context context)
    {manager=(SensorManager?)context.GetSystemService(Context.SensorService);sensor=manager?.GetDefaultSensor(SensorType.Light);}
    public bool Start()
    {
        if(Running)return true;Lux=null;
        try{Running=sensor is not null&&manager?.RegisterListener(this,sensor,SensorDelay.Normal)==true;}
        catch(Java.Lang.RuntimeException){Running=false;}
        return Running;
    }
    public void Stop(){if(Running)manager?.UnregisterListener(this);Running=false;Lux=null;}
    public void OnSensorChanged(SensorEvent? e)
    {if(Running&&e?.Values is {Count:>0} values&&float.IsFinite(values[0])&&values[0]>=0)Lux=values[0];}
    public void OnAccuracyChanged(Sensor? sensor,SensorStatus accuracy){}
    protected override void Dispose(bool disposing){if(disposing)Stop();base.Dispose(disposing);}
}
