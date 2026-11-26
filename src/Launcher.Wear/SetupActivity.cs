using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace LauncherWear;

[Activity(Label = "Launcher for Wear OS", MainLauncher = true, Exported = true,
    Theme = "@android:style/Theme.DeviceDefault.NoActionBar")]
public sealed class SetupActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        int padding = (int)(Resources!.DisplayMetrics!.Density * 28 + .5f);
        var instructions = new TextView(this)
        {
            Text = OperatingSystem.IsAndroidVersionAtLeast(36)
                ? "On your phone, open Launcher → Settings → Wear OS → Install Launcher Roll."
                : "Installing faces from Launcher requires Wear OS 6 or later.",
            TextSize = 16,
            Gravity = GravityFlags.Center
        };
        instructions.SetPadding(padding, padding, padding, padding);
        SetContentView(instructions);
    }
}
