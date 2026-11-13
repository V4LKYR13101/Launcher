using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.OS;
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

    private void RootCard(string id,string title,string glyph,Action<LinearLayout> build)
    {
        bool collapsed=state.CollapsedCards.GetValueOrDefault(id);
        var card=Stack(); card.SetPadding(Dp(4),0,Dp(4),Dp(collapsed?4:10));
        var header=Stack(true);header.SetGravity(GravityFlags.CenterVertical);
        header.AddView(new GlyphView(this,glyph,palette.Text),new LinearLayout.LayoutParams(Dp(19),Dp(19)));
        var name=Text(title,16,true);name.SetPadding(Dp(10),0,0,0);header.AddView(name,new LinearLayout.LayoutParams(0,-2,1));
        header.AddView(new GlyphView(this,collapsed?"down":"up",palette.Muted),new LinearLayout.LayoutParams(Dp(17),Dp(17)));
        header.Click+=(_,_)=>{state.CollapsedCards[id]=!collapsed;Save();RenderBody();};card.AddView(header,Match(Dp(46)));
        if(!collapsed)build(card);
        var divider=new View(this){Background=new Android.Graphics.Drawables.ColorDrawable(palette.Panel)};
        var dividerLayout=Match(Dp(1));dividerLayout.SetMargins(0,Dp(12),0,0);card.AddView(divider,dividerLayout);
        var lp=Match();lp.SetMargins(0,0,0,Dp(8));body.AddView(card,lp);
    }

    private void RenderRoot()
    {
        if(state.CalendarVisible)RootCard("calendar","Today","calendar",card=>
        {
            var date=Text(DateTime.Now.ToString("dddd, d MMMM"),21,true);date.SetPadding(Dp(6),Dp(6),0,Dp(14));card.AddView(date,Match());
            var days=Stack(true);var start=DateTime.Today.AddDays(-((int)DateTime.Today.DayOfWeek+6)%7);
            for(int i=0;i<7;i++)
            {
                var day=start.AddDays(i);bool today=day==DateTime.Today;
                var col=Stack();col.SetGravity(GravityFlags.Center);col.Background=Background(today?palette.Accent:Color.Transparent,14);
                var name=Text(day.ToString("ddd")[..1],11,color:today?palette.OnAccent:palette.Muted);name.Gravity=GravityFlags.Center;col.AddView(name,Match());
                var number=Text(day.Day.ToString(),19,true,today?palette.OnAccent:palette.Text);number.Gravity=GravityFlags.Center;number.SetPadding(0,Dp(10),0,0);col.AddView(number,Match());
                var dl=new LinearLayout.LayoutParams(0,Dp(72),1);dl.SetMargins(Dp(2),0,Dp(2),0);days.AddView(col,dl);
                col.Click+=(_,_)=>OpenIntent(new Intent(Intent.ActionView,Uri.Parse("content://com.android.calendar/time/"+new DateTimeOffset(day).ToUnixTimeMilliseconds())));
            }
            card.AddView(days,Match());
        });
        if(state.FocusCardVisible)RootCard("focus","Focus","focus",card=>
        {
            timerText=Text(FocusTime(),42,true);timerText.SetPadding(Dp(6),Dp(6),0,Dp(12));card.AddView(timerText,Match());
            Paragraph(state.Focus?"Quiet outlines. One thing at a time.":"Make room for one thing. Choose a focus session.",card);
            var controls=Stack(true);
            foreach(int minutes in new[]{15,25,50})
            {
                var b=Button(minutes+" min",()=>{state.Focus=true;state.FocusEndsAt=Now+minutes*60_000L;Save();Render();});
                var lp=new LinearLayout.LayoutParams(0,-2,1);lp.SetMargins(Dp(3),0,Dp(3),0);controls.AddView(b,lp);
            }
            card.AddView(controls,Match());
            if(state.Focus){Gap(card,8);card.AddView(Button("End focus",()=>{state.Focus=false;state.FocusEndsAt=0;Save();Render();}),Match());}
        });
        if(state.NotesVisible)RootCard("notes","Notes","note",card=>
        {
            var note=new EditText(this){Text=state.Note,Hint="A thought worth keepingâ€¦",TextSize=16,Typeface=regular,Gravity=GravityFlags.Top};
            note.SetTextColor(palette.Text);note.SetHintTextColor(palette.Muted);note.Background=Background(palette.Panel,12);
            note.SetPadding(Dp(14),Dp(12),Dp(14),Dp(12));note.SetMinLines(3);note.SetMaxLines(9);
            note.SetFilters([new Android.Text.InputFilterLengthFilter(20_000)]);
            note.TextChanged+=(_,_)=>{state.Note=note.Text??"";Save();};card.AddView(note,Match());
            var foot=Text("Saved on this device",10,color:palette.Muted);foot.SetPadding(Dp(4),Dp(8),0,0);card.AddView(foot,Match());
        });
        if(state.CalculatorVisible)RootCard("calculator","Calculator","calculator",card=>
        {
            var expression=new EditText(this){Hint="(24 + 6) Ã— 3",TextSize=21,Typeface=regular};
            expression.SetSingleLine(true);expression.SetTextColor(palette.Text);expression.SetHintTextColor(palette.Muted);
            expression.InputType=Android.Text.InputTypes.ClassText|Android.Text.InputTypes.TextFlagNoSuggestions;
            expression.Background=Background(palette.Panel,12);expression.SetPadding(Dp(14),Dp(12),Dp(14),Dp(12));
            var answer=Text("=",30,true);answer.SetPadding(Dp(10),Dp(12),Dp(10),Dp(8));answer.Gravity=GravityFlags.Right;
            expression.TextChanged+=(_,_)=>answer.Text=string.IsNullOrWhiteSpace(expression.Text)?"=":"= "+Calculator.Evaluate(expression.Text!);
            card.AddView(expression,Match());card.AddView(answer,Match());
        });
        foreach(var slot in state.Widgets.ToArray())RenderWidget(slot);
        body.AddView(Button("+  Add Android widget",AddWidget),Match());Gap(body,8);
        var settings=Text("Customize Root",12,color:palette.Muted);settings.Gravity=GravityFlags.Center;settings.SetPadding(0,Dp(12),0,Dp(12));
        settings.Click+=(_,_)=>Navigate("Root settings");body.AddView(settings,Match());
    }

    private string FocusTime()
    {
        if(state.FocusEndsAt<=0)return state.Focus?"In focus":"25:00";
        var t=TimeSpan.FromMilliseconds(Math.Max(0,state.FocusEndsAt-Now));return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
    }

    private void RenderRootSettings()
    {
        Title("Only what you need.");
        ToggleRow(body,"Calendar card",state.CalendarVisible,v=>{state.CalendarVisible=v;Save();});
        ToggleRow(body,"Focus card",state.FocusCardVisible,v=>{state.FocusCardVisible=v;Save();});
        ToggleRow(body,"Notes card",state.NotesVisible,v=>{state.NotesVisible=v;Save();});
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
        var card=Stack();card.SetPadding(0,0,0,Dp(slot.Collapsed?0:8));
        var header=Stack(true);header.SetGravity(GravityFlags.CenterVertical);
        header.SetPadding(Dp(8),0,Dp(8),0);
        var providerApp=apps.FirstOrDefault(a=>a.PackageName==info.Provider?.PackageName);
        header.AddView(providerApp is not null?AppIcon(providerApp,palette.Text):new GlyphView(this,"grid",palette.Muted),new LinearLayout.LayoutParams(Dp(19),Dp(19)));
        var label=Text(info.LoadLabel(PackageManager)??"Widget",15,true);label.Gravity=GravityFlags.CenterVertical;
        label.SetPadding(Dp(10),0,Dp(8),0);label.SetSingleLine(true);label.Ellipsize=Android.Text.TextUtils.TruncateAt.End;
        header.AddView(label,new LinearLayout.LayoutParams(0,Dp(48),1));
        header.AddView(new GlyphView(this,slot.Collapsed?"down":"up",palette.Muted),new LinearLayout.LayoutParams(Dp(18),Dp(18)));
        header.Click+=(_,_)=>{slot.Collapsed=!slot.Collapsed;Save();RenderBody();};header.LongClick+=(_,_)=>WidgetOptions(slot);card.AddView(header,Match());
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
#pragma warning disable CA1422 // Android 7â€“11 widget size API.
                    view.UpdateAppWidgetSize(null,width,height,width,height);
#pragma warning restore CA1422
                }
                card.AddView(view,Match(Dp(height)));
            }
            catch(Java.Lang.RuntimeException){Paragraph("This widget is unavailable. Hold its header to remove it.",card);}
        }
        var lp=Match();lp.SetMargins(0,0,0,Dp(14));body.AddView(card,lp);
    }
    private void WidgetOptions(WidgetSlot slot) => Choose(WidgetLabel(slot.Id),["Fit widget automatically","Compact Â· 72 dp","Small Â· 140 dp","Medium Â· 220 dp","Large Â· 320 dp","Tall Â· 440 dp","Move up","Move down","Remove widget"],i=>
    {
        if(i==0)slot.AutoHeight=true;
        else if(i<6){slot.AutoHeight=false;slot.Height=new[]{0,72,140,220,320,440}[i];}
        else if(i==8){widgetHost.DeleteAppWidgetId(slot.Id);state.Widgets.Remove(slot);}
        else
        {
            int index=state.Widgets.IndexOf(slot),target=index+(i==6?-1:1);
            if(target>=0&&target<state.Widgets.Count)(state.Widgets[index],state.Widgets[target])=(state.Widgets[target],state.Widgets[index]);
        }
        Save();Render();
    });

    private void RenderConversations()
    {
        var conversationHeader=Stack(true);conversationHeader.SetGravity(GravityFlags.CenterVertical);
        var heading=Text("Conversations",27,true);heading.Gravity=GravityFlags.CenterVertical;
        conversationHeader.AddView(heading,new LinearLayout.LayoutParams(0,Dp(64),1));
        conversationHeader.AddView(IconButton("filter","Filter notification apps",PickConversationSources),new LinearLayout.LayoutParams(Dp(48),Dp(48)));body.AddView(conversationHeader,Match());
        if(!ConversationService.Enabled(this))
        {
            Paragraph("Your messages, in one quiet place. Connect Android notifications to see current conversations from your messaging apps.");
            body.AddView(Button("Set up conversations",()=>Navigate("Permissions"),true),Match());Gap(body);
            Paragraph("Only active message notifications appear here. No chat history is imported or stored.");return;
        }
        var keyguard=(KeyguardManager?)GetSystemService(KeyguardService);
        if(keyguard?.IsDeviceLocked==true){Paragraph("Unlock your phone to view message previews.");return;}
        var messages=(state.ConversationMessagesOnly?ConversationService.Snapshot():ConversationService.NotificationSnapshot())
            .Where(m=>!state.ConversationExcludedApps.Contains(m.Package)).ToArray();
        Paragraph(state.ConversationMessagesOnly?"Messages only":"All notification types Â· choose sources with the filter icon");
        var filters=Stack(true);
        var filterScroll=new HorizontalScrollView(this){HorizontalScrollBarEnabled=false};filterScroll.AddView(filters);
        foreach(var package in new[]{"All"}.Concat(messages.Select(m=>m.Package).Distinct()))
        {
            string label=package=="All"?"All":apps.FirstOrDefault(a=>a.PackageName==package)?.Label??package.Split('.').Last();
            var chip=Button(label,()=>{conversationFilter=package;RenderBody();},conversationFilter==package);
            var lp=new LinearLayout.LayoutParams(-2,Dp(44));lp.SetMargins(0,0,Dp(8),0);filters.AddView(chip,lp);
        }
        body.AddView(filterScroll,Match());Gap(body);
        if(!messages.Any(m=>m.Package==conversationFilter))conversationFilter="All";
        var visible=messages.Where(m=>(conversationFilter=="All"||m.Package==conversationFilter)
            && (m.Title.Contains(query,StringComparison.CurrentCultureIgnoreCase)||m.Text.Contains(query,StringComparison.CurrentCultureIgnoreCase))).ToArray();
        if(visible.Length==0)Paragraph("All quiet. New message notifications will appear here. If access was just enabled, wait for the next incoming message.");
        foreach(var message in visible)
        {
            var card=Stack();card.Background=Background(palette.Card,16);card.SetPadding(Dp(14),Dp(14),Dp(14),Dp(14));
            var row=Stack(true);row.SetGravity(GravityFlags.CenterVertical);
            var sourceIcon=NotificationIcon(message.Package);sourceIcon.ContentDescription=apps.FirstOrDefault(a=>a.PackageName==message.Package)?.Label??message.Package;
            row.AddView(sourceIcon,new LinearLayout.LayoutParams(Dp(24),Dp(24)));
            var title=Text(message.Title,15,true);title.SetPadding(Dp(10),0,Dp(8),0);row.AddView(title,new LinearLayout.LayoutParams(0,-2,1));
            row.AddView(Text(DateTimeOffset.FromUnixTimeMilliseconds(message.Time).ToLocalTime().ToString("HH:mm"),10,color:palette.Muted));card.AddView(row,Match());
            var preview=Text(message.Text,13,color:palette.Muted);preview.SetMaxLines(3);preview.SetPadding(0,Dp(10),0,Dp(6));card.AddView(preview,Match());
            card.AddView(Text(apps.FirstOrDefault(a=>a.PackageName==message.Package)?.Label??message.Package,10,color:palette.Muted),Match());
            card.Click+=(_,_)=>
            {
                OpenNotification(message);
            };
            card.LongClick+=(_,_)=>Choose(message.Title,["Open conversation","Dismiss notification"],i=>
            {
                if(i==1)ConversationService.Dismiss(message.Key);
                else OpenNotification(message);
            });
            var lp=Match();lp.SetMargins(0,0,0,Dp(10));body.AddView(card,lp);
        }
    }

    protected override void OnActivityResult(int requestCode,Result resultCode,Intent? data)
    {
        base.OnActivityResult(requestCode,resultCode,data);
        if(requestCode==811){CompleteCredentialAuthentication(resultCode);return;}
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
                var buffer=new char[2_000_001];int count=0,n;
                while(count<buffer.Length&&(n=reader.Read(buffer,count,buffer.Length-count))>0)count+=n;
                var restored=LauncherState.Deserialize(new string(buffer,0,count));
                Confirm("Restore layout?",$"Replace your layout and notes with this backup ({restored.Drawers.Count} drawers)? Existing Android widgets will be kept.","Restore",()=>
                    {
                        restored.Widgets=state.Widgets;state=restored;AppOrganization.Reconcile(state,apps);Save();Navigate("Tiles");Toast("Layout restored.");
                    });
            }
        }
        catch(Exception e) when(e is IOException or System.Text.Json.JsonException or InvalidDataException or Java.Lang.RuntimeException or UnauthorizedAccessException)
        {Toast("Could not read or write this backup. Choose a valid Launcher JSON file.");}
    }
}
