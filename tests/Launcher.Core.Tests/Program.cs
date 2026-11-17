using Launcher.Core;

int checks=0;
void Check(bool result,string name) {checks++;if(!result)throw new Exception("FAIL: "+name);}
void Reject(Action action,string name) {bool rejected=false;try{action();}catch(Exception e)when(e is InvalidDataException or System.Text.Json.JsonException){rejected=true;}Check(rejected,name);}

Check(LauncherState.Deserialize("{}").HapticFeedback=="Subtle","older layouts receive subtle haptics");
foreach(string mode in new[]{"Off","Subtle","Crisp"})
{
    var hapticState=new LauncherState{HapticFeedback=mode};
    Check(LauncherState.Deserialize(hapticState.Serialize()).HapticFeedback==mode,"haptic preference survives backup: "+mode);
}
Check(LauncherState.Deserialize("{\"HapticFeedback\":\"invalid\"}").HapticFeedback=="Subtle","invalid haptic preference normalizes");
Check(!LauncherState.Deserialize("{}").SystemPanelEnabled&&!LauncherState.Deserialize("{}").SystemPanelStatus,"system panel and status strip remain opt-in for existing layouts");
var panelState=LauncherState.Deserialize("{\"SystemPanelEnabled\":true,\"SystemPanelOpacity\":0,\"SystemPanelOffset\":0,\"SystemPanelSide\":\"invalid\",\"SystemPanelTheme\":\"invalid\",\"SystemPanelControls\":[\"Sound\",\"Sound\",\"invalid\"]}");
Check(panelState.SystemPanelOpacity==90&&panelState.SystemPanelOffset==16&&panelState.SystemPanelSide=="Right"&&panelState.SystemPanelTheme=="Follow Launcher","imported panel appearance is bounded");
Check(panelState.SystemPanelControls.Count==6&&panelState.SystemPanelControls[0]=="Sound","panel controls retain order while repairing duplicates and missing controls");
Check(LauncherState.Deserialize(panelState.Serialize()).SystemPanelEnabled,"panel preference survives backup");
Check(LauncherState.Deserialize("{}").TileIconScale==100,"existing layouts keep their default icon size");
var iconState=new LauncherState{TileIconScale=135};
Check(LauncherState.Deserialize(iconState.Serialize()).TileIconScale==135,"icon size survives layout backup");
Check(LauncherState.Deserialize("{\"TileIconScale\":900}").TileIconScale==150&&LauncherState.Deserialize("{\"TileIconScale\":1}").TileIconScale==60,"imported icon size is bounded");
var state=new LauncherState();
AppEntry[] apps=[new("phone/main","com.android.dialer","main","Phone"),new("chat/main","org.telegram.messenger","main","Telegram"),new("video/main","com.youtube","main","YouTube"),new("video/second","com.youtube","second","YouTube Studio")];
AppOrganization.Reconcile(state,apps);
Check(state.Apps.Count==4,"separate launcher components are retained");
Check(AppOrganization.GuessDrawer(new("a/b","com.jbl.headphones","b","JBL Headphones"))!="essentials","headphones aren't mistaken for Phone");
Check(AppOrganization.GuessDrawer(new("a/b","com.chromecast.home","b","Home"))!="essentials","Chromecast isn't mistaken for Chrome");
state.Apps["chat/main"].Pinned=true;
state.Apps["video/main"].Hidden=true;
state.Focus=true;
var visible=AppOrganization.Visible(state,apps).ToArray();
Check(visible.Length==3 && visible.Any(a=>a.Id=="phone/main") && visible.Any(a=>a.Id=="chat/main"),"outlined focus keeps non-hidden apps available");
Check(AppOrganization.Visible(state,apps,"youtube").All(a=>a.Id!="video/main"),"outlined focus still respects hidden apps in search");
state.Apps["chat/main"].Label="Family";
Check(AppOrganization.Visible(state,apps,"family").Count()==1,"custom labels are searchable");
Check(AppOrganization.Visible(state,apps,"telegram").Count()==1,"original labels remain searchable");
state.RemoveDrawer("messaging");
Check(state.Apps["chat/main"].DrawerId=="other","deleting drawer rehomes apps");
state.RemoveDrawer("other");Check(state.Drawers.Any(d=>d.Id=="other"),"fallback drawer cannot be removed");
state.Note="A note with \"quotes\", 漢字 and newlines\nnext line";
state.FocusCardVisible=false;
var restored=LauncherState.Deserialize(state.Serialize());
Check(restored.Note=="" && restored.Notes.Any(n=>n.Text==state.Note) && restored.Apps["chat/main"].Pinned,"backup preserves legacy notes through migration and customization");
Check(!restored.FocusCardVisible,"focus card visibility survives backup and restore");
restored.Columns=999;restored.CornerRadius=-1;restored.Apps["chat/main"].DrawerId="missing";restored.Theme="invalid";restored.Accent="bad";restored.Normalize();
Check(restored.Columns==6 && restored.CornerRadius==0 && restored.Theme=="Dark" && restored.Accent=="#E4FF00","import sanitizes invalid settings");
Check(restored.Apps["chat/main"].DrawerId=="other","import repairs missing drawer references");
var damaged=LauncherState.Deserialize("{\"Drawers\":null,\"Apps\":null,\"Widgets\":null,\"Note\":null,\"CollapsedCards\":null}");
Check(damaged.Drawers.Count>0 && damaged.Apps.Count==0 && damaged.Note=="","null import collections recovered");
Reject(()=>LauncherState.Deserialize("{\"Version\":999}"),"future backup version rejected");
Reject(()=>LauncherState.Deserialize("null"),"null backup rejected");
Reject(()=>LauncherState.Deserialize(new string(' ',2_000_001)),"oversized backup rejected");
var before=state.Apps["chat/main"].Label;AppOrganization.Reconcile(state,apps);
Check(state.Apps["chat/main"].Label==before,"refresh preserves user changes");
Check(AppOrganization.FormatDuration(90*60_000)=="1h 30m","usage time format");
Check(AppOrganization.FormatDuration(-1)=="0m","negative usage clamped");
foreach(var (hex,dark) in new[]{("#E4FF00",true),("#222222",false),("#808080",true),("#FF0000",true),("#0000FF",false),("#00FF00",true)})
    Check(ColorContrast.UseDarkText(hex)==dark,"accent contrast "+hex);
