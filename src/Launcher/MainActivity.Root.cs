using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Media.Session;
using Android.Views;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.UI;
using Uri = Android.Net.Uri;

namespace LauncherApp;

public partial class MainActivity
{
    private string conversationFilter="All";

    private void RootCard(string id,string title,string glyph,Action<LinearLayout> build,string? summary=null)
    {
        bool collapsed=state.CollapsedCards.GetValueOrDefault(id);
        var card=Stack();card.Tag="root-card:"+id;card.SetPadding(Dp(16),Dp(8),Dp(16),Dp(8));card.Background=Surface(palette.Card,22,id=="overview");if(LayerMotionEnabled)card.Elevation=Dp(2);
        var header=Stack(true);header.SetGravity(GravityFlags.CenterVertical);header.ContentDescription=title+(collapsed?", collapsed":", expanded");
        header.AddView(new GlyphView(this,glyph,palette.Muted),new LinearLayout.LayoutParams(Dp(16),Dp(16)));
        bool note=id.StartsWith("note:");var name=Text(note?title:title.ToUpperInvariant(),note?15:11,true,note?palette.Text:palette.Muted);name.LetterSpacing=note?0:.10f;name.SetPadding(Dp(10),0,0,0);name.SetSingleLine(true);name.Ellipsize=Android.Text.TextUtils.TruncateAt.End;header.AddView(name,new LinearLayout.LayoutParams(0,-2,1));
        if(id=="feeds")header.AddView(FeedRefreshControl(),new LinearLayout.LayoutParams(Dp(44),Dp(44)));
        header.AddView(new GlyphView(this,collapsed?"down":"up",palette.Muted),new LinearLayout.LayoutParams(Dp(14),Dp(14)));
        depthHeadings.Add(header);
        TouchFeedback(header,8);BindHoldHaptics(header);header.LongClick+=(_,_)=>{if(id=="private-space")ShowProfiles();else RootOptions(id,title);};
        card.AddView(header,Match(Dp(Math.Max(48,15*FontScale*1.3f+12))));
        var viewport=new FrameLayout(this);viewport.SetClipChildren(true);card.AddView(viewport,Match());
        LinearLayout Contents(bool folded)
        {
            var inner=Stack();inner.SetPadding(0,Dp(4),0,Dp(4));
            if(!folded)build(inner);
            else
            {
                if(!string.IsNullOrEmpty(summary)){var preview=Text(summary,13,color:id=="weather"?palette.Accent:palette.Muted);preview.SetMaxLines(2);inner.AddView(preview,Match());}
                if(id=="media"&&media.Controller is {} controller)
                {
                    var actions=Stack(true);var transport=controller.GetTransportControls();long supported=controller.PlaybackState?.Actions??0;
                    void Control(string glyph,string label,long required,Action action){var control=IconButton(glyph,label,action);control.Enabled=(supported&required)!=0;control.Alpha=control.Enabled?1:.32f;actions.AddView(control,new LinearLayout.LayoutParams(Dp(48),Dp(48)));}
                    Control("previous","Previous track",PlaybackState.ActionSkipToPrevious,()=>transport?.SkipToPrevious());
                    Control(media.Playing?"pause":"play",media.Playing?"Pause":"Play",media.Playing?PlaybackState.ActionPause|PlaybackState.ActionPlayPause:PlaybackState.ActionPlay|PlaybackState.ActionPlayPause,()=>{if(media.Playing)transport?.Pause();else transport?.Play();});
                    Control("next","Next track",PlaybackState.ActionSkipToNext,()=>transport?.SkipToNext());inner.AddView(actions,Match());
                }
                if(id=="timers")foreach(var timer in state.Timers.Take(3))
                {
                    var row=Stack(true);row.SetGravity(GravityFlags.CenterVertical);var value=Text(TimerDisplay(timer),21,true,palette.Accent);workspaceTimerViews.Add((timer,value));row.AddView(value,new LinearLayout.LayoutParams(0,-2,1));
                    row.AddView(IconButton(timer.EndsAt>0?"pause":"play",timer.EndsAt>0?"Pause timer":"Start timer",()=>ToggleTimer(timer)));inner.AddView(row,Match());
                }
            }
            return inner;
        }
        viewport.AddView(Contents(collapsed),new FrameLayout.LayoutParams(-1,-2));
        header.Click+=(_,_)=>
        {
            bool folded=!state.CollapsedCards.GetValueOrDefault(id);state.CollapsedCards[id]=folded;Save();
            if(!LayerMotionEnabled){RefreshBodyInPlace();return;}
            header.ContentDescription=title+(folded?", collapsed":", expanded");
            header.RemoveViewAt(header.ChildCount-1);header.AddView(new GlyphView(this,folded?"down":"up",palette.Muted),new LinearLayout.LayoutParams(Dp(14),Dp(14)));
            if(folded&&id=="feeds"){feedImageLifetime?.Cancel();feedPanel=null;feedStatus=null;}
            if(folded&&id=="resources")resourcesPanel=null;
            SwapRootPane(id,viewport,Contents(folded));
        };
        var lp=Match();lp.SetMargins(0,0,0,Dp(12));body.AddView(card,lp);
    }

