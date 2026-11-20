using Android.Content;
using Android.Hardware;
namespace LauncherApp.Services;

public sealed class DepthMotionMonitor : Java.Lang.Object, ISensorEventListener
{
    private readonly SensorManager? manager;
    private readonly Sensor? sensor;
    private readonly Action<float, float> changed;
    private readonly float[] matrix = new float[9], angles = new float[3];
    private bool baseline;
    private float pitch, roll, x, y;
    public bool Available => sensor is not null;
    public bool Running
    {
        get; private set;
    }
    public DepthMotionMonitor(Context context, Action<float, float> onChange)
    {
        manager = context.GetSystemService(Context.SensorService) as SensorManager;
        sensor = manager?.GetDefaultSensor(SensorType.GameRotationVector) ?? manager?.GetDefaultSensor(SensorType.RotationVector);
        changed = onChange;
    }
    public void Start()
    {
        if (Running)
            return;
        baseline = false;
        x = y = 0;
        try
        {
            Running = sensor is not null && manager?.RegisterListener(this, sensor, SensorDelay.Ui) == true;
        }
        catch (Java.Lang.RuntimeException) { Running = false; }
    }
    public void Recenter()
    {
        baseline = false;
        x = y = 0;
    }
    public void Stop()
    {
        if (Running)
            manager?.UnregisterListener(this);
        Running = false;
        baseline = false;
        x = y = 0;
    }
    public void OnSensorChanged(SensorEvent? e)
    {
        if (!Running || e?.Values is not { Count: >= 3 } values)
            return;
        var vector = values.ToArray();
        if (vector.Any(v => !float.IsFinite(v)))
            return;
        SensorManager.GetRotationMatrixFromVector(matrix, vector);
        SensorManager.GetOrientation(matrix, angles);
        if (!float.IsFinite(angles[1]) || !float.IsFinite(angles[2]))
            return;
        if (!baseline)
        {
            pitch = angles[1];
            roll = angles[2];
            baseline = true;
            return;
        }
        static float Delta(float a, float b) => MathF.Atan2(MathF.Sin(a - b), MathF.Cos(a - b));
        float targetX = Math.Clamp(Delta(angles[2], roll) / .23f, -1, 1), targetY = Math.Clamp(Delta(angles[1], pitch) / .23f, -1, 1);
        x += (targetX - x) * .14f;
        y += (targetY - y) * .14f;
        changed(x, y);
    }
    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy)
    {
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Stop();
        base.Dispose(disposing);
    }
}
