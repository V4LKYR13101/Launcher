using Android.Animation;
using Android.Content.PM;
using Android.Graphics;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private FrameLayout? appLayer;
    private LinearLayout? expandedApp;
    private View? selectedAppTile;
    private ScrollView? recessedPage;
    private ValueAnimator? appLayerAnimation;
    private float appLayerFraction;
    private float appLayerStartX,appLayerStartY,appLayerScaleX,appLayerScaleY;
    private bool appLayerCounted,pendingLayerRefresh;
    private Action? pendingAppInspect;
    private void InspectAppTile(AppEntry app,View source)
    {
        if(!LayerMotionEnabled||searching||FontScale>=1.4f){EditApp(app);return;}
        WithAppUnlock(app.PackageName,()=>
        {
            void Continue()
            {
                if(IsDestroyed||!resumed||!IsHomePage||searching||!state.Apps.ContainsKey(app.Id))return;
                var target=source.IsAttachedToWindow?source:root.FindViewWithTag("app-tile:"+app.Id);
                if(target is not null)OpenAppLayer(app,target);
            }
            if(!resumed)pendingAppInspect=Continue;else Continue();
        });
    }
    private void StopAppLayer()
    {
        var animation=appLayerAnimation;appLayerAnimation=null;animation?.Cancel();
        if(selectedAppTile is {} source)source.Alpha=1;
        if(recessedPage is {} pageView)
        {pageView.Alpha=1;pageView.ScaleX=1;pageView.ScaleY=1;pageView.ImportantForAccessibility=ImportantForAccessibility.Auto;}
        if(appLayer is {} layer)root.RemoveView(layer);
        pendingLayerRefresh=false;appLayer=null;expandedApp=null;selectedAppTile=null;recessedPage=null;appLayerFraction=0;
        if(appLayerCounted){openSheetCount--;appLayerCounted=false;}
    }
    private void PaintAppLayer(float fraction)
    {
        appLayerFraction=fraction;
        if(expandedApp is {} card)
        {
            card.TranslationX=appLayerStartX*(1-fraction);card.TranslationY=appLayerStartY*(1-fraction);
            card.ScaleX=appLayerScaleX+(1-appLayerScaleX)*fraction;card.ScaleY=appLayerScaleY+(1-appLayerScaleY)*fraction;
            card.Alpha=.5f+.5f*fraction;
        }
        if(recessedPage is {} pageView){pageView.ScaleX=pageView.ScaleY=1-.025f*fraction;pageView.Alpha=1-.42f*fraction;}
    }
    private void AnimateAppLayer(bool opening,Action? after=null)
    {
        var old=appLayerAnimation;appLayerAnimation=null;old?.Cancel();
        if(!LayerMotionEnabled)
        {if(opening)PaintAppLayer(1);else {bool refresh=pendingLayerRefresh;StopAppLayer();if(refresh)RefreshBodyInPlace();SyncDepthMotion();after?.Invoke();}return;}
        var animation=ValueAnimator.OfFloat(appLayerFraction,opening?1:0)!;appLayerAnimation=animation;
        animation.SetDuration(opening?240:180);animation.SetInterpolator(new DecelerateInterpolator(1.5f));
        animation.Update+=(_,e)=>{if(ReferenceEquals(appLayerAnimation,animation))PaintAppLayer(((Java.Lang.Float)e.Animation!.AnimatedValue!).FloatValue());};
        animation.AnimationEnd+=(_,_)=>
        {
            if(!ReferenceEquals(appLayerAnimation,animation))return;appLayerAnimation=null;
            if(!opening){bool refresh=pendingLayerRefresh;StopAppLayer();if(refresh)RefreshBodyInPlace();SyncDepthMotion();after?.Invoke();}
        };
        animation.Start();
    }
    private void OpenAppLayer(AppEntry app,View source)
    {
        depthMotion.Stop();ResetDeviceDepth();StopHomePageAnimation();StopAppLayer();HideKeyboard();
        int[] location=new int[2],rootLocation=new int[2];source.GetLocationInWindow(location);root.GetLocationInWindow(rootLocation);
        int originX=location[0]-rootLocation[0]-root.PaddingLeft,originY=location[1]-rootLocation[1]-root.PaddingTop;
        int available=root.Width-root.PaddingLeft-root.PaddingRight,width=Math.Min(Dp(300),available-Dp(32));
        int maxHeight=Math.Min(Dp(360),scroll.Height-Dp(24));if(width<=0||maxHeight<Dp(170)){EditAppUnlocked(app);return;}
        var layer=new FrameLayout(this){Clickable=true};layer.Click+=(_,_)=>AnimateAppLayer(false);
        var card=Stack();card.SetPadding(Dp(14),Dp(12),Dp(14),Dp(8));card.Background=Background(palette.Card,Math.Clamp(state.CornerRadius,8,16),palette.Divider);
        card.Elevation=Dp(2);card.PivotX=0;card.PivotY=0;card.Clickable=true;card.FocusableInTouchMode=true;
        var viewport=new ScrollView(this){VerticalScrollBarEnabled=false,OverScrollMode=OverScrollMode.Never};
        var content=Stack();viewport.AddView(content,new ScrollView.LayoutParams(-1,-2));card.AddView(viewport,Match());
        var identity=Stack(true);identity.SetGravity(GravityFlags.CenterVertical);
        identity.AddView(AppIcon(app,palette.Text),new LinearLayout.LayoutParams(Dp(40),Dp(40)));
        var labels=Stack();labels.SetPadding(Dp(12),0,0,0);var title=Text(AppOrganization.DisplayName(state,app),16,true);title.SetMaxLines(2);title.Ellipsize=Android.Text.TextUtils.TruncateAt.End;labels.AddView(title,Match());
        var drawer=Text(state.Drawers.FirstOrDefault(d=>d.Id==state.Apps[app.Id].DrawerId)?.Name??"App",11,color:palette.Muted);drawer.SetPadding(0,Dp(4),0,0);labels.AddView(drawer,Match());identity.AddView(labels,new LinearLayout.LayoutParams(0,-2,1));
        identity.AddView(IconButton("close","Close app actions",()=>AnimateAppLayer(false)),new LinearLayout.LayoutParams(Dp(48),Dp(48)));content.AddView(identity,Match());
        if(AppCatalog.HasUsageAccess(this))
        {
            var today=Text("Today · "+AppOrganization.FormatDuration(usage.GetValueOrDefault(app.PackageName)),12,true,palette.Accent);today.SetPadding(0,Dp(12),0,Dp(6));content.AddView(today,Match());
            int goal=state.Apps[app.Id].DailyGoalMinutes;if(goal>0)Paragraph("Daily goal · "+goal+" min",content);
        }
        void Go(Action action)=>AnimateAppLayer(false,action);
        if(OperatingSystem.IsAndroidVersionAtLeast(25))AddLayerShortcuts(content,app,Go);
        var actions=Stack(true);actions.SetPadding(0,Dp(8),0,0);actions.AddView(RootAction("Open",()=>Go(()=>
        {
            // This surface was authenticated on entry and is discarded on backgrounding.
            int goal=state.Apps[app.Id].DailyGoalMinutes;
            if(goal>0&&usage.GetValueOrDefault(app.PackageName)>=goal*60_000L)
                Confirm("Your daily goal",$"You have used {app.Label} for {AppOrganization.FormatDuration(usage.GetValueOrDefault(app.PackageName))} today.","Open app",()=>LaunchDirect(app));
            else StartAppDirect(app);
        })),new LinearLayout.LayoutParams(0,-2,1));
        actions.AddView(RootAction("Edit",()=>Go(()=>EditAppUnlocked(app))),new LinearLayout.LayoutParams(0,-2,1));actions.AddView(RootAction("Details",()=>Go(()=>ShowAppDashboard(app))),new LinearLayout.LayoutParams(0,-2,1));content.AddView(actions,Match());
        card.Measure(View.MeasureSpec.MakeMeasureSpec(width,MeasureSpecMode.Exactly),View.MeasureSpec.MakeMeasureSpec(maxHeight,MeasureSpecMode.AtMost));
        int height=Math.Min(maxHeight,card.MeasuredHeight);
        int left=Math.Clamp(originX+source.Width/2<available/2?originX:originX+source.Width-width,Dp(12),available-width-Dp(12));
        int top=Math.Clamp(originY,scroll.Top+Dp(12),scroll.Top+scroll.Height-height-Dp(12));
        layer.AddView(card,new FrameLayout.LayoutParams(width,height){LeftMargin=left,TopMargin=top});
        selectedAppTile=source;source.Alpha=0;recessedPage=scroll;scroll.ImportantForAccessibility=ImportantForAccessibility.NoHideDescendants;
        appLayer=layer;expandedApp=card;appLayerCounted=true;openSheetCount++;
        appLayerStartX=originX-left;appLayerStartY=originY-top;appLayerScaleX=source.Width/(float)width;appLayerScaleY=source.Height/(float)height;
        PaintAppLayer(0);root.AddView(layer,new FrameLayout.LayoutParams(-1,-1));card.RequestFocus();AnimateAppLayer(true);
    }
    [System.Runtime.Versioning.SupportedOSPlatform("android25.0")]
    private void AddLayerShortcuts(LinearLayout content,AppEntry app,Action<Action> go)
    {
        if(!OperatingSystem.IsAndroidVersionAtLeast(25))return;
        try
        {
            var launcher=GetSystemService(LauncherAppsService) as LauncherApps;if(launcher?.HasShortcutHostPermission!=true)return;
            using var query=new LauncherApps.ShortcutQuery();query.SetPackage(app.PackageName);query.SetQueryFlags((LauncherAppsShortcutQueryFlags)11); // dynamic (1), pinned (2), manifest (8)
            var user=ProfileCatalog.User(this,app.ProfileSerial);if(user is null)return;
            var shortcuts=launcher.GetShortcuts(query,user)?.Where(s=>s.IsEnabled).OrderBy(s=>s.Rank).DistinctBy(s=>s.Id).Take(3).ToArray()??[];
            if(shortcuts.Length==0)return;Gap(content,6);
            foreach(var shortcut in shortcuts)
            {
                var item=shortcut;var label=item.ShortLabel?.ToString();if(string.IsNullOrWhiteSpace(label))continue;
                var action=Text(label,14);action.SetPadding(Dp(10),Dp(8),Dp(10),Dp(8));action.SetMinimumHeight(Dp(48));action.SetMaxLines(2);TouchFeedback(action,10);
                action.Click+=(_,_)=>go(()=>
                {
                    try{launcher.StartShortcut(item,null,null);}
                    catch(Java.Lang.RuntimeException){Toast("This shortcut is no longer available.");}
                });content.AddView(action,Match());
            }
        }
        catch(Java.Lang.RuntimeException){}
    }
}