    private void RenderRoot()=>RenderWorkspace();

    private string FocusTime()
    {
        if(state.FocusEndsAt<=0)return state.Focus?"In focus":"25:00";
        var t=TimeSpan.FromMilliseconds(Math.Max(0,state.FocusEndsAt-Now));return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
    }

    private void RenderRootSettings()
    {
        Title("Root cards");
        ToggleRow(body,"Calendar card",state.CalendarVisible,v=>{state.CalendarVisible=v;Save();});
        ToggleRow(body,"Focus card",state.FocusCardVisible,v=>{state.FocusCardVisible=v;Save();});
        ToggleRow(body,"Notes card",state.NotesVisible,v=>{state.NotesVisible=v;Save();});
        ToggleRow(body,"Phone resources card",state.ResourcesVisible,v=>{state.ResourcesVisible=v;if(v)state.RootHidden.Remove("resources");Save();});
        ToggleRow(body,"Calculator card",state.CalculatorVisible,v=>{state.CalculatorVisible=v;Save();});
        Gap(body);body.AddView(Button("+  Add Android widget",AddWidget,true),Match());Gap(body);
        Paragraph("Your installed apps provide Android widgets. Tap a widget header to collapse it; hold the header to resize or remove it. Provider setup may open a separate screen.");
        foreach(var slot in state.Widgets.ToArray())SettingRow(body,WidgetLabel(slot.Id),slot.Height+" dp",()=>WidgetOptions(slot));
    }

    private string WidgetLabel(int id) => AppWidgetManager.GetInstance(this)?.GetAppWidgetInfo(id)?.LoadLabel(PackageManager)??"Android widget";
    private void AddWidget()
    {
        if(state.Widgets.Count>=20){Toast("You can add up to 20 widgets.");return;}
        pendingWidget=widgetHost.AllocateAppWidgetId();
        try
        {
            var intent=new Intent(AppWidgetManager.ActionAppwidgetPick);intent.PutExtra(AppWidgetManager.ExtraAppwidgetId,pendingWidget);
            StartActivityForResult(intent,801);
        }
        catch(ActivityNotFoundException){widgetHost.DeleteAppWidgetId(pendingWidget);pendingWidget=-1;Toast("Widget picker is unavailable on this device.");}
    }
    private void RenderWidget(WidgetSlot slot)
    {
        var info=AppWidgetManager.GetInstance(this)?.GetAppWidgetInfo(slot.Id);
        if(info is null)return;
        var card=Stack();card.Tag="root-widget:"+slot.Id;card.SetPadding(0,0,0,Dp(slot.Collapsed?0:8));
        var header=Stack(true);header.SetGravity(GravityFlags.CenterVertical);header.ContentDescription=WidgetLabel(slot.Id)+(slot.Collapsed?", collapsed":", expanded");
        header.SetPadding(0,0,0,0);
        var providerApp=apps.FirstOrDefault(a=>a.PackageName==info.Provider?.PackageName);
        header.AddView(providerApp is not null?AppIcon(providerApp,palette.Text):new GlyphView(this,"grid",palette.Muted),new LinearLayout.LayoutParams(Dp(19),Dp(19)));
        var label=Text(info.LoadLabel(PackageManager)??"Widget",15,true);label.Gravity=GravityFlags.CenterVertical;
        label.SetPadding(Dp(10),0,Dp(8),0);label.SetSingleLine(true);label.Ellipsize=Android.Text.TextUtils.TruncateAt.End;
        header.AddView(label,new LinearLayout.LayoutParams(0,Math.Max(Dp(48),Dp(15*FontScale*1.3f+16)),1));
        header.AddView(new GlyphView(this,slot.Collapsed?"down":"up",palette.Muted),new LinearLayout.LayoutParams(Dp(18),Dp(18)));
        TouchFeedback(header,8);BindHoldHaptics(header);header.Click+=(_,_)=>{slot.Collapsed=!slot.Collapsed;Save();RenderBody();};header.LongClick+=(_,_)=>WidgetOptions(slot);card.AddView(header,Match());
        if(!slot.Collapsed)
        {
            try
            {
                var view=widgetHost.CreateView(this,slot.Id,info)!;view.SetAppWidget(slot.Id,info);
                view.SetPadding(0,0,0,0);
                int width=(int)(Resources!.DisplayMetrics!.WidthPixels/Resources.DisplayMetrics.Density)-48;
                int height=slot.AutoHeight?Math.Clamp((int)Math.Ceiling(info.MinHeight/Resources.DisplayMetrics.Density),64,440):slot.Height;
                if(OperatingSystem.IsAndroidVersionAtLeast(31))
                    view.UpdateAppWidgetSize(new Bundle(),new System.Collections.Generic.List<Android.Util.SizeF>{new(width,height)});
                else
                {
#pragma warning disable CA1422 // Android 7–11 widget size API.
                    view.UpdateAppWidgetSize(null,width,height,width,height);
#pragma warning restore CA1422
                }
                card.AddView(view,Match(Dp(height)));
            }
            catch(Java.Lang.RuntimeException){Paragraph("This widget is unavailable. Hold its header to remove it.",card);}
        }
        var lp=Match();lp.SetMargins(0,0,0,Dp(14));body.AddView(card,lp);
    }
    private void WidgetOptions(WidgetSlot slot) => Choose(WidgetLabel(slot.Id),["Fit widget automatically","Compact · 72 dp","Small · 140 dp","Medium · 220 dp","Large · 320 dp","Tall · 440 dp","Move up","Move down","Remove widget"],i=>
    {
        if(i==0)slot.AutoHeight=true;
        else if(i<6){slot.AutoHeight=false;slot.Height=new[]{0,72,140,220,320,440}[i];}
        else if(i==8){widgetHost.DeleteAppWidgetId(slot.Id);state.Widgets.Remove(slot);state.RootOrder.Remove("widget:"+slot.Id);state.RootPinned.Remove("widget:"+slot.Id);state.RootHidden.Remove("widget:"+slot.Id);}
        else
        {
            MoveTodayCard("widget:"+slot.Id,i==6?-1:1);
        }
        Save();Render();
    });

