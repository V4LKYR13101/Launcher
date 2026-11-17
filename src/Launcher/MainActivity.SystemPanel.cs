using Android.Content;
using Android.App;
using Android.Widget;
using LauncherApp.Services;
namespace LauncherApp;
public partial class MainActivity
{
    private void HandleSystemPanelIntent(Intent? intent)
    {
        string? key=intent?.GetStringExtra("system_panel_notification"),reply=intent?.GetStringExtra("system_panel_reply"),player=intent?.GetStringExtra("system_panel_player");
        intent?.RemoveExtra("system_panel_notification");intent?.RemoveExtra("system_panel_reply");intent?.RemoveExtra("system_panel_player");
        if(key is not null||reply is not null||player is not null)handler.Post(()=>
        {
            // Refresh rules before authenticating an overlay entry point.
            state.Apps=store.Load().Apps;
            if(player is not null)WithAppUnlock(player,()=>{var launch=PackageManager?.GetLaunchIntentForPackage(player);if(launch is not null)OpenIntent(launch);});
            else if(ConversationService.NotificationSnapshot().FirstOrDefault(n=>n.Key==(key??reply)) is {} notification)
            {if(reply is not null)ReplyTo(notification);else OpenNotification(notification);}
        });
    }
    private Dialog? systemPanelSettings;
    private LinearLayout? systemPanelSettingsContent;
    private void RefreshSystemPanelSettings()
    {
        if(systemPanelSettings?.IsShowing!=true)return;
        int position=(systemPanelSettingsContent?.Parent as ScrollView)?.ScrollY??0;
        systemPanelSettings.Dismiss();handler.Post(()=>{ShowSystemPanelSettings();if(systemPanelSettingsContent?.Parent is ScrollView view)view.Post(()=>view.ScrollTo(0,position));});
    }
    private void ShowSystemPanelSettings()
    {
        if(systemPanelSettings?.IsShowing==true)return;
        systemPanelSettings=Sheet("System panel",(panel,dialog)=>
    {
        systemPanelSettingsContent=panel;
        Paragraph("A matching status strip and quick panel across your apps. Swipe down on the small edge handle to open it. The native Android panel remains available.",panel);
        ToggleRow(panel,"Enable system panel",state.SystemPanelEnabled,v=>{state.SystemPanelEnabled=v;Save();});
        SettingRow(panel,"Accessibility access",SystemPanelService.IsEnabled(this)?"Connected":"Required · enable Launcher system panel",()=>OpenIntent(new Intent(Android.Provider.Settings.ActionAccessibilitySettings)),"settings");
        Paragraph("If Android blocks access for an APK installed through your browser, open Launcher’s App info and allow restricted settings, then return here.",panel);
        SettingRow(panel,"Notification access",ConversationService.Enabled(this)?"Connected":"Required for notifications and media",()=>OpenIntent(new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS")),"message");
        SettingRow(panel,"Brightness access",Android.Provider.Settings.System.CanWrite(this)?"Allowed":"Optional · change system brightness",()=>OpenIntent(new Intent(Android.Provider.Settings.ActionManageWriteSettings,Android.Net.Uri.Parse("package:"+PackageName))),"sun");
        SettingRow(panel,"Connection names","Optional · nearby devices and Wi-Fi name",RequestConnectionDetails,"wifi");
        Paragraph("The optional status strip draws over other apps. Below-bar placement can cover app headers; turn it off if it gets in the way. Top placement leaves Android’s right-hand indicator area visible.",panel);
        ToggleRow(panel,"Show status strip",state.SystemPanelStatus,v=>{state.SystemPanelStatus=v;Save();});
        ToggleRow(panel,"Place below Android status bar",state.SystemPanelBelowStatus,v=>{state.SystemPanelBelowStatus=v;Save();});
        ToggleRow(panel,"Panel animations",state.SystemPanelAnimations,v=>{state.SystemPanelAnimations=v;Save();});
        SettingRow(panel,"Handle position",state.SystemPanelSide,()=>Choose("Handle position",["Left","Right"],i=>{state.SystemPanelSide=i==0?"Left":"Right";Save();RefreshSystemPanelSettings();}),"more");
        SettingRow(panel,"Handle distance",state.SystemPanelOffset+" dp below Android’s status bar",()=>Choose("Handle distance",["Near · 32 dp","Middle · 48 dp","Lower · 80 dp"],i=>{state.SystemPanelOffset=new[]{32,48,80}[i];Save();RefreshSystemPanelSettings();}),"more");
        SettingRow(panel,"Panel theme",state.SystemPanelTheme,()=>Choose("Panel theme",["Follow Launcher","Dark","Light"],i=>{state.SystemPanelTheme=new[]{"Follow Launcher","Dark","Light"}[i];Save();RefreshSystemPanelSettings();}),"sun");
        SettingRow(panel,"Surface opacity",state.SystemPanelOpacity+"%",()=>Choose("Surface opacity",["100%","95%","90%"],i=>{state.SystemPanelOpacity=new[]{100,95,90}[i];Save();RefreshSystemPanelSettings();}),"photo");
        SettingRow(panel,"Control order",string.Join(" · ",state.SystemPanelControls??[]),()=>Choose("Control order",["Connections first","Display first","Sound first"],i=>{state.SystemPanelControls=i==1?["Display","Battery","Wi-Fi","Bluetooth","Sound","Settings"]:i==2?["Sound","Bluetooth","Wi-Fi","Display","Battery","Settings"]:["Wi-Fi","Bluetooth","Sound","Display","Battery","Settings"];Save();RefreshSystemPanelSettings();}),"grid");
        SettingRow(panel,"Open panel","Preview after enabling the switch and accessibility access",()=>{dialog.Dismiss();if(!SystemPanelService.Open())Toast("Enable System panel and its accessibility access first");},"phone");
        Paragraph("The handle uses only its small visible area. The strip does not accept touches. Android navigation stays available. The panel hides when the phone locks, the screen turns off or Android opens protected system controls. Samsung behaviour still needs device verification.",panel);
        SettingRow(panel,"Disable accessibility service","Remove the panel and disconnect its service",()=>{state.SystemPanelEnabled=false;Save();SystemPanelService.Disconnect();dialog.Dismiss();},"settings");
    });
    }
}
