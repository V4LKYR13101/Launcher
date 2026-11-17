using Android.Views;
using Android.Widget;
using LauncherApp.Services;
namespace LauncherApp;
public partial class MainActivity
{
    private readonly Dictionary<string,string> packageLabels=[];
    private string PackageLabel(string package)
    {
        if(apps.FirstOrDefault(a=>a.PackageName==package) is {} entry)return entry.Label;
        if(packageLabels.TryGetValue(package,out string? label))return label;
        try{var info=PackageManager!.GetApplicationInfo(package,Android.Content.PM.PackageInfoFlags.MetaData);label=PackageManager.GetApplicationLabel(info);}
        catch(Android.Content.PM.PackageManager.NameNotFoundException){label=package.Split('.').Last();}
        return packageLabels[package]=string.IsNullOrWhiteSpace(label)?package:label;
    }
    private View NotificationCard(Conversation item,bool actions=false,bool swipe=false,bool compact=false)
    {
        string app=PackageLabel(item.Package);
        bool concealed=ConcealPreview(item.Package);
        var card=Stack();card.Background=Background(palette.Card,12,palette.Divider);card.SetPadding(Dp(14),Dp(compact?10:14),Dp(14),Dp(actions?8:14));card.Tag="notification:"+item.Key;TouchFeedback(card);
        var source=Stack(true);source.SetGravity(GravityFlags.CenterVertical);var icon=NotificationIcon(item.Package);icon.ContentDescription=app;
        source.AddView(icon,new LinearLayout.LayoutParams(Dp(20),Dp(20)));var origin=Text(app,11,true);origin.SetPadding(Dp(8),0,Dp(8),0);origin.SetSingleLine(true);origin.Ellipsize=Android.Text.TextUtils.TruncateAt.End;source.AddView(origin,new LinearLayout.LayoutParams(0,-2,1));
        source.AddView(Text(DateTimeOffset.FromUnixTimeMilliseconds(item.Time).ToLocalTime().ToString("HH:mm"),10,color:palette.Muted));card.AddView(source,Match());
        var title=Text(concealed?"Protected notification":item.Title,15,true);title.SetPadding(0,Dp(compact?6:10),0,0);title.SetMaxLines(2);title.Ellipsize=Android.Text.TextUtils.TruncateAt.End;card.AddView(title,Match());
        if(item.Text.Length>0||concealed){var preview=Text(concealed?"Unlock to open this notification.":item.Text,13,color:palette.Muted);preview.SetLineSpacing(Dp(2),1);preview.SetMaxLines(3);preview.Ellipsize=Android.Text.TextUtils.TruncateAt.End;preview.SetPadding(0,Dp(6),0,0);card.AddView(preview,Match());}
        if(actions)
        {
            var buttons=new List<(string Title,Action Action)>();
            if(ConversationService.CanReply(item.Key))buttons.Add(("Reply",()=>ReplyTo(item)));
            if(state.KeepMessageHistory)buttons.Add(("History",()=>WithAppUnlock(item.Package,()=>ShowMessageHistory(item.Package,ConversationService.Thread(item)))));
            if(buttons.Count>0)
            {
                var controls=Stack(true);controls.SetGravity(GravityFlags.Right);controls.SetPadding(0,Dp(4),0,0);
                foreach(var button in buttons){var action=RootAction(button.Title,button.Action);action.TextSize=12;controls.AddView(action,new LinearLayout.LayoutParams(-2,-2));}
                card.AddView(controls,Match());
            }
        }
        card.Click+=(_,_)=>OpenNotification(item);
        BindHoldHaptics(card);card.LongClick+=(_,_)=>Choose(app,OperatingSystem.IsAndroidVersionAtLeast(26)?["Open notification",item.Dismissible?"Dismiss notification":"Persistent notification","Snooze for one hour"]:["Open notification",item.Dismissible?"Dismiss notification":"Persistent notification"],i=>
        {if(i==0)OpenNotification(item);else if(i==2){if(!ConversationService.Snooze(item.Key,3600000))Toast("Android could not snooze this notification.");}else if(item.Dismissible)ConversationService.Dismiss(item.Key);else Toast("Android keeps this notification while its service is active.");});
        if(swipe)card.SetOnTouchListener(new LauncherApp.UI.NotificationSwipeListener(Dp(90),item.Dismissible,()=>ConversationService.Dismiss(item.Key),()=>Toast("Android keeps this notification while its service is active."),(view,success)=>Haptic(view,success?HapticCue.Commit:HapticCue.Reject)));
        return card;
    }
}