    private void RenderConversations()
    {
        var conversationHeader=Stack(true);conversationHeader.SetGravity(GravityFlags.CenterVertical);
        var heading=Text("Notifications",27,true);heading.Gravity=GravityFlags.CenterVertical;
        conversationHeader.AddView(heading,new LinearLayout.LayoutParams(0,Math.Max(Dp(52),Dp(27*FontScale*1.25f+12)),1));
        conversationHeader.AddView(IconButton("filter","Filter notification apps",PickConversationSources),new LinearLayout.LayoutParams(Dp(48),Dp(48)));body.AddView(conversationHeader,Match());
        if(!ConversationService.Enabled(this))
        {
            Paragraph("Your messages, in one quiet place. Connect Android notifications to see current conversations from your messaging apps.");
            body.AddView(Button("Set up conversations",()=>Navigate("Permissions"),true),Match());Gap(body);
            Paragraph("Launcher displays current notifications. Optional local history can save new message notifications after you enable it.");return;
        }
        var keyguard=(KeyguardManager?)GetSystemService(KeyguardService);
        if(keyguard?.IsDeviceLocked==true){Paragraph("Unlock your phone to view message previews.");return;}
        var messages=(state.ConversationMessagesOnly?ConversationService.Snapshot():ConversationService.NotificationSnapshot())
            .Where(m=>!state.ConversationExcludedApps.Contains(m.Package)).ToArray();
        Paragraph(state.ConversationMessagesOnly?"Messages only":"All notification types · choose sources with the filter icon");
        if(conversationFilter!="All"&&!messages.Any(m=>m.Package==conversationFilter))conversationFilter="All";
        var filters=Stack(true);
        var filterScroll=new HorizontalScrollView(this){HorizontalScrollBarEnabled=false};filterScroll.AddView(filters);
        foreach(var package in new[]{"All"}.Concat(messages.Select(m=>m.Package).Distinct()))
        {
            string label=package=="All"?"All":PackageLabel(package);
            var chip=RootAction(label,()=>{conversationFilter=package;RenderBody();});chip.SetTextColor(conversationFilter==package?palette.OnAccent:palette.Text);chip.Background=Background(conversationFilter==package?palette.Accent:Color.Transparent,20,conversationFilter==package?null:palette.Divider);
            var lp=new LinearLayout.LayoutParams(-2,-2);lp.SetMargins(0,0,Dp(8),0);filters.AddView(chip,lp);
        }
        body.AddView(filterScroll,Match());Gap(body,16);
        if(conversationFilter!="All"&&!messages.Any(m=>m.Package==conversationFilter))conversationFilter="All";
        var visible=messages.Where(m=>(conversationFilter=="All"||m.Package==conversationFilter)
            && (ConcealPreview(m.Package)?PackageLabel(m.Package).Contains(query,StringComparison.CurrentCultureIgnoreCase):m.Title.Contains(query,StringComparison.CurrentCultureIgnoreCase)||m.Text.Contains(query,StringComparison.CurrentCultureIgnoreCase))).ToArray();
        if(visible.Length==0)Paragraph(query.Trim().Length>0?"No notifications match your search.":"No notifications from the selected apps. Use the filter icon to choose which apps appear here.");
        string? previousPackage=null;DateTime? previousDate=null;
        foreach(var message in visible)
        {
            var date=DateTimeOffset.FromUnixTimeMilliseconds(message.Time).LocalDateTime.Date;
            bool newDay=previousDate!=date;
            if(newDay)
            {
                string label=date==DateTime.Today?"Today":date==DateTime.Today.AddDays(-1)?"Yesterday":date.ToString("ddd, d MMM");
                SectionCaption(body,label);previousPackage=null;
            }
            bool compact=previousPackage==message.Package&&!newDay;
            var lp=Match();lp.SetMargins(0,Dp(compact?4:10),0,0);body.AddView(NotificationCard(message,actions:true,compact:compact),lp);
            previousPackage=message.Package;previousDate=date;
        }
    }

