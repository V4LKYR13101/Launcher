using Android.Widget;
using Android.Views;
using Android.Content.Res;
namespace LauncherApp;
public partial class MainActivity
{
    private void ShowTileIconSize()
    {
        int selected=state.TileIconScale;TextView? value=null;SeekBar? slider=null;
        bool showUsage=state.ShowUsage&&LauncherApp.Services.AppCatalog.HasUsageAccess(this);
        var previewApps=apps.Where(a=>!state.Apps[a.Id].Hidden).Take(3).ToArray();
        var previewSizes=new List<Action<int>>();
        void Preview()
        {
            if(value is null)return;
            value.Text=selected+"%"+(selected==100?" · default":"");
            foreach(var resize in previewSizes)resize(RequestedTileIconSize(selected));
        }
        Sheet("Tile icon size",(panel,dialog)=>
        {
            Paragraph("Resize app icons inside standard and expanded tiles. Icons fit the available space; tile dimensions and grid spacing stay the same.",panel);
            value=Text("",22,true,palette.Accent);value.Gravity=GravityFlags.Center;panel.AddView(value,Match());
            var preview=Stack(true);preview.SetPadding(0,Dp(14),0,Dp(14));panel.AddView(preview,Match());
            foreach(var app in previewApps)
            {
                var card=Stack();card.SetPadding(Dp(6),Dp(6),Dp(6),Dp(10));card.Background=Background(palette.Card,state.CornerRadius);card.SetGravity(GravityFlags.Center);
                var slot=new FrameLayout(this);previewSizes.Add(FitTileIcon(slot,AppIcon(app,palette.Text),RequestedTileIconSize(selected)));card.AddView(slot,new LinearLayout.LayoutParams(-1,0,1));
                if(state.ShowLabels){var label=Text(Launcher.Core.AppOrganization.DisplayName(state,app),11);label.Gravity=GravityFlags.Center;label.SetSingleLine(true);label.Ellipsize=Android.Text.TextUtils.TruncateAt.End;card.AddView(label,Match());}
                if(showUsage){var time=Text(Launcher.Core.AppOrganization.FormatDuration(usage.GetValueOrDefault(app.PackageName)),9,color:palette.Muted);time.Gravity=GravityFlags.Center;card.AddView(time,Match());}
                var lp=new LinearLayout.LayoutParams(0,Dp(state.TileHeight),1);lp.SetMargins(Dp(4),0,Dp(4),0);preview.AddView(card,lp);
            }
            slider=new SeekBar(this){Max=90,Progress=selected-60,ContentDescription="Tile icon size"};slider.ProgressTintList=ColorStateList.ValueOf(palette.Accent);slider.ThumbTintList=ColorStateList.ValueOf(palette.Accent);slider.HapticFeedbackEnabled=false;
            slider.ProgressChanged+=(_,e)=>{selected=e.Progress+60;Preview();};slider.StopTrackingTouch+=(_,_)=>Haptic(slider,HapticCue.Selection);panel.AddView(slider,Match(Dp(48)));
            var limits=Stack(true);limits.AddView(Text("60%",11,color:palette.Muted),new LinearLayout.LayoutParams(0,-2,1));limits.AddView(Text("150%",11,color:palette.Muted));panel.AddView(limits,Match());
            var reset=Text("Reset to default",13,true,palette.Accent);reset.Gravity=GravityFlags.Center;reset.SetMinimumHeight(Dp(48));TouchFeedback(reset);reset.Click+=(_,_)=>{slider.Progress=40;};panel.AddView(reset,Match());Preview();
        },(footer,dialog)=>DialogActions(footer,("Cancel",dialog.Dismiss),("Save",()=>{state.TileIconScale=selected;Save();dialog.Dismiss();Render();})));
    }
}
