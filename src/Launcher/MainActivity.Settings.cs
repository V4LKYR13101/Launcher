using Android.App;
using Android.App.Roles;
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
    private void SettingRow(LinearLayout parent,string title,string detail,Action action,string glyph="more")
    {
        var row=Stack(true); row.SetGravity(GravityFlags.CenterVertical); row.SetMinimumHeight(Dp(64));
        var labels=Stack(); labels.SetPadding(Dp(4),Dp(9),Dp(8),Dp(9));
        labels.AddView(Text(title,16,true),Match());
        if(detail.Length>0) { var sub=Text(detail,12,color:palette.Muted); sub.SetPadding(0,Dp(4),0,0); labels.AddView(sub,Match()); }
        row.AddView(labels,new LinearLayout.LayoutParams(0,-2,1));
        row.AddView(new GlyphView(this,glyph,palette.Muted),new LinearLayout.LayoutParams(Dp(20),Dp(20)));
        row.Click+=(_,_)=>action(); parent.AddView(row,Match());
    }

    private void ToggleRow(LinearLayout parent,string title,bool value,Action<bool> action)
    {
        var toggle=new Switch(this) { Text=title,Checked=value,TextSize=15,Typeface=regular };
        toggle.ThumbTintList=AccentTint();toggle.TrackTintList=AccentTint();
        toggle.SetTextColor(palette.Text); toggle.SetPadding(Dp(4),Dp(6),0,Dp(6)); toggle.SetMinimumHeight(Dp(56));
        toggle.CheckedChange+=(_,e)=>action(e.IsChecked); parent.AddView(toggle,Match());
    }

    private void Choose(string title,string[] options,Action<int> action) => Sheet(title,(panel,dialog)=>
    {
        for(int i=0;i<options.Length;i++)
        {
            int index=i;
            var option=Button(options[i],()=>{dialog.Dismiss();action(index);});
            option.Gravity=GravityFlags.Left|GravityFlags.CenterVertical;
            var lp=Match();lp.SetMargins(0,0,0,Dp(8));panel.AddView(option,lp);
        }
    });

    private void Prompt(string title,string initial,Action<string> action)
    {
        EditText? input=null;
        var dialog=Sheet(title,(panel,sheet)=>
        {
            bool weather=title=="Weather location";
            Paragraph(weather?"Search for a city or town, anywhere in the world.":title=="New drawer"?"Give your apps a space of their own.":"Choose a name that feels right.",panel);
            var label=Text(weather?"CITY":title.Contains("#")?"HEX COLOR":"NAME",10,true,palette.Muted);label.SetPadding(Dp(4),0,0,Dp(8));panel.AddView(label,Match());
            input=new EditText(this) {Text=initial,Hint=weather?"e.g. Cape Town, South Africa":title=="New drawer"?"e.g. Daily essentials":title,TextSize=19,Typeface=regular};
            input.SetSingleLine(true);input.SetTextColor(palette.Text);input.SetHintTextColor(palette.Muted);
            input.Background=Background(palette.Panel,16);input.SetPadding(Dp(16),Dp(16),Dp(16),Dp(16));
            input.SetSelection(initial.Length);panel.AddView(input,Match(Dp(62)));
            Gap(panel,6);
        },(actions,sheet)=>
        {
            actions.AddView(Button(title=="Weather location"?"Search":title=="New drawer"?"Create drawer":"Save",()=>{sheet.Dismiss();action(input?.Text??"");},true),Match());
            Gap(actions,8);actions.AddView(Button("Cancel",()=>sheet.Dismiss()),Match());
        });
        input!.RequestFocus();input.PostDelayed(()=>((Android.Views.InputMethods.InputMethodManager?)GetSystemService(InputMethodService))?.ShowSoftInput(input,Android.Views.InputMethods.ShowFlags.Implicit),180);
    }

    private void Confirm(string title,string message,string actionLabel,Action action)=>Sheet(title,(panel,dialog)=>Paragraph(message,panel),(footer,dialog)=>
    {
        footer.AddView(Button(actionLabel,()=>{dialog.Dismiss();action();},true),Match());Gap(footer,8);
        footer.AddView(Button("Cancel",()=>dialog.Dismiss()),Match());
    });

    private void QuickControls()
    {
        Sheet("Launcher",(panel,dialog)=>
        {
            void Go(Action action) { dialog.Dismiss(); action(); }
            var navigation=Stack(true);
            foreach(var (label,target) in new[]{("Root","Root"),("Apps","Tiles"),("Notifications","Conversations")})
            {
                var lp=new LinearLayout.LayoutParams(0,Dp(48),1);lp.SetMargins(Dp(3),0,Dp(3),0);
                navigation.AddView(Button(label,()=>Go(()=>Navigate(target))),lp);
            }
            panel.AddView(navigation,Match());Gap(panel,8);
            SettingRow(panel,"Light",state.Theme=="Light"?"On":"Soft light theme",()=>Go(()=>{state.Theme="Light";Save();Render();}),"sun");
            SettingRow(panel,"Sun",state.Theme=="Sun"?"On":"High contrast, outdoors",()=>Go(()=>{state.Theme="Sun";state.UseWallpaper=false;Save();Render();}),"sun");
            SettingRow(panel,"Dark",state.Theme=="Dark"?"On":"Quiet, dark surfaces",()=>Go(()=>{state.Theme="Dark";Save();Render();}),"focus");
            SettingRow(panel,"Focus",state.Focus?"On · tap to turn off":"Essentials and pinned apps only",()=>Go(()=>{state.Focus=!state.Focus;state.FocusEndsAt=0;Save();Render();}),"focus");
            SettingRow(panel,page=="Root"?"Add widget":"Add drawer","",()=>Go(()=>{if(page=="Root")AddWidget();else NewDrawer();}),"plus");
            SettingRow(panel,"All apps","Search your installed apps",()=>Go(()=>{page="Tiles";StartSearch();}),"grid");
            SettingRow(panel,"Settings","Make Launcher your own",()=>Go(()=>Navigate("Settings")),"settings");
        });
    }

    private void RenderSettings()
    {
        Title("Launcher"); Paragraph("Your phone. At your pace.");
        SettingRow(body,"Appearance","Themes, layout, typography & tiles",()=>Navigate("Appearance"),"sun");
        SettingRow(body,"Drawers","Create, rename, reorder & organize",()=>Navigate("Drawers"),"grid");
        SettingRow(body,"Root","Cards, notes & Android widgets",()=>Navigate("Root settings"),"root");
        SettingRow(body,"Conversations","Choose notification sources & filters",PickConversationSources,"message");
        SettingRow(body,"Weather",string.IsNullOrEmpty(state.Weather.City)?"Choose a city, units & home strip":state.Weather.City,()=>Navigate("Weather"),"cloud");
        SettingRow(body,"App updates","Check, download & install from Kevlog",()=>Navigate("Updates"),"down");
        SettingRow(body,"Hidden apps",$"{apps.Count(a=>state.Apps[a.Id].Hidden)} hidden · tap to restore",()=>Navigate("Hidden"),"app");
        SettingRow(body,"Permissions","Default home, usage & conversations",()=>Navigate("Permissions"),"settings");
        SettingRow(body,"Quick panel","Launcher controls & current notifications",()=>Navigate("Quick panel"),"down");
        Gap(body,12);
        ToggleRow(body,"Focus mode",state.Focus,v=>{state.Focus=v;if(!v)state.FocusEndsAt=0;Save();});
        Paragraph("Focus keeps Essentials and pinned apps visible. It doesn’t block other apps or change your notification settings.");
        SettingRow(body,"Back up layout","Save a JSON file",ExportBackup,"file");
        SettingRow(body,"Restore layout","Import your saved customization",ImportBackup,"file");
        SettingRow(body,"How to use Launcher","Gestures, drawers, tiles & privacy",ShowGuide,"note");
        Gap(body); Paragraph("Launcher · 1.0\nBuilt with C# and .NET for Android. Settings and notes stay on this device. No ads or accounts. Weather uses Open-Meteo.");
    }

    private void RenderAppearance()
    {
        Title("Make space.");
        Paragraph("Choose a starting point, then adjust each detail.");
        var presets=Stack(true);
        foreach(string preset in new[]{"Classic","Studio"})
        {
            var b=Button(preset,()=>{state.ApplyPreset(preset);Save();Render();});
            var lp=new LinearLayout.LayoutParams(0,Dp(54),1); lp.SetMargins(0,0,Dp(8),0); presets.AddView(b,lp);
        }
        body.AddView(presets,Match()); Gap(body);
        SettingRow(body,"Theme",state.Theme,()=>Choose("Theme",["Dark","Light","Sun"],i=>{state.Theme=new[]{"Dark","Light","Sun"}[i];if(i==2)state.UseWallpaper=false;Save();Render();}),"sun");
        SettingRow(body,"Accent",state.Accent,()=>Choose("Accent",["Acid yellow","Warm white","Mint","Sky","Lilac","Peach","Custom hex color"],i=>
        {
            if(i==6)Prompt("Accent · #RRGGBB",state.Accent,s=>
            {
                if(!System.Text.RegularExpressions.Regex.IsMatch(s,"^#[0-9a-fA-F]{6}$")){Toast("Use a color such as #E4FF00.");return;}
                state.Accent=s;Save();Render();
            });
            else {state.Accent=new[]{"#E4FF00","#F4F4E8","#9FE7C1","#9BCBFF","#CEB8FF","#FFBB98"}[i];Save();Render();}
        }),"photo");
        SettingRow(body,"Columns",state.Columns.ToString(),()=>Choose("Columns",["3","4","5","6"],i=>{state.Columns=i+3;Save();Render();}),"grid");
        SettingRow(body,"Tile height",state.TileHeight+" dp",()=>Choose("Tile height",["Compact · 88","Balanced · 100","Classic · 112","Spacious · 132","Large · 152"],i=>{state.TileHeight=new[]{88,100,112,132,152}[i];Save();Render();}));
        SettingRow(body,"Corners",state.CornerRadius+" dp",()=>Choose("Tile corners",["Square","Subtle · 6","Soft · 12","Round · 20","Pill · 28"],i=>{state.CornerRadius=new[]{0,6,12,20,28}[i];Save();Render();}));
        SettingRow(body,"Icon style",state.IconStyle,()=>Choose("Icon style",["Monochrome","Grayscale","Original"],i=>{state.IconStyle=new[]{"Monochrome","Grayscale","Original"}[i];Save();Render();}),"photo");
        SettingRow(body,"Drawer alignment",state.Alignment,()=>Choose("Drawer alignment",["Top","Center","Bottom"],i=>{state.Alignment=new[]{"Top","Center","Bottom"}[i];Save();Render();}));
        ToggleRow(body,"Centered drawer headings",state.CenterHeadings,v=>{state.CenterHeadings=v;Save();});
        ToggleRow(body,"App labels",state.ShowLabels,v=>{state.ShowLabels=v;Save();});
        ToggleRow(body,"Show usage time",state.ShowUsage,v=>{state.ShowUsage=v;Save();});
        ToggleRow(body,"Notification count badges",state.ShowNotificationBadges,v=>{state.ShowNotificationBadges=v;Save();});
        ToggleRow(body,"Use system wallpaper",state.UseWallpaper,v=>{state.UseWallpaper=v;Save();Render();});
        ToggleRow(body,"Minimal status bar",state.MinimalStatusBar,v=>{state.MinimalStatusBar=v;Save();Render();});
        SettingRow(body,"Status row position",state.StatusPlacement=="Top"?"Top corners":"Below camera cutout",()=>Choose("Status row position",["Top corners","Below camera cutout"],i=>{state.StatusPlacement=i==0?"Top":"Safe";Save();Render();}));
        ToggleRow(body,"Media playback card",state.ShowMediaCard,v=>{state.ShowMediaCard=v;Save();});
        Paragraph("Monochrome uses each app’s own themed icon when available, otherwise its real icon in grayscale. Every app keeps its identity. Pull down on the home screen for Launcher’s quick panel. Android’s system shade remains available from the screen edge.");
    }

    private void RenderDrawerSettings()
    {
        body.AddView(Button("+  New drawer",NewDrawer,true),Match()); Gap(body);
        foreach(var drawer in state.Drawers)
        {
            SettingRow(body,drawer.Name,$"{apps.Count(a=>state.Apps[a.Id].DrawerId==drawer.Id)} apps",()=>EditDrawer(drawer),"grid");
        }
        Gap(body); Paragraph("Hold a drawer heading on the home screen to edit it. Apps in a removed drawer return to App box.");
    }
    private void NewDrawer() => Prompt("New drawer","",s=>
    {
        if(string.IsNullOrWhiteSpace(s))return;
        if(state.Drawers.Count>=40){Toast("You can create up to 40 drawers.");return;}
        var drawer=new Drawer {Name=LauncherState.CleanName(s,"Drawer"),Expanded=true};
        state.Drawers.Insert(Math.Max(0,state.Drawers.Count-1),drawer); Save();Render(); SelectDrawerApps(drawer);
    });
    private void EditDrawer(Drawer drawer)
    {
        Sheet(drawer.Name,(panel,dialog)=>
        {
            void Done(Action change) {change();Save();dialog.Dismiss();Render();}
            SettingRow(panel,"Rename",drawer.Name,()=>Prompt("Drawer name",drawer.Name,s=>Done(()=>drawer.Name=LauncherState.CleanName(s,drawer.Name))));
            SettingRow(panel,"Drawer icon","Choose a geometric symbol",()=>{dialog.Dismiss();PickDrawerIcon(drawer);},DrawerIcons.All.Contains(drawer.Icon)?drawer.Icon:DrawerIcons.ForDrawer(drawer.Id));
            SettingRow(panel,"Choose apps","Select the apps in this drawer",()=>{dialog.Dismiss();SelectDrawerApps(drawer);},"grid");
            ToggleRow(panel,"Expanded",drawer.Expanded,v=>Done(()=>drawer.Expanded=v));
            SettingRow(panel,"Move up","",()=>Done(()=>MoveDrawer(drawer,-1)),"up");
            SettingRow(panel,"Move down","",()=>Done(()=>MoveDrawer(drawer,1)),"down");
            if(drawer.Id!="other")SettingRow(panel,"Remove drawer","Apps move to App box",()=>
                Confirm("Remove "+drawer.Name+"?","Its apps will move to App box.","Remove drawer",()=>Done(()=>state.RemoveDrawer(drawer.Id))));
            panel.AddView(Button("Done",()=>dialog.Dismiss(),true),Match());
        });
    }
    private void MoveDrawer(Drawer drawer,int direction)
    {
        int i=state.Drawers.IndexOf(drawer),j=i+direction;
        if(j<0 || j>=state.Drawers.Count)return;
        (state.Drawers[i],state.Drawers[j])=(state.Drawers[j],state.Drawers[i]);
    }
    private void SelectDrawerApps(Drawer drawer)
    {
        var options=apps.OrderBy(a=>a.Label).ToArray();
        var selected=options.Select(a=>state.Apps[a.Id].DrawerId==drawer.Id).ToArray();
        Sheet(drawer.Name,(panel,dialog)=>
        {
            Paragraph("Choose the apps for this drawer.",panel);
            for(int i=0;i<options.Length;i++)
            {
                int index=i;var app=options[i];
                var check=new CheckBox(this){Text=app.Label+(state.Apps[app.Id].Hidden?" (hidden)":""),Checked=selected[i],Typeface=regular,TextSize=15};
                check.ButtonTintList=AccentTint();
                check.SetTextColor(palette.Text);check.SetMinimumHeight(Dp(52));check.CheckedChange+=(_,e)=>selected[index]=e.IsChecked;panel.AddView(check,Match());
            }
        },(footer,dialog)=>
        {
            footer.AddView(Button("Save",()=>
            {
                for(int i=0;i<options.Length;i++)
                {
                    var rule=state.Apps[options[i].Id];
                    if(selected[i])rule.DrawerId=drawer.Id;
                    else if(rule.DrawerId==drawer.Id)rule.DrawerId="other";
                }
                Save();dialog.Dismiss();Render();
            },true),Match());Gap(footer,8);footer.AddView(Button("Cancel",()=>dialog.Dismiss()),Match());
        });
    }
    private void RenderHidden()
    {
        Paragraph("Hidden apps are removed from Launcher tiles and search. This does not secure or disable them.");
        var hidden=apps.Where(a=>state.Apps[a.Id].Hidden).ToArray();
        if(hidden.Length==0)Paragraph("No hidden apps. Hold an app tile to hide it.");
        foreach(var app in hidden)SettingRow(body,app.Label,"Tap to show",()=>{state.Apps[app.Id].Hidden=false;Save();Render();},"plus");
    }
    private void RenderPermissions()
    {
        Title("You’re in control.");
        Paragraph("Launcher works without optional permissions. Enable only the features you want.");
        SettingRow(body,"Default home app",IsDefaultHome()?"Launcher is your home":"Choose Launcher in Android’s chooser",RequestHome,"grid");
        SettingRow(body,"Usage access",AppCatalog.HasUsageAccess(this)?"Enabled":"Optional · real app usage and daily goals",()=>OpenIntent(new Intent(Android.Provider.Settings.ActionUsageAccessSettings,Uri.Parse("package:"+PackageName))),"clock");
        SettingRow(body,"Notification access",ConversationService.Enabled(this)?"Enabled · quick panel and conversations":"Optional · current notifications and messages",()=>
            Confirm("Notification access","Launcher shows current notifications in its quick panel and message previews in Conversations. These stay in memory on your device. You can revoke access in Android settings.","Open settings",()=>OpenIntent(new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS"))),"message");
        SettingRow(body,"Connection names",connections.CanReadBluetooth&&connections.CanReadWifiName?"Enabled":"Optional · Wi-Fi and connected Bluetooth names",RequestConnectionDetails,"wifi");
        Paragraph("No accessibility service, contacts, SMS or storage permission. Connection names optionally require nearby-device and precise location access. Weather uses internet access for your chosen city; Launcher does not request GPS coordinates. Work-profile and private-space apps are not supported in this version.");
    }
    private bool IsDefaultHome()
    {
        var home=new Intent(Intent.ActionMain);home.AddCategory(Intent.CategoryHome);
        return PackageManager!.ResolveActivity(home,Android.Content.PM.PackageInfoFlags.MatchDefaultOnly)?.ActivityInfo?.PackageName==PackageName;
    }
    private void RequestHome()
    {
        if(OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            var roles=(RoleManager?)GetSystemService(RoleService);
            if(roles?.IsRoleAvailable(RoleManager.RoleHome)==true && !roles.IsRoleHeld(RoleManager.RoleHome))
            {StartActivityForResult(roles.CreateRequestRoleIntent(RoleManager.RoleHome),601);return;}
        }
        OpenIntent(new Intent(Android.Provider.Settings.ActionHomeSettings));
    }
    private void ShowGuide() => Sheet("A quieter home",(panel,dialog)=>
    {
        Paragraph("Tap a drawer heading to expand it. Hold it to rename, reorder or choose its apps.",panel);
        Paragraph("Tap an app to open it. Hold an app to move, rename, pin, highlight, expand, hide or set a daily usage goal.",panel);
        Paragraph("Swipe right for Root; swipe left for Conversations. The menu inside search also opens Root, Apps and Notifications.",panel);
        Paragraph("Pull down within the top of the Tiles screen for Launcher’s quick panel. Tap the minimal status row to open it too. The screen-edge notification shade belongs to Android.",panel);
        Paragraph("Hold the search icon, or tap the three dots, for theme and focus controls. Focus leaves Essentials and pinned apps visible.",panel);
        Paragraph("Root has editable notes, calendar, calculator, a focus timer and support for your installed Android widgets.",panel);
        panel.AddView(Button("Got it",()=>dialog.Dismiss(),true),Match());
    });
    private void ExportBackup()
    {
        var intent=new Intent(Intent.ActionCreateDocument); intent.AddCategory(Intent.CategoryOpenable); intent.SetType("application/json");
        intent.PutExtra(Intent.ExtraTitle,"launcher-layout.json");StartActivityForResult(intent,701);
    }
    private void ImportBackup()
    {
        var intent=new Intent(Intent.ActionOpenDocument);intent.AddCategory(Intent.CategoryOpenable);intent.SetType("*/*");StartActivityForResult(intent,702);
    }
}
