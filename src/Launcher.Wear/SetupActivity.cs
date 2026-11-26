using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using WatchResource = Launcher.Wear.Resource;

namespace LauncherWear;

[Activity(Label = "Launcher for Wear OS", MainLauncher = true, Exported = true,
    Theme = "@android:style/Theme.DeviceDefault.NoActionBar")]
public sealed class SetupActivity : Activity
{
    private TextView? instructions;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        int padding = (int)(Resources!.DisplayMetrics!.Density * 28 + .5f);
        instructions = new TextView(this)
        {
            TextSize = 16,
            Gravity = GravityFlags.Center
        };
        instructions.SetPadding(padding, padding, padding, padding);
        SetContentView(instructions);
    }

    protected override void OnStart()
    {
        base.OnStart();
        RollData.LanguageChanged += OnLanguageChanged;
        RefreshInstructions();
        _ = RollData.RestoreLanguageAsync(ApplicationContext ?? this);
    }

    protected override void OnStop()
    {
        RollData.LanguageChanged -= OnLanguageChanged;
        base.OnStop();
    }

    private void OnLanguageChanged() => RunOnUiThread(RefreshInstructions);

    private void RefreshInstructions()
    {
        using var settings = RollData.Settings(this);
        var message = WearText.For(this, settings).Get(OperatingSystem.IsAndroidVersionAtLeast(36)
            ? WatchResource.String.setup_install
            : WatchResource.String.setup_requires_wear_six);
        instructions!.Text = message;
    }
}
