using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.UI;
using Uri = Android.Net.Uri;

namespace LauncherApp;

public partial class MainActivity
{
    private readonly Dictionary<string,Android.Animation.ValueAnimator> drawerAnimations=new();
    private void StopDrawerAnimations()
    {
        foreach(var animation in drawerAnimations.Values.ToArray())animation.Cancel();
        drawerAnimations.Clear();
    }
    private void FoldDrawer(Drawer drawer,LinearLayout content,View heading,Action populate)
    {
        int from=content.Visibility==ViewStates.Gone?0:content.Height;
        if(drawerAnimations.Remove(drawer.Id,out var previous))previous.Cancel();
        drawer.Expanded=!drawer.Expanded;Save();
        bool expanding=drawer.Expanded;
        heading.ContentDescription=drawer.Name+(expanding?", expanded":", collapsed");
        if(content.ChildCount==0)populate();
        content.Visibility=ViewStates.Visible;
        int width=body.Width>0?body.Width:Resources!.DisplayMetrics!.WidthPixels-Dp(32);
        content.Measure(View.MeasureSpec.MakeMeasureSpec(width,MeasureSpecMode.Exactly),View.MeasureSpec.MakeMeasureSpec(0,MeasureSpecMode.Unspecified));
        int natural=content.MeasuredHeight,target=expanding?natural:0;
        var layout=content.LayoutParameters!;layout.Height=from;content.LayoutParameters=layout;
        content.PivotY=0;content.SetCameraDistance(Dp(1200));
        var animation=Android.Animation.ValueAnimator.OfInt(from,target)!;
        animation.SetDuration(230);animation.SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.5f));
        bool cancelled=false;animation.AnimationCancel+=(_,_)=>cancelled=true;
        animation.Update+=(_,e)=>
        {
            int height=((Java.Lang.Integer)e.Animation!.AnimatedValue!).IntValue();
            var lp=content.LayoutParameters!;lp.Height=height;content.LayoutParameters=lp;
            float fraction=natural>0?Math.Clamp(height/(float)natural,0,1):1;
            content.Alpha=.35f+.65f*fraction;content.RotationX=-7*(1-fraction);
        };
        animation.AnimationEnd+=(_,_)=>
        {
            if(cancelled)return;
            content.Visibility=expanding?ViewStates.Visible:ViewStates.Gone;
            content.Alpha=1;content.RotationX=0;
            var lp=content.LayoutParameters!;lp.Height=-2;content.LayoutParameters=lp;
            drawerAnimations.Remove(drawer.Id);
        };
        drawerAnimations[drawer.Id]=animation;animation.Start();
    }
    private void RenderTiles()
    {
        AddWeatherStrip();
        AddMediaCard();
        body.SetGravity(state.Alignment switch { "Bottom" => GravityFlags.Bottom, "Center" => GravityFlags.CenterVertical, _ => GravityFlags.Top });
        if (state.Focus)
        {
            var focus = Button("Ⅱ  Focus is on", () => { state.Focus=false; state.FocusEndsAt=0; Save(); Render(); });
            body.AddView(focus,Match()); Gap(body,12);
        }
        if (apps.Count == 0)
        {
            Title(loading ? "Finding your apps…" : "Your home, simplified.");
            Paragraph("Installed apps will appear here in drawers. Tap the three dots below to customize Launcher.");
            return;
        }
        var visible=AppOrganization.Visible(state,apps).ToArray();
        bool hasUsage=AppCatalog.HasUsageAccess(this);
        foreach (var drawer in state.Drawers)
        {
            var contents=visible.Where(a=>state.Apps[a.Id].DrawerId==drawer.Id).ToArray();
            if(contents.Length==0) continue;
            var heading = new FrameLayout(this) { ContentDescription = drawer.Name + (drawer.Expanded ? ", expanded" : ", collapsed") };
            var name=Text(drawer.Name, state.CenterHeadings ? 23 : 27,true);
            name.Gravity=state.CenterHeadings ? GravityFlags.Center : GravityFlags.CenterVertical;
            name.SetPadding(state.CenterHeadings ? Dp(40):Dp(32),0,state.CenterHeadings?Dp(40):Dp(54),0);
            name.SetSingleLine(true);name.Ellipsize=Android.Text.TextUtils.TruncateAt.End;
            heading.AddView(name,new FrameLayout.LayoutParams(-1,-1));
            var drawerIcon=DrawerIcons.All.Contains(drawer.Icon)?drawer.Icon:DrawerIcons.ForDrawer(drawer.Id);
            heading.AddView(new GlyphView(this,drawerIcon,palette.Text),new FrameLayout.LayoutParams(Dp(19),Dp(19),GravityFlags.Left|GravityFlags.CenterVertical));
            var detail=Text(hasUsage && state.ShowUsage ? AppOrganization.FormatDuration(contents.Select(a=>a.PackageName).Distinct().Sum(p=>usage.GetValueOrDefault(p))) : "",9,color:palette.Muted);
            detail.Gravity=GravityFlags.Right|GravityFlags.CenterVertical;
            heading.AddView(detail,new FrameLayout.LayoutParams(Dp(54),-1,GravityFlags.Right));
            var drawerContent=Stack();drawerContent.SetClipChildren(true);
            void Populate(){AddTileGrid(drawerContent,contents);Gap(drawerContent,8);}
            heading.Click+=(_,_)=>FoldDrawer(drawer,drawerContent,heading,Populate);
            heading.LongClick+=(_,_)=>EditDrawer(drawer);
            body.AddView(heading,Match(Dp(state.CenterHeadings?58:54)));
            body.AddView(drawerContent,Match());
            if(drawer.Expanded)Populate();else drawerContent.Visibility=ViewStates.Gone;
        }
        if(visible.Length==0) Paragraph("No apps are visible in focus. Pin an app in settings or turn focus off below.");
        var pinned=visible.Where(a=>state.Apps[a.Id].Pinned).Take(5).ToArray();
        if(pinned.Length>0)
        {
            Gap(body,18);
            var dock=Stack(true); dock.SetPadding(Dp(8),Dp(8),Dp(8),Dp(8)); dock.Background=Background(palette.Card,20);
            foreach(var app in pinned)
            {
                var icon=new FrameLayout(this) { ContentDescription=app.Label+", pinned" };
                icon.AddView(AppIcon(app,palette.Text),new FrameLayout.LayoutParams(Dp(30),Dp(30),GravityFlags.Center));
                AddBadge(icon,app.PackageName);
                icon.Click+=(_,_)=>Launch(app); icon.LongClick+=(_,_)=>EditApp(app);
                dock.AddView(icon,new LinearLayout.LayoutParams(0,Dp(52),1));
            }
            body.AddView(dock,Match());
        }
    }

    private void AddTileGrid(LinearLayout parent,IEnumerable<AppEntry> entries)
    {
        LinearLayout? row=null; int used=0;
        void Finish()
        {
            if(row is null) return;
            if(used<state.Columns) row.AddView(new View(this),new LinearLayout.LayoutParams(0,1,state.Columns-used));
        }
        foreach(var app in entries)
        {
            int span=state.Apps[app.Id].Wide?2:1;
            if(row is null || used+span>state.Columns)
            {
                Finish(); row=Stack(true); row.WeightSum=state.Columns; parent.AddView(row,Match()); used=0;
            }
            var cell=new FrameLayout(this);
            var inner=new FrameLayout.LayoutParams(-1,-1);inner.SetMargins(Dp(4),Dp(4),Dp(4),Dp(4));
            cell.AddView(AppTile(app),inner);
            row.AddView(cell,new LinearLayout.LayoutParams(0,Dp(state.TileHeight+8),span));used+=span;
        }
        Finish();
    }

    private View AppIcon(AppEntry app,Color color)
    {
        var icon=new ImageView(this); icon.SetScaleType(ImageView.ScaleType.FitCenter);
        try
        {
            if(!iconCache.TryGetValue(app.Id,out var cached))
            {
                cached=PackageManager!.GetActivityIcon(new ComponentName(app.PackageName,app.ActivityName));
                iconCache[app.Id]=cached;
            }
            var actual=cached.GetConstantState()?.NewDrawable(Resources)?.Mutate()??cached.Mutate();
            if(state.IconStyle=="Monochrome" && OperatingSystem.IsAndroidVersionAtLeast(33)
                && actual is AdaptiveIconDrawable adaptive && adaptive.Monochrome is { } monochrome)
            {
                var drawable=monochrome.Mutate()!;drawable.SetTint(color);icon.SetImageDrawable(drawable);
                return icon;
            }
            icon.SetImageDrawable(actual);
            if(state.IconStyle!="Original")
            {
                using var matrix=new ColorMatrix(); matrix.SetSaturation(0);
                icon.SetColorFilter(new ColorMatrixColorFilter(matrix));
            }
        }
        catch(Android.Content.PM.PackageManager.NameNotFoundException)
        {
            var initials=string.Concat(app.Label.Split(' ',StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w=>w[0])).ToUpperInvariant();
            var fallback=Text(initials,16,true,color);fallback.Gravity=GravityFlags.Center;return fallback;
        }
        return icon;
    }

    private View AppTile(AppEntry app)
    {
        var rule=state.Apps[app.Id];
        var foreground=rule.Highlight?palette.OnAccent:palette.Text;
        bool locked=PackageLocked(app.PackageName);
        bool outlined=state.Focus||state.Theme=="Sun";
        var tile=Stack();
        var surface=rule.Highlight?palette.Accent:outlined?palette.Base:palette.Card;
        var border=outlined&&!rule.Highlight?palette.Muted:(Color?)null;
        tile.Background = locked
    ? new FingerprintDrawable(
        surface,
        rule.Highlight ? palette.OnAccent : palette.Accent,
        Dp(state.CornerRadius),
        border)
    : Background(surface, state.CornerRadius, border);

        tile.SetPadding(
            locked ? 0 : Dp(0),
            locked ? 0 : Dp(0),
            locked ? 0 : Dp(0),
            locked ? 0 : Dp(0));

        tile.SetGravity(GravityFlags.Center);

        tile.ContentDescription =
            AppOrganization.DisplayName(state, app)
            + (locked ? ", locked" : "")
            + ", hold to customize";
        if (rule.Wide)
        {
            var top=Stack(true); top.SetGravity(GravityFlags.CenterVertical);
            top.AddView(AppIcon(app,foreground),new LinearLayout.LayoutParams(Dp(44),Dp(44)));
            var labels=Stack(); labels.SetPadding(Dp(10),0,0,0);
            var label=Text(AppOrganization.DisplayName(state,app),12,true,foreground); label.SetMaxLines(2); labels.AddView(label,Match());
            if(state.ShowUsage && AppCatalog.HasUsageAccess(this)) labels.AddView(Text(AppOrganization.FormatDuration(usage.GetValueOrDefault(app.PackageName)),9,color:foreground),Match());
            top.AddView(labels,new LinearLayout.LayoutParams(0,-2,1)); tile.AddView(top,new LinearLayout.LayoutParams(-1,0,1));
            var actions=Stack(true);
            foreach(var (name,action) in new (string,Action)[]{("Open",()=>Launch(app)),("Info",()=>AppInfo(app))})
            {
                var chip=Text(name,10,color:foreground); chip.Gravity=GravityFlags.Center;
                chip.Background=Background(Color.Transparent,24,foreground); chip.Click+=(_,_)=>action();
                var cp=new LinearLayout.LayoutParams(0,Dp(30),1); cp.SetMargins(Dp(2),Dp(4),Dp(2),0); actions.AddView(chip,cp);
            }
            tile.AddView(actions,Match());
        }
        else
        {
            var iconBox=new FrameLayout(this);
            int iconSize=state.Columns>=6?38:state.Columns==5?44:52;
            iconBox.AddView(AppIcon(app,foreground),new FrameLayout.LayoutParams(Dp(iconSize),Dp(iconSize),GravityFlags.Center));
            tile.AddView(iconBox,new LinearLayout.LayoutParams(-1,0,1));
            if(state.ShowLabels)
            {
                var label=Text(AppOrganization.DisplayName(state,app),state.Columns>=5?10:11,color:foreground);
                label.Gravity=GravityFlags.Center; label.SetMaxLines(2); label.Ellipsize=Android.Text.TextUtils.TruncateAt.End;
                tile.AddView(label,Match());
            }
            if(state.ShowUsage && AppCatalog.HasUsageAccess(this))
            {
                var time=Text(AppOrganization.FormatDuration(usage.GetValueOrDefault(app.PackageName)),9,color:rule.Highlight?foreground:palette.Muted);
                time.Gravity=GravityFlags.Center; time.SetPadding(0,Dp(3),0,0); tile.AddView(time,Match());
            }
        }
        tile.Click+=(_,_)=>Launch(app); tile.LongClick+=(_,_)=>EditApp(app);
        return WithBadge(tile,app);
    }

    private View NotificationIcon(string package)
    {
        var app=apps.FirstOrDefault(a=>a.PackageName==package);
        if(app is not null)return AppIcon(app,palette.Text);
        try
        {
            var raw=PackageManager!.GetApplicationIcon(package);
            var icon=new ImageView(this);icon.SetScaleType(ImageView.ScaleType.FitCenter);
            if(state.IconStyle=="Monochrome" && OperatingSystem.IsAndroidVersionAtLeast(33) && raw is AdaptiveIconDrawable adaptive && adaptive.Monochrome is {} mono)
            {var drawable=mono.Mutate()!;drawable.SetTint(palette.Text);icon.SetImageDrawable(drawable);}
            else
            {
                icon.SetImageDrawable(raw);
                if(state.IconStyle!="Original"){using var matrix=new ColorMatrix();matrix.SetSaturation(0);icon.SetColorFilter(new ColorMatrixColorFilter(matrix));}
            }
            return icon;
        }
        catch(Android.Content.PM.PackageManager.NameNotFoundException)
        {var initial=Text(package.Split('.').Last()[..1].ToUpperInvariant(),12,true,palette.Muted);initial.Gravity=GravityFlags.Center;return initial;}
    }

    private View WithBadge(View content,AppEntry app)
    {
        var frame=new FrameLayout(this);frame.AddView(content,new FrameLayout.LayoutParams(-1,-1));
        AddBadge(frame,app.PackageName);
        return frame;
    }
    private void AddBadge(FrameLayout frame,string package)
    {
        var badge=Text("",9,true,palette.OnAccent);badge.Gravity=GravityFlags.Center;
        badge.SetPadding(Dp(5),0,Dp(5),0);badge.SetMinimumWidth(Dp(19));badge.Background=Background(palette.Accent,20);
        var lp=new FrameLayout.LayoutParams(-2,Dp(19),GravityFlags.Top|GravityFlags.Right);lp.SetMargins(0,Dp(6),Dp(6),0);
        frame.AddView(badge,lp);badgeViews.Add((badge,package));UpdateBadge(badge,package);
    }
    private void UpdateBadge(TextView badge,string package)
    {
        int count=badgeCounts.GetValueOrDefault(package);
        badge.Visibility=state.ShowNotificationBadges&&count>0?ViewStates.Visible:ViewStates.Gone;
        badge.Text=NotificationBadges.Label(count);badge.ContentDescription=$"{count} unread notifications";
    }

    private void RenderSearchResults()
    {
        if(page=="Conversations") { RenderConversations(); return; }
        Title(query.Length==0?"All apps":"Results");
        var results=AppOrganization.Visible(state,apps,query).OrderBy(a=>AppOrganization.DisplayName(state,a),StringComparer.CurrentCultureIgnoreCase).ToArray();
        if(results.Length==0) { Paragraph("No apps found. Hidden apps can be restored in Settings → Hidden apps."); return; }
        if(query.Length==0) Paragraph($"{results.Length} apps · hold any app to customize");
        foreach(var app in results.Take(searchLimit))
        {
            var row=Stack(true); row.SetGravity(GravityFlags.CenterVertical); row.SetPadding(Dp(10),Dp(10),Dp(10),Dp(10));
            var iconBox=new FrameLayout(this);iconBox.AddView(AppIcon(app,palette.Text),new FrameLayout.LayoutParams(Dp(28),Dp(28),GravityFlags.Center));AddBadge(iconBox,app.PackageName);
            row.AddView(iconBox,new LinearLayout.LayoutParams(Dp(44),Dp(44)));
            var label=Text(AppOrganization.DisplayName(state,app),16); label.SetPadding(Dp(18),0,0,0);
            row.AddView(label,new LinearLayout.LayoutParams(0,-2,1));
            if(state.Apps[app.Id].Pinned) row.AddView(new GlyphView(this,"pin",palette.Muted),new LinearLayout.LayoutParams(Dp(18),Dp(18)));
            row.SetMinimumHeight(Dp(62)); row.Click+=(_,_)=>Launch(app); row.LongClick+=(_,_)=>EditApp(app); body.AddView(row,Match());
        }
        if(results.Length>searchLimit)body.AddView(Button($"Show more apps · {results.Length-searchLimit} remaining",()=>
        {int y=scroll.ScrollY;searchLimit+=24;RenderBody();scroll.Post(()=>scroll.ScrollTo(0,y));}),Match());
    }

    private void AppInfo(AppEntry app) => OpenIntent(new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings,Uri.Parse("package:"+app.PackageName)));

    private Dialog Sheet(string title,Action<LinearLayout,Dialog> build,Action<LinearLayout,Dialog>? footer=null)
    {
        var dialog=new Dialog(this,Android.Resource.Style.ThemeMaterialNoActionBar);
        var overlay=new FrameLayout(this);overlay.SetOnApplyWindowInsetsListener(new InsetListener());
        var outside=new View(this);outside.Click+=(_,_)=>dialog.Dismiss();overlay.AddView(outside,new FrameLayout.LayoutParams(-1,-1));
        var container=new MaxHeightStack(this,(int)(Resources!.DisplayMetrics!.HeightPixels*.87));
        var shape=Background(palette.Card,0);shape.SetCornerRadii([Dp(28),Dp(28),Dp(28),Dp(28),0,0,0,0]);container.Background=shape;
        container.SetPadding(Dp(22),0,Dp(22),Dp(20));
        var handle=new FrameLayout(this){ContentDescription="Drag down to close sheet"};
        var line=new View(this){Background=Background(palette.Muted,4)};
        handle.AddView(line,new FrameLayout.LayoutParams(Dp(38),Dp(4),GravityFlags.Center));
        handle.SetOnTouchListener(new SheetDragListener(container,dialog,Dp(100)));
        container.AddView(handle,Match(Dp(32)));
        var heading=Text(title,27,true);heading.SetPadding(Dp(2),Dp(4),Dp(2),Dp(18));
        heading.SetOnTouchListener(new SheetDragListener(container,dialog,Dp(100)));container.AddView(heading,Match());
        var content=Stack();build(content,dialog);
        var scroller=new ScrollView(this){VerticalScrollBarEnabled=false,FillViewport=false};scroller.AddView(content);
        container.AddView(scroller,new LinearLayout.LayoutParams(-1,0,1));
        if(footer is not null){var actions=Stack();actions.SetPadding(0,Dp(16),0,0);footer(actions,dialog);container.AddView(actions,Match());}
        overlay.AddView(container,new FrameLayout.LayoutParams(-1,-2,GravityFlags.Bottom));
        dialog.SetContentView(overlay);dialog.Window!.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
        dialog.Window.SetSoftInputMode(SoftInput.AdjustResize);
        dialog.Show();dialog.Window.DecorView.SetPadding(0,0,0,0);dialog.Window.SetLayout(-1,-1);
        dialog.Window.SetGravity(GravityFlags.Fill);dialog.Window.AddFlags(WindowManagerFlags.DimBehind);
        dialog.Window.SetDimAmount(.5f);
        if(OperatingSystem.IsAndroidVersionAtLeast(30) && state.MinimalStatusBar && IsHomePage)
            dialog.Window.InsetsController?.Hide(WindowInsets.Type.StatusBars());
        container.TranslationY=Dp(40);container.Alpha=0;
        container.Animate()!.TranslationY(0).Alpha(1).SetDuration(180).Start();
        overlay.RequestApplyInsets();
        return dialog;
    }

    private sealed class MaxHeightStack(Android.Content.Context context,int maxHeight) : LinearLayout(context)
    {
        protected override void OnMeasure(int widthMeasureSpec,int heightMeasureSpec)
        {
            Orientation=Orientation.Vertical;
            int available=MeasureSpec.GetSize(heightMeasureSpec);
            base.OnMeasure(widthMeasureSpec,MeasureSpec.MakeMeasureSpec(Math.Min(maxHeight,available),MeasureSpecMode.AtMost));
        }
    }

    private void EditApp(AppEntry app)=>WithAppUnlock(app.PackageName,()=>EditAppUnlocked(app));
    private void EditAppUnlocked(AppEntry app)
    {
        var rule=state.Apps[app.Id];
        Sheet(AppOrganization.DisplayName(state,app),(panel,dialog)=>
        {
            Paragraph(app.PackageName,panel);
            if(AppCatalog.HasUsageAccess(this))
            {
                var used=usage.GetValueOrDefault(app.PackageName);
                var card=Stack(); card.SetPadding(Dp(16),Dp(14),Dp(16),Dp(14)); card.Background=Background(palette.Panel);
                card.AddView(Text("TODAY",10,true,palette.Muted),Match()); card.AddView(Text(AppOrganization.FormatDuration(used),32,true),Match());
                Paragraph(rule.DailyGoalMinutes>0?$"Daily goal · {rule.DailyGoalMinutes} minutes":"Set a daily goal below for a mindful launch reminder.",card); panel.AddView(card,Match()); Gap(panel);
            }
            void Change(Action action) { action(); Save(); dialog.Dismiss(); Render(); }
            SettingRow(panel,"Biometric lock",PackageLocked(app.PackageName)?"On · authenticate to open":"Off",()=>
            {dialog.Dismiss();SetAppLock(app,!PackageLocked(app.PackageName));},"lock");
            SettingRow(panel,"Open app in grayscale",rule.OpenInGrayscale?"On · app display, not icon colour":"Off · keep app colours",()=>
            {
                Choose("App display colour",["Colour","Grayscale","Grayscale setup"],i=>
                {
                    if(i==2){dialog.Dismiss();Navigate("Grayscale setup");return;}
                    Change(()=>{foreach(var p in state.Apps.Where(p=>p.Key.StartsWith(app.PackageName+"/",StringComparison.Ordinal)))p.Value.OpenInGrayscale=i==1;});
                    if(i==1&&!Services.GrayscaleService.Ready(this))Navigate("Grayscale setup");
                });
            },"photo");
            Paragraph("App lock protects launches and app editing through Launcher. Android notifications, Recents and other launchers remain controlled by Android.",panel);
            SettingRow(panel,"Drawer",state.Drawers.First(d=>d.Id==rule.DrawerId).Name,()=>
                Choose("Move to drawer",state.Drawers.Select(d=>d.Name).ToArray(),i=>Change(()=>rule.DrawerId=state.Drawers[i].Id)));
            SettingRow(panel,"Rename",rule.Label??app.Label,()=>Prompt("App name",rule.Label??app.Label,s=>Change(()=>rule.Label=LauncherState.CleanName(s,app.Label))));
            ToggleRow(panel,"Highlight tile",rule.Highlight,v=>Change(()=>rule.Highlight=v));
            ToggleRow(panel,"Expanded tile",rule.Wide,v=>Change(()=>rule.Wide=v));
            ToggleRow(panel,"Pin to favorites",rule.Pinned,v=>Change(()=>rule.Pinned=v));
            SettingRow(panel,"Daily usage goal",rule.DailyGoalMinutes==0?"Off":rule.DailyGoalMinutes+" min",()=>
                Choose("Daily goal",["Off","15 minutes","30 minutes","45 minutes","60 minutes","90 minutes","120 minutes"],i=>Change(()=>rule.DailyGoalMinutes=new[]{0,15,30,45,60,90,120}[i])));
            var move=Stack(true);
            move.AddView(Button("Move earlier",()=>Change(()=>MoveApp(app,-1))),new LinearLayout.LayoutParams(0,-2,1));
            move.AddView(Button("Move later",()=>Change(()=>MoveApp(app,1))),new LinearLayout.LayoutParams(0,-2,1)); panel.AddView(move,Match()); Gap(panel);
            panel.AddView(Button(rule.Hidden?"Show app":"Hide from Launcher",()=>Change(()=>rule.Hidden=!rule.Hidden)),Match()); Gap(panel);
            panel.AddView(Button("Android app info",()=>{dialog.Dismiss();AppInfo(app);}),Match()); Gap(panel);
            panel.AddView(Button("Done",()=>dialog.Dismiss(),true),Match());
        });
    }

    private void MoveApp(AppEntry app,int direction)
    {
        var rule=state.Apps[app.Id];
        var siblings=apps.Where(a=>state.Apps[a.Id].DrawerId==rule.DrawerId).OrderByDescending(a=>state.Apps[a.Id].Pinned).ThenBy(a=>state.Apps[a.Id].Order).ToList();
        int index=siblings.FindIndex(a=>a.Id==app.Id),target=index+direction;
        if(target<0 || target>=siblings.Count) return;
        var other=state.Apps[siblings[target].Id]; other.Pinned=rule.Pinned;
        (other.Order,rule.Order)=(rule.Order,other.Order);
    }
}