var badges=NotificationBadges.Count([new("summary","mail","g",5,true,true),new("one","mail","g",1,false,true),new("two","mail","g",2,false,true),new("music","player","p",1,false,false),new("onlysummary","chat","c",4,true,true)]);
Check(badges["mail"]==3,"badges don't double-count group summaries");
Check(badges["chat"]==4,"summary-only notifications supply a badge count");
Check(!badges.ContainsKey("player"),"ongoing services don't become unread badges");
Check(NotificationBadges.Label(120)=="99+","large badge counts stay compact");
state.Drawers[0].Icon="diamond";state.StatusPlacement="Safe";state.ConversationExcludedApps=["com.example.mail"];state.Weather.City="Cape Town";state.Weather.Latitude=-33.9;state.Weather.Longitude=18.4;
var customized=LauncherState.Deserialize(state.Serialize());
Check(customized.Drawers[0].Icon=="diamond","drawer icon selection survives restart");
Check(customized.StatusPlacement=="Safe","status placement preference persists");
Check(customized.ConversationExcludedApps.SequenceEqual(new[]{"com.example.mail"}),"conversation source filters persist");
Check(customized.Weather.City=="Cape Town"&&customized.Weather.Latitude==-33.9,"weather location preference persists");
Check(WeatherCondition.Label(95)=="Thunderstorms"&&WeatherCondition.Glyph(61)=="rain","weather conditions use actual WMO codes");
foreach(var (expression,expected) in new[]{("(24 + 6) × 3","90"),("2+3*4","14"),("-5 + 10","5"),("200 * 15%","30"),("1/0","Check expression"),("2+","Check expression"),("System.IO.File.Delete()","Check expression"),("2(3)","Check expression"),(".5 + .25","0.75")})
    Check(Calculator.Evaluate(expression)==expected,"calculator: "+expression);
