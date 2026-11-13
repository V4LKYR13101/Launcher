using Android.Content;
using LauncherApp.Services;

namespace LauncherApp;

public partial class MainActivity
{
    private void RenderGrayscaleSetup()
    {
        Title("App grayscale");
        Paragraph("Choose an app’s display colour by holding its tile. This changes the opened app, independently of its icon.");
        SettingRow(body,"1. Foreground app detection",GrayscaleService.IsEnabled(this)?"Enabled":"Enable Launcher app grayscale in Android accessibility settings",()=>
            Confirm("Foreground app detection","Launcher uses window-change events to identify the app in front. It does not read screen content, record your activity or upload this information.","Open Android settings",()=>OpenIntent(new Intent(Android.Provider.Settings.ActionAccessibilitySettings))),"settings");
        SettingRow(body,"2. Display permission",GrayscaleService.HasSystemAccess(this)?"Granted":"One-time permission from your computer",()=>
            Sheet("Display permission",(panel,dialog)=>
            {
                Paragraph("Connect the phone to your computer with USB debugging enabled. Run this command once:",panel);
                var command=Text("adb shell pm grant com.launcher.home android.permission.WRITE_SECURE_SETTINGS",14);
                command.SetTextIsSelectable(true);panel.AddView(command,Match());
                Paragraph("Then return here. Android does not offer a normal permission popup for changing display colours.",panel);
                panel.AddView(Button("Copy command",()=>
                {((ClipboardManager)GetSystemService(ClipboardService)!).PrimaryClip=ClipData.NewPlainText("Launcher display permission",command.Text);Toast("Command copied.");}),Match());
            }),"settings");
        Gap(body);Paragraph(GrayscaleService.Ready(this)?"Setup complete. Per-app grayscale choices are ready for you to try.":"Both setup steps are needed before a saved grayscale choice can affect the display.");
        Paragraph("Colour correction applies to the whole display while the selected app is in front. Split-screen windows share that colour state. Launcher restores the previous setting when you leave. Your Samsung’s display behaviour still needs your testing.");
        body.AddView(Button("Turn off service and restore colours",()=>{GrayscaleService.StopAndRestore();Render();}),Match());
        Gap(body);body.AddView(Button("Android colour correction",()=>OpenIntent(new Intent(Android.Provider.Settings.ActionAccessibilitySettings))),Match());
    }
}
