using Launcher.Core;

int checks=0;
void Check(bool result,string name) {checks++;if(!result)throw new Exception("FAIL: "+name);}
void Reject(Action action,string name) {bool rejected=false;try{action();}catch(Exception e)when(e is InvalidDataException or System.Text.Json.JsonException){rejected=true;}Check(rejected,name);}

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
Check(visible.Length==2 && visible.Any(a=>a.Id=="phone/main") && visible.Any(a=>a.Id=="chat/main"),"focus permits essentials and favorites");
Check(!AppOrganization.Visible(state,apps,"youtube").Any(),"search cannot bypass focus or hidden state");
state.Apps["chat/main"].Label="Family";
Check(AppOrganization.Visible(state,apps,"family").Count()==1,"custom labels are searchable");
Check(AppOrganization.Visible(state,apps,"telegram").Count()==1,"original labels remain searchable");
state.RemoveDrawer("messaging");
Check(state.Apps["chat/main"].DrawerId=="other","deleting drawer rehomes apps");
state.RemoveDrawer("other");Check(state.Drawers.Any(d=>d.Id=="other"),"fallback drawer cannot be removed");
state.Note="A note with \"quotes\", 漢字 and newlines\nnext line";
state.FocusCardVisible=false;
var restored=LauncherState.Deserialize(state.Serialize());
Check(restored.Note==state.Note && restored.Apps["chat/main"].Pinned,"backup preserves notes and customization");
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
Console.WriteLine($"PASS · {checks} core behavior checks");

sealed class FixtureHandler(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        =>Task.FromResult(response(request));
}