var updateBytes=System.Text.Encoding.UTF8.GetBytes("verified fixture APK bytes");
var release=new AppRelease("1.0.1",2,"https://kevlog.co.za/downloads/projects/launcher/test/",
    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(updateBytes)),updateBytes.Length);
Check(AppUpdates.Validate(release).Host=="kevlog.co.za","update accepts the Launcher release host");
foreach(var bad in new[]{release with{Url="http://kevlog.co.za/downloads/projects/launcher/test/"},
    release with{Url="https://evil.example/downloads/projects/launcher/test/"},
    release with{Url="https://kevlog.co.za/downloads/projects/another/test/"},
    release with{Bytes=0},release with{Sha256="bad"},release with{Build=-1}})
    Reject(()=>AppUpdates.Validate(bad),"invalid update manifest rejected");
using(var http=new HttpClient(new FixtureHandler(_=>new(System.Net.HttpStatusCode.OK){Content=new StringContent(
    System.Text.Json.JsonSerializer.Serialize(new {android=release}))})))
    Check((await AppUpdates.CheckAsync(client:http)).Build==2,"lowercase live manifest deserializes with source generation");
var updatePath=Path.Combine(Path.GetTempPath(),"launcher-update-test-"+Guid.NewGuid().ToString("N")+".apk");
try
{
    using var http=new HttpClient(new FixtureHandler(_=>new(System.Net.HttpStatusCode.OK){Content=new ByteArrayContent(updateBytes)}));
    int progress=0;await AppUpdates.DownloadAsync(release,updatePath,p=>progress=p,client:http);
    Check(File.ReadAllBytes(updatePath).SequenceEqual(updateBytes)&&progress==100,"verified download finishes at 100 percent");
    foreach(var invalid in new[]{release with{Bytes=updateBytes.Length+1},release with{Bytes=updateBytes.Length-1},
        release with{Sha256=new string('0',64)}})
    {
        bool failed=false;
        try{await AppUpdates.DownloadAsync(invalid,updatePath,_=>{},client:http);}catch(InvalidDataException){failed=true;}
        Check(failed&&!File.Exists(updatePath+".part")&&File.ReadAllBytes(updatePath).SequenceEqual(updateBytes),
            "truncated, oversized or corrupt update cannot replace a verified file");
    }
}
finally{File.Delete(updatePath);File.Delete(updatePath+".part");}
foreach(string content in new[]{new string('漢',20000),string.Concat(Enumerable.Repeat("🌍",10000))})
{
    var collection=new LauncherState{Notes=Enumerable.Range(0,200).Select(i=>new WorkspaceNote{Title="Note "+i,Text=content}).ToList()};collection.Normalize();
    var loaded=LauncherState.Deserialize(collection.Serialize());Check(loaded.Notes.Count==200&&loaded.Notes.All(n=>n.Text==content),"maximum Unicode note collection round-trips");
}
var originalLayout=new LauncherState{Theme="Light",Notes=[new(){Title="Keep me",Text="Original"}],SystemPanelEnabled=true,KeepMessageHistory=true,Widgets=[new(){Id=17}]};
var incomingLayout=new LauncherState{Theme="Sun",Notes=[new(){Title="Imported",Text="Backup"}],SystemPanelEnabled=false,Widgets=[new(){Id=99}]};
var appearanceOnly=BackupRestore.Select(originalLayout,incomingLayout,true,false,false,false);
Check(appearanceOnly.Theme=="Sun"&&appearanceOnly.Notes.Single().Text=="Original"&&appearanceOnly.Widgets.Single().Id==17,"selective appearance restore keeps notes and device widget bindings");
Check(appearanceOnly.SystemPanelEnabled&&appearanceOnly.KeepMessageHistory,"restore preserves local feature consent");
var notesOnly=BackupRestore.Select(originalLayout,incomingLayout,false,false,true,false);
Check(notesOnly.Theme=="Light"&&notesOnly.Notes.Single().Text=="Backup","notes-only restore keeps appearance");
foreach(string invalid in new[]{"{}","null","{\"current\":{},\"daily\":{},\"hourly\":{}}","{\"current\":0,\"daily\":[],\"hourly\":{}}"})
{
    using var document=System.Text.Json.JsonDocument.Parse(invalid);Reject(()=>WeatherParser.Parse(document.RootElement),"incomplete forecast is a recoverable data error");
}
using(var document=System.Text.Json.JsonDocument.Parse("""{"current":{"temperature_2m":21,"weather_code":0,"is_day":1},"hourly":{"time":["2026-11-15T12:00","2026-11-15T13:00"],"temperature_2m":[21],"weather_code":[0,1]},"daily":{"time":["2026-11-15"],"temperature_2m_max":[25],"temperature_2m_min":[15],"weather_code":[0]},"utc_offset_seconds":7200}"""))
{
    var forecast=WeatherParser.Parse(document.RootElement);Check(forecast.Temperature==21&&forecast.Hours.Count==1&&forecast.Days.Count==1,"forecast parsing bounds mismatched provider arrays");
}
Check(LauncherState.Deserialize("{}").HideProtectedPreviews&&LauncherState.Deserialize("{}").ShowHomeNavigation,"older layouts receive privacy and visible navigation defaults");
Check(SigningTrust.Accepts([SigningTrust.LegacyCertificate],[SigningTrust.LegacyCertificate],[]),"bridge update retains current signing identity");
Check(SigningTrust.Accepts([SigningTrust.LegacyCertificate],[SigningTrust.ProductionCertificate],[SigningTrust.LegacyCertificate,SigningTrust.ProductionCertificate]),"verified production signer rotation is accepted");
Check(!SigningTrust.Accepts([SigningTrust.LegacyCertificate],[SigningTrust.ProductionCertificate],[]),"new signer without verified lineage is rejected");
Check(!SigningTrust.Accepts([SigningTrust.ProductionCertificate],[SigningTrust.LegacyCertificate],[SigningTrust.LegacyCertificate,SigningTrust.ProductionCertificate]),"signer downgrade is rejected");
Check(!SigningTrust.Accepts([SigningTrust.LegacyCertificate],[new string('A',64)],[SigningTrust.LegacyCertificate,new string('A',64)]),"untrusted rotation is rejected");
var migrationRelease=release with{Version="1.5.1",Build=27};
using(var http=new HttpClient(new FixtureHandler(_=>new(System.Net.HttpStatusCode.OK){Content=new StringContent(System.Text.Json.JsonSerializer.Serialize(new{android=release,androidProduction=migrationRelease}))})))
    Check((await AppUpdates.CheckAsync(client:http)).Build==27,"migration-aware updater selects production while retaining a legacy bridge manifest");