    protected override void OnActivityResult(int requestCode,Result resultCode,Intent? data)
    {
        base.OnActivityResult(requestCode,resultCode,data);
        if(requestCode==811){CompleteCredentialAuthentication(resultCode);return;}
        if(requestCode==815)
        {
            if(resultCode==Result.Ok&&data?.Data is {} folder)
            {
                try{ContentResolver!.TakePersistableUriPermission(folder,data.Flags&(ActivityFlags.GrantReadUriPermission|ActivityFlags.GrantWriteUriPermission));companion.SetFolder(folder.ToString()!);if(companionSession&&resumed)companion.Start();RenderBody();}
                catch(Java.Lang.SecurityException){Toast("Folder access was not granted.");}
            }
            return;
        }
        if(requestCode==813)
        {
            if(resultCode==Result.Ok&&data?.Data is {} attachment&&state.Tickets.FirstOrDefault(t=>t.Id==attachingTicket) is {} ticket)
            {try{ContentResolver!.TakePersistableUriPermission(attachment,ActivityFlags.GrantReadUriPermission);}catch(Java.Lang.SecurityException){}ticket.Attachment=attachment.ToString()??"";Save();RenderBody();}attachingTicket=null;return;
        }
        if(requestCode is 801 or 802)
        {
            if(resultCode!=Result.Ok)
            {
                if(pendingWidget>0)widgetHost.DeleteAppWidgetId(pendingWidget);pendingWidget=-1;return;
            }
            int id=data?.GetIntExtra(AppWidgetManager.ExtraAppwidgetId,pendingWidget)??pendingWidget;
            if(id<=0 || id!=pendingWidget){Toast("Could not add this widget.");return;}
            var info=AppWidgetManager.GetInstance(this)?.GetAppWidgetInfo(id);
            if(requestCode==801 && info?.Configure is not null)
            {
                try
                {
                    var config=new Intent(AppWidgetManager.ActionAppwidgetConfigure);config.SetComponent(info.Configure);config.PutExtra(AppWidgetManager.ExtraAppwidgetId,id);
                    StartActivityForResult(config,802);return;
                }
                catch(ActivityNotFoundException){widgetHost.DeleteAppWidgetId(id);pendingWidget=-1;Toast("Widget setup is unavailable.");return;}
            }
            if(info is not null)state.Widgets.Add(new(){Id=id,Height=Math.Clamp((int)Math.Ceiling(info.MinHeight/Resources!.DisplayMetrics!.Density),64,440),AutoHeight=true});
            else widgetHost.DeleteAppWidgetId(id);
            pendingWidget=-1;Save();Navigate("Root");
        }
        if(resultCode!=Result.Ok||data?.Data is null)return;
        try
        {
            if(requestCode==701)
            {
                using var stream=ContentResolver!.OpenOutputStream(data.Data)!;using var writer=new StreamWriter(stream);
                writer.Write(state.Serialize());Toast("Layout saved.");
            }
            if(requestCode==702)
            {
                using var stream=ContentResolver!.OpenInputStream(data.Data)!;using var reader=new StreamReader(stream);
                var text=new System.Text.StringBuilder();var buffer=new char[16384];int n;
                while((n=reader.Read(buffer))>0){if(text.Length+n>LauncherState.MaximumJsonCharacters)throw new InvalidDataException("Backup too large.");text.Append(buffer,0,n);}
                var restored=LauncherState.Deserialize(text.ToString());
                ShowRestoreOptions(restored);
            }
        }
        catch(Exception e) when(e is IOException or System.Text.Json.JsonException or InvalidDataException or Java.Lang.RuntimeException or UnauthorizedAccessException)
        {Toast("Could not read or write this backup. Choose a valid Launcher JSON file.");}
    }
}
