using Android.Graphics.Drawables;
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
    private int matchingSettings;
    private void SettingRow(LinearLayout parent,string title,string detail,Action action,string glyph="more")
    {
        if(ReferenceEquals(parent,body)&&!SettingMatches(title+" "+detail))return;
        if(ReferenceEquals(parent,body))matchingSettings++;
        var row=Stack(true); row.SetGravity(GravityFlags.CenterVertical); row.SetMinimumHeight(Dp(64));
        var labels=Stack(); labels.SetPadding(0,Dp(10),Dp(10),Dp(10));
        labels.AddView(Text(title,15,true),Match());
        if(detail.Length>0) { var sub=Text(detail,12,color:palette.Muted); sub.SetPadding(0,Dp(4),0,0);sub.SetMaxLines(2);sub.Ellipsize=Android.Text.TextUtils.TruncateAt.End;sub.SetLineSpacing(Dp(2),1); labels.AddView(sub,Match()); }
        row.AddView(labels,new LinearLayout.LayoutParams(0,-2,1));
        row.AddView(new GlyphView(this,glyph,palette.Muted),new LinearLayout.LayoutParams(Dp(18),Dp(18)));
        TouchFeedback(row);row.Click+=(_,_)=>action(); parent.AddView(row,Match());
    }

    private void ToggleRow(LinearLayout parent,string title,bool value,Action<bool> action)
    {
        if(ReferenceEquals(parent,body)&&!SettingMatches(title))return;
        if(ReferenceEquals(parent,body))matchingSettings++;
        var toggle=new Switch(this) { Text=title,Checked=value,TextSize=14,Typeface=regular };
        toggle.ThumbTintList=AccentTint();toggle.TrackTintList=SwitchTrackTint();
        toggle.SetTextColor(new Android.Content.Res.ColorStateList([new[]{-Android.Resource.Attribute.StateEnabled},Array.Empty<int>()],[palette.Muted.ToArgb(),palette.Text.ToArgb()])); toggle.SetPadding(0,Dp(8),0,Dp(8)); toggle.SetMinimumHeight(Dp(52));
        BindToggleHaptics(toggle);toggle.CheckedChange+=(_,e)=>action(e.IsChecked); parent.AddView(toggle,Match());
    }

    private int SelectedChoice(string title)=>title switch
    {
        "Theme"=>state.FollowSystemTheme?3:Array.IndexOf(new[]{"Dark","Light","Sun"},state.Theme),
        "Accent"=>Math.Min(6,Array.FindIndex(new[]{"#E4FF00","#F4F4E8","#9FE7C1","#9BCBFF","#CEB8FF","#FFBB98",state.Accent},color=>color.Equals(state.Accent,StringComparison.OrdinalIgnoreCase))),
        "Columns"=>state.Columns-3,"Tile height"=>Array.IndexOf(new[]{88,100,112,132,152},state.TileHeight),
        "Wallpaper dimming"=>Array.IndexOf(new[]{35,50,65,80},state.WallpaperDim),"Tile corners"=>Array.IndexOf(new[]{0,6,12,20,28},state.CornerRadius),
        "Icon style"=>Array.IndexOf(new[]{"Monochrome","Grayscale","Original"},state.IconStyle),"Drawer alignment"=>Array.IndexOf(new[]{"Top","Center","Bottom"},state.Alignment),
        "Temperature units"=>state.Weather.Fahrenheit?1:0,"Weather refresh"=>Array.IndexOf(new[]{0,30,60,180,360},state.Weather.RefreshMinutes),
        "Status row position"=>state.StatusPlacement=="Top"?0:1,"Switching delay"=>Array.IndexOf(new[]{3,5,10,20},state.LightThemeDelaySeconds),_=>-1
    };
    private void Choose(string title,string[] options,Action<int> action,int selected=-1) => Sheet(title,(panel,dialog)=>
    {
        int current=selected>=0?selected:SelectedChoice(title);
        for(int i=0;i<options.Length;i++)
        {
            int index=i;
            bool active=i==current;
            var option=Text(options[i]+(active?"   ✓":""),15,active,active?palette.Accent:palette.Text);option.Gravity=GravityFlags.Start|GravityFlags.CenterVertical;option.SetMinimumHeight(Dp(52));option.SetPadding(Dp(14),Dp(14),Dp(14),Dp(14));option.Selected=active;option.ContentDescription=options[i]+(active?", selected":"");
            if(active)option.Background=Background(new Color(palette.Accent.R,palette.Accent.G,palette.Accent.B,(byte)20),12);
            TouchFeedback(option,10);option.Click+=(_,_)=>{dialog.Dismiss();action(index);};panel.AddView(option,Match());
            if(i<options.Length-1)panel.AddView(new View(this){Background=new ColorDrawable(palette.Divider)},Match(Dp(1)));
        }
    });

    private void Prompt(string title,string initial,Action<string> action)
    {
        EditText? input=null;
        var dialog=Sheet(title,(panel,sheet)=>
        {
            bool weather=title=="Weather location";
            bool secret=title.Contains("API key",StringComparison.OrdinalIgnoreCase);bool lux=title.Contains("(lux)");
            bool feedCount=title=="Articles to display";
            bool duration=title.Contains("duration",StringComparison.OrdinalIgnoreCase),address=title.Contains("address",StringComparison.OrdinalIgnoreCase),code=title=="Pairing code",colour=title.Contains("#");
            string help=feedCount?"Choose how many stories appear initially, from 1 to 50.":lux?"Enter the sensor level in lux. The Light threshold must be higher than the Dark threshold.":weather?"Search for a city or town.":secret?"Paste your provider key. Leave this blank to remove it.":duration?"Use a duration such as 10 min, 30s or 1h.":address?"Enter the address shown by Launcher Companion.":code?"Enter the eight-digit code shown on your computer.":colour?"Use a six-digit hex colour, such as #E4FF00.":title.Contains("command")||title.Contains("query")?"Enter the search or value for this card.":"Enter a name below.";
            Paragraph(help,panel);
            var label=Text(feedCount?"STORY COUNT":lux?"LIGHT LEVEL (LUX)":weather?"CITY":secret?"API KEY":duration?"DURATION":address?"ADDRESS":code?"PAIRING CODE":colour?"HEX COLOUR":"NAME",10,true,palette.Muted);label.SetPadding(Dp(4),0,0,Dp(8));panel.AddView(label,Match());
            input=new EditText(this) {Text=initial,Hint=weather?"e.g. Cape Town, South Africa":title=="New drawer"?"e.g. Daily essentials":title,TextSize=18,Typeface=regular};
            input.SetSingleLine(true);if(lux)input.InputType=Android.Text.InputTypes.ClassNumber|Android.Text.InputTypes.NumberFlagDecimal;else if(secret)input.InputType=Android.Text.InputTypes.ClassText|Android.Text.InputTypes.TextVariationPassword;else if(address)input.InputType=Android.Text.InputTypes.ClassText|Android.Text.InputTypes.TextVariationUri;else if(code||feedCount)input.InputType=Android.Text.InputTypes.ClassNumber;input.Typeface=regular;input.SetTextColor(palette.Text);input.SetHintTextColor(palette.Muted);
            input.Background=Background(palette.Card,12,palette.Divider);input.SetPadding(Dp(14),Dp(12),Dp(14),Dp(12));
            StyleEntry(input);input.SetSelection(initial.Length);panel.AddView(input,Match(Math.Max(Dp(56),Dp(18*FontScale*1.3f+24))));
            Gap(panel,6);
        },(actions,sheet)=>
        {
            DialogActions(actions,("Cancel",sheet.Dismiss),(title=="Weather location"?"Search":title=="New drawer"?"Create drawer":"Save",()=>
            {string value=input?.Text??"",error=ValidatePrompt(title,value);if(error.Length>0){input!.Error=error;input.RequestFocus();return;}sheet.Dismiss();action(value);}));
        });
        input!.RequestFocus();input.PostDelayed(()=>((Android.Views.InputMethods.InputMethodManager?)GetSystemService(InputMethodService))?.ShowSoftInput(input,Android.Views.InputMethods.ShowFlags.Implicit),180);
    }

    private string ValidatePrompt(string title,string value)
    {
        string text=value.Trim();
        if(title.Contains("API key",StringComparison.OrdinalIgnoreCase)||title=="Search aliases")return "";
        if(title.Contains('#'))return System.Text.RegularExpressions.Regex.IsMatch(text,"^#[0-9a-fA-F]{6}$")?"":"Use six hex digits, such as #E4FF00.";
        if(title=="Articles to display")return int.TryParse(text,out int count)&&count is >=1 and <=50?"":"Choose a number from 1 to 50.";
        if(title.Contains("(lux)"))
        {
            if((!double.TryParse(text,out double lux)&&!double.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out lux))||!double.IsFinite(lux)||lux is <0 or >200000)return "Enter a light level from 0 to 200000 lux.";
            return title.StartsWith("Light")&&lux<=state.DarkThemeLux||title.StartsWith("Dark")&&lux>=state.LightThemeLux?"The Light threshold must be higher than the Dark threshold.":"";
        }
        if(title.Contains("duration",StringComparison.OrdinalIgnoreCase)){try{RootCommand.TimerDuration(text);return "";}catch(ArgumentException e){return e.Message;}}
        if(title.Contains("address",StringComparison.OrdinalIgnoreCase)){try{CompanionLink.ValidateAddress(text);return "";}catch(ArgumentException e){return e.Message;}}
        if(title=="Pairing code")return text.Length==8&&text.All(char.IsAsciiDigit)?"":"Enter the eight-digit code shown on the computer.";
        if(title=="Weather location")return text.Length>=2?"":"Enter at least two characters.";
        if(title=="Save a link")return OnlineCards.SafeLink(text)?"":"Enter a full http:// or https:// link.";
        return text.Length==0?"Enter a value before saving.":"";
    }

    private void Confirm(string title,string message,string actionLabel,Action action)=>Sheet(title,(panel,dialog)=>Paragraph(message,panel),(footer,dialog)=>
    {
        DialogActions(footer,("Cancel",dialog.Dismiss),(actionLabel,()=>{dialog.Dismiss();action();}));
    });

    private void QuickControls()
    {
        Sheet("Launcher",(panel,dialog)=>
        {
            void Go(Action action) { dialog.Dismiss(); action(); }
            var navigation=Stack(FontScale<1.4f);
            foreach(var (label,target) in new[]{("Today","Root"),("Apps","Tiles"),("Notifications","Conversations")})
            {
                var lp=FontScale>=1.4f?Match():new LinearLayout.LayoutParams(0,-1,1);lp.SetMargins(Dp(3),0,Dp(3),0);
                navigation.AddView(Button(label,()=>Go(()=>Navigate(target))),lp);
            }
            panel.AddView(navigation,Match());Gap(panel,8);
            SettingRow(panel,"Light",state.Theme=="Light"?"On":"Soft light theme",()=>Go(()=>{ManualTheme("Light");}),"sun");
            SettingRow(panel,"Sun",state.Theme=="Sun"?"On":"High contrast, outdoors",()=>Go(()=>{ManualTheme("Sun");}),"sun");
            SettingRow(panel,"Dark",state.Theme=="Dark"?"On":"Quiet, dark surfaces",()=>Go(()=>{ManualTheme("Dark");}),"focus");
            SettingRow(panel,"Focus",state.Focus?"On · tap to turn off":"Quiet outlined tiles",()=>Go(()=>{state.Focus=!state.Focus;state.FocusEndsAt=0;Save();Render();}),"focus");
            SettingRow(panel,page=="Root"?"Add widget":"Add drawer","",()=>Go(()=>{if(page=="Root")AddWidget();else NewDrawer();}),"plus");
            SettingRow(panel,"All apps","Search your installed apps",()=>Go(()=>{page="Tiles";StartSearch();}),"grid");
            SettingRow(panel,"Settings","Make Launcher your own",()=>Go(()=>Navigate("Settings")),"settings");
        });
    }

    private void RenderSettings()
    {
        matchingSettings=0;
        SectionCaption(body,"Home screen");
        SettingRow(body,"Setup & feature health","Access, device support and recovery",ShowFeatureHealth,"settings");
        SettingRow(body,"Edit layout","Arrange apps and cards · undo changes",ShowLayoutEditor,"grid");
        SettingRow(body,"Haptic feedback",state.HapticFeedback=="Off"?"Off":state.HapticFeedback+" · follows Android touch vibration",ShowHapticSettings,"phone");
        SettingRow(body,"Appearance","Themes, layout, typography & tiles",()=>Navigate("Appearance"),"sun");
        SettingRow(body,"Automatic theme",state.AutomaticLightTheme?"On · light sensor":"Off · choose the theme yourself",()=>Navigate("Automatic theme"),"sun");
        SettingRow(body,"Drawers","Create, rename, reorder & organise",()=>Navigate("Drawers"),"grid");
        SettingRow(body,"Hidden apps",$"{apps.Count(a=>state.Apps[a.Id].Hidden)} hidden · tap to restore",()=>Navigate("Hidden"),"app");
        ToggleRow(body,"Focus mode",state.Focus,v=>{state.Focus=v;if(!v)state.FocusEndsAt=0;Save();});
        Paragraph("Focus outlines app tiles. Apps stay available and Android notification settings are unchanged.");
        SectionCaption(body,"Cards and conversations");
        SettingRow(body,"Root","Cards, notes & Android widgets",()=>Navigate("Root settings"),"root");
        SettingRow(body,"News & feeds",Counted(Sources.Count(s=>s.Enabled),"source")+" · "+Counted(state.FeedArticleCount,"story","stories")+$" · images {(state.FeedImages?"on":"off")}",ManageFeeds,"rss");
        SettingRow(body,"Saved stories",Counted(state.SavedArticles.Count,"saved story","saved stories"),ShowSavedArticles,"rss");
        SettingRow(body,"Weather",string.IsNullOrEmpty(state.Weather.City)?"Choose a city and units":state.Weather.City,()=>Navigate("Weather"),"cloud");
        SettingRow(body,"Conversations","Choose notification sources & filters",PickConversationSources,"message");
        SettingRow(body,"Message history",state.KeepMessageHistory?"Saving new message notifications":"Optional · messages & inline replies",MessageHistorySettings,"message");
        SettingRow(body,"Privacy & recovery","Protected previews, backups and data",ShowPrivacyRecovery,"lock");
        SettingRow(body,"Timer alerts","Notifications & exact alarm access",OpenTimerPermissions,"clock");
        SectionCaption(body,"Connections and access");
        SettingRow(body,"System panel",state.SystemPanelEnabled?SystemPanelService.IsEnabled(this)?"On · available across apps":"On · enable accessibility access":"Off · matching controls across apps",ShowSystemPanelSettings,"phone");
        SettingRow(body,"Desktop companion","Notes, photos & files on your network",()=>Navigate("Desktop companion"),"computer");
        SettingRow(body,"Online cards","RSS, web search & YouTube",()=>Navigate("Integrations"),"link");
        SettingRow(body,"App grayscale","Display permission & foreground detection",()=>Navigate("Grayscale setup"),"photo");
        SettingRow(body,"Permissions","Default home, usage & conversations",()=>Navigate("Permissions"),"settings");
        SettingRow(body,"Quick panel","Launcher controls & current notifications",()=>Navigate("Quick panel"),"down");
        SectionCaption(body,"Updates and backup");
        SettingRow(body,"App updates","Check, download & install from Kevlog",()=>Navigate("Updates"),"down");
        SettingRow(body,"Back up layout","Save a JSON file",ExportBackup,"file");
        SettingRow(body,"Restore layout","Import your saved layout",ImportBackup,"file");
        SettingRow(body,"How to use Launcher","Gestures, drawers, tiles & privacy",ShowGuide,"note");
        if(FilteringSettings&&matchingSettings==0){var empty=Text("No settings match “"+settingsQuery.Trim()+"”. Try a feature such as tabs, accent or privacy.",14,color:palette.Muted);empty.SetPadding(0,Dp(20),0,Dp(20));body.AddView(empty,Match());}
        Gap(body);Paragraph("Launcher · "+InstalledPackage().VersionName+"\nSettings and notes stay on this device. Weather uses Open-Meteo.");
    }

    private void RenderAppearance()
    {
        Title("Appearance");
        SettingRow(body,"Automatic light / dark",state.AutomaticLightTheme?"Enabled":"Off",()=>Navigate("Automatic theme"),"sun");
        Paragraph("Choose a starting point, then adjust each detail.");
        var presets=Stack(true);
        foreach(string preset in new[]{"Classic","Studio"})
        {
            var b=Button(preset,()=>{state.AutomaticLightTheme=false;state.ApplyPreset(preset);Save();Render();});
            var lp=new LinearLayout.LayoutParams(0,-2,1); lp.SetMargins(0,0,Dp(8),0); presets.AddView(b,lp);
        }
        body.AddView(presets,Match()); Gap(body);
        SettingRow(body,"Theme",state.FollowSystemTheme?"Follow Android":state.Theme,()=>Choose("Theme",["Dark","Light","Sun","Follow Android"],i=>{if(i==3){state.FollowSystemTheme=true;state.AutomaticLightTheme=false;SyncSystemTheme();Save();Render();}else ManualTheme(new[]{"Dark","Light","Sun"}[i]);}),"sun");
        SettingRow(body,"Wallpaper dimming",state.WallpaperDim+"% · keeps text readable",()=>Choose("Wallpaper dimming",["Subtle · 35%","Balanced · 50%","Deep · 65%","Strong · 80%"],i=>{state.WallpaperDim=new[]{35,50,65,80}[i];Save();Render();}),"photo");
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
        SettingRow(body,"Tile icon size",state.TileIconScale==100?"Default · 100%":state.TileIconScale+"%",ShowTileIconSize,"photo");
        SettingRow(body,"Icon style",state.IconStyle,()=>Choose("Icon style",["Monochrome","Grayscale","Original"],i=>{state.IconStyle=new[]{"Monochrome","Grayscale","Original"}[i];Save();Render();}),"photo");
        SettingRow(body,"Icon pack",state.IconPack.Length==0?"Apps’ own icons":BuiltinIcons.Libraries.FirstOrDefault(p=>p.Package==state.IconPack)?.Label??state.IconPack,()=>PickIconPack(),"photo");
        SettingRow(body,"Drawer alignment",state.Alignment,()=>Choose("Drawer alignment",["Top","Center","Bottom"],i=>{state.Alignment=new[]{"Top","Center","Bottom"}[i];Save();Render();}));
        ToggleRow(body,"Layered interface",state.LayeredInterface,v=>
        {state.LayeredInterface=v;if(!v){StopAppLayer();StopHomePageAnimation();StopRootFolds();StopDrawerAnimations();depthMotion.Stop();ResetDeviceDepth();}Save();RenderBody();});
        Paragraph("Master switch for app expansion, page perspective, drawer and card reveals, scroll masking and phone tilt. Off returns everything to flat, immediate navigation.");
        ToggleRow(body,"Tilt depth",state.TiltDepth,v=>{if(v&&!depthMotion.Available){Toast("This phone does not provide a rotation sensor.");RenderBody();return;}state.TiltDepth=v;Save();});
        body.GetChildAt(body.ChildCount-1)!.Enabled=state.LayeredInterface&&depthMotion.Available;
        Paragraph(depthMotion.Available?"Optional, subtle movement from the phone's rotation sensor. Calibrates when opened and pauses during touch. No gestures or navigation actions are added.":"Tilt depth is unavailable on this device. Other layered effects still work.");
        ToggleRow(body,"Page transitions",state.PageTransitions,v=>{state.PageTransitions=v;if(!v)StopHomePageAnimation();Save();});
        body.GetChildAt(body.ChildCount-1)!.Enabled=state.LayeredInterface;
        ToggleRow(body,"Centered drawer headings",state.CenterHeadings,v=>{state.CenterHeadings=v;Save();});
        ToggleRow(body,"App labels",state.ShowLabels,v=>{state.ShowLabels=v;Save();});
        ToggleRow(body,"Show page tabs",state.ShowHomeNavigation,v=>{state.ShowHomeNavigation=v;Save();});
        SettingRow(body,"Page tab buttons",state.HomeNavigationPages.Count==0?"None shown":string.Join(" · ",state.HomeNavigationPages.Select(PageLabel)),ShowPageTabChoices,"grid");
        ToggleRow(body,"Show usage time",state.ShowUsage,v=>{state.ShowUsage=v;Save();});
        ToggleRow(body,"Notification count badges",state.ShowNotificationBadges,v=>{state.ShowNotificationBadges=v;Save();});
        ToggleRow(body,"Use system wallpaper",state.UseWallpaper,v=>{state.UseWallpaper=v;Save();Render();});
        ToggleRow(body,"Minimal status bar",state.MinimalStatusBar,v=>{state.MinimalStatusBar=v;Save();Render();});
        SettingRow(body,"Status row position",state.StatusPlacement=="Top"?"Top corners":"Below camera cutout",()=>Choose("Status row position",["Top corners","Below camera cutout"],i=>{state.StatusPlacement=i==0?"Top":"Safe";Save();Render();}));
        ToggleRow(body,"Media playback card",state.ShowMediaCard,v=>{state.ShowMediaCard=v;Save();});
        Paragraph("Monochrome uses each app’s own themed icon when available, otherwise its real icon in grayscale. Every app keeps its identity. Pull down on the home screen for Launcher’s quick panel. Android’s system shade remains available from the screen edge.");
    }

    private void ShowPageTabChoices()=>Sheet("Page tab buttons",(panel,dialog)=>
    {
        Paragraph("Choose which buttons appear below home. Swipe between pages or use the Launcher menu to reach a page whose button is hidden.",panel);
        foreach(string target in new[]{"Root","Tiles","Conversations"})
        {string pageId=target;ToggleRow(panel,PageLabel(pageId),state.HomeNavigationPages.Contains(pageId),value=>{if(value){if(!state.HomeNavigationPages.Contains(pageId))state.HomeNavigationPages.Add(pageId);}else state.HomeNavigationPages.Remove(pageId);Save();});}
    },(footer,dialog)=>footer.AddView(Button("Done",()=>{dialog.Dismiss();RenderBody();},true),Match()));

    private void RenderDrawerSettings()
    {
        ToggleRow(body,"Drag headings to fold",state.DragDrawerHeadings,value=>{state.DragDrawerHeadings=value;Save();});
        Paragraph("Optional vertical drag on drawer headings. Leave off for normal page scrolling. Tap any heading to open or close its drawer.");
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
            SettingRow(panel,"Drawer icon","Symbols, libraries & installed packs",()=>{dialog.Dismiss();PickDrawerIcon(drawer);},DrawerIcons.All.Contains(drawer.Icon)?drawer.Icon:DrawerIcons.ForDrawer(drawer.Id));
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
        var options=apps.Where(a=>!a.PrivateProfile).OrderBy(a=>a.Label).ToArray();
        var selected=options.Select(a=>state.Apps[a.Id].DrawerId==drawer.Id).ToArray();
        Sheet(drawer.Name,(panel,dialog)=>
        {
            Paragraph("Choose the apps for this drawer.",panel);
            for(int i=0;i<options.Length;i++)
            {
                int index=i;var app=options[i];
                var check=new CheckBox(this){Text=app.Label+(state.Apps[app.Id].Hidden?" (hidden)":""),Checked=selected[i],Typeface=regular,TextSize=15};
                check.ButtonTintList=AccentTint();BindToggleHaptics(check);
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
        var hidden=apps.Where(a=>!a.PrivateProfile&&state.Apps[a.Id].Hidden).ToArray();
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
        Paragraph("Access is optional and used by the features you enable. App grayscale uses an accessibility service and a one-time display permission. File sharing uses a folder you select. Connection names may require nearby-device and location access; weather uses the city you enter. Work profiles and Android 15+ Private Space require Launcher as the default home. Android controls which profiles are accessible.");
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
    private void ShowGuide() => Sheet("Using Launcher",(panel,dialog)=>
    {
        Paragraph("Tap a drawer heading to expand it. Hold it to rename, reorder or choose its apps.",panel);
        Paragraph("Tap an app to open it. Hold an app to move, rename, pin, highlight, expand, hide or set a daily usage goal.",panel);
        Paragraph("Swipe right for Root; swipe left for Conversations. The menu inside search also opens Root, Apps and Notifications.",panel);
        Paragraph("Pull down within the top of the Tiles screen for Launcher’s quick panel. Tap the minimal status row to open it too. The screen-edge notification shade belongs to Android.",panel);
        Paragraph("Hold the search icon, or tap the three dots, for theme and focus controls. Focus gives app tiles a quiet outlined appearance.",panel);
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
        void Pick(){var intent=new Intent(Intent.ActionOpenDocument);intent.AddCategory(Intent.CategoryOpenable);intent.SetType("*/*");StartActivityForResult(intent,702);}
        if(state.Apps.Values.Any(r=>r.BiometricLocked))AuthenticateDevice("layout restore",Pick);else Pick();
    }
}