var checklistText="[ ] Pack passport\n[x] Charge phone\n書類";
Check(Checklist.Read(checklistText).Length==3&&Checklist.Read(Checklist.Toggle(checklistText,0,true))[0].Done,"checklists toggle one item and preserve Unicode items");
Check(Checklist.Read(Checklist.Toggle(checklistText,0,true))[1].Done,"checklist toggles preserve other completion states");
var searchable=new LauncherState{Notes=[new(){Title="Trip",Text="Remember the airport shuttle"}],Tickets=[new(){Title="Flight",Reference="ABC123"}],SavedArticles=[new(){Title="Garden ideas",Url="https://example.com/garden"}],Reminders=[new(){Title="Collect passport"}]};
Check(LocalSearch.Find(searchable,"airport shuttle").Single().Kind=="Note","local search finds note content using all terms");
Check(LocalSearch.Find(searchable,"ABC123").Single().Kind=="Journey"&&LocalSearch.Find(searchable,"garden").Single().Kind=="Saved story"&&LocalSearch.Find(searchable,"passport").Single().Kind=="Reminder","local search includes journeys, saved stories and reminders");
Check(LocalSearch.Find(searchable,"unrelated").Length==0,"local search does not return unrelated content");
var ordered=new LauncherState{RootOrder=Enumerable.Range(0,250).Select(i=>"note:"+i).Prepend("overview").ToList(),RootHidden=["overview"]};
var orderedCopy=LauncherState.Deserialize(ordered.Serialize());Check(orderedCopy.RootOrder.Count==251&&orderedCopy.RootHidden.Contains("overview"),"large card orders and removable overview survive reload");
var navigation=new LauncherState{HomeNavigationPages=["Root","Root","unknown","Conversations"]};navigation.Normalize();
Check(navigation.HomeNavigationPages.SequenceEqual(new[]{"Root","Conversations"}),"page buttons reject unknown destinations and duplicate entries");
navigation.HomeNavigationPages=[];Check(LauncherState.Deserialize(navigation.Serialize()).HomeNavigationPages.Count==0,"all individual page buttons can remain hidden after reload");
Check(ColorContrast.ForDarkSurface("#000000")!="#000000"&&ColorContrast.ForDarkSurface("#123456")!="#123456","dark custom accents remain visible on dark surfaces");
Check(ColorContrast.ForDarkSurface("#9BCBFF")=="#9BCBFF"&&ColorContrast.ForDarkSurface("#E4FF00")=="#E4FF00","readable dark-theme accents retain the chosen colour");
var delivered=new LauncherState{Timers=[new(){Id="timer",EndsAt=0,RemainingMs=0,LastAlertEndsAt=100}],Reminders=[new(){Id="repeat",DueAt=200,LastAlertDueAt=100,RepeatDays=1},new(){Id="once",DueAt=100,LastAlertDueAt=100,Completed=true}]};
var staleAlerts=new LauncherState{Theme="Light",Timers=[new(){Id="timer",EndsAt=100,RemainingMs=50}],Reminders=[new(){Id="repeat",DueAt=100},new(){Id="once",DueAt=100}]};WorkspaceReconciliation.PreserveDeliveredAlerts(staleAlerts,delivered);
Check(staleAlerts.Timers[0].EndsAt==0&&staleAlerts.Timers[0].RemainingMs==0&&staleAlerts.Theme=="Light","a stale screen save cannot revive a delivered timer or overwrite user preferences");
Check(staleAlerts.Reminders[0].DueAt==200&&!staleAlerts.Reminders[0].Completed,"stale saves retain the next occurrence of a repeating reminder");
Check(staleAlerts.Reminders[1].Completed,"stale saves retain completion of a one-shot reminder");
staleAlerts.Timers[0].EndsAt=300;WorkspaceReconciliation.PreserveDeliveredAlerts(staleAlerts,delivered);Check(staleAlerts.Timers[0].EndsAt==300,"an explicitly restarted timer keeps its new deadline");
staleAlerts.Timers=[];staleAlerts.Reminders=[];WorkspaceReconciliation.PreserveDeliveredAlerts(staleAlerts,delivered);Check(staleAlerts.Timers.Count==0&&staleAlerts.Reminders.Count==0,"alert reconciliation never restores deleted items");
Console.WriteLine($"PASS · {checks} core behavior checks");

sealed class FixtureHandler(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        =>Task.FromResult(response(request));
}
