using Android.App;
using Android.OS;
using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.Graphics;
using Android.Widget;
namespace Launcher.UiFixture;
[Activity(Name="com.launcher.uifixture.MainActivity",Label="Studio Player",MainLauncher=true,Exported=true)]
public class MainActivity:Activity
{
 static MediaSession? session;static bool playing=true;static long position=47000;static TextView? status;
 protected override void OnCreate(Bundle? savedInstanceState)
 {
  base.OnCreate(savedInstanceState);var panel=new LinearLayout(this){Orientation=Android.Widget.Orientation.Vertical};panel.SetPadding(28,200,28,28);panel.SetBackgroundColor(Color.Rgb(8,8,8));status=new TextView(this){Text="Studio Player opened",TextSize=24};panel.AddView(status);
  var start=new Button(this){Text="Start sample playback and messages"};start.Click+=(_,_)=>StartSample();panel.AddView(start);SetContentView(panel);
  if(Intent?.GetBooleanExtra("sample",false)==true)StartSample();
  if(Intent?.GetBooleanExtra("pin",false)==true)RequestSampleShortcut();
  if(Intent?.GetBooleanExtra("verify_apk",false)==true)VerifyMigration();
  if(Intent?.GetBooleanExtra("seed_calendar",false)==true)SeedCalendar();
  if(Intent?.GetBooleanExtra("cleanup_calendar",false)==true)CleanupCalendar();
  if(Intent?.GetBooleanExtra("pinned_open",false)==true)status!.Text="PINNED_ACTION_OPENED";
 }
 void RequestSampleShortcut()
 {
  var manager=(Android.Content.PM.ShortcutManager)GetSystemService(ShortcutService)!;
  var shortcut=new Android.Content.PM.ShortcutInfo.Builder(this,"qa-pinned-action").SetShortLabel("QA pinned action").SetIcon(Android.Graphics.Drawables.Icon.CreateWithResource(this,Android.Resource.Drawable.IcDialogInfo)).SetIntent(new Intent(this,typeof(MainActivity)).SetAction(Intent.ActionView).PutExtra("pinned_open",true)).Build();
  status!.Text="PIN_REQUESTED:"+manager.RequestPinShortcut(shortcut,null);
 }
 void VerifyMigration()
 {
  try
  {
   var flags=Android.Content.PM.PackageInfoFlags.SigningCertificates;var installed=PackageManager!.GetPackageInfo("com.launcher.home",flags);var incoming=PackageManager.GetPackageArchiveInfo(System.IO.Path.Combine(FilesDir!.AbsolutePath,"candidate.apk"),flags)!;
   string Hash(Android.Content.PM.Signature signature)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(signature.ToByteArray()!));
   bool accepted=incoming.PackageName=="com.launcher.home"&&Launcher.Core.SigningTrust.Accepts(installed!.SigningInfo!.GetApkContentsSigners()!.Select(Hash),incoming.SigningInfo!.GetApkContentsSigners()!.Select(Hash),incoming.SigningInfo.GetSigningCertificateHistory()!.Select(Hash));
   status!.Text="SIGNING_ACCEPTED:"+accepted+"\nINCOMING:"+incoming.VersionName;
  }catch(Exception e){status!.Text="SIGNING_ERROR:"+e.GetType().Name;}
 }
 Android.Net.Uri CalendarUri()=>Android.Provider.CalendarContract.Calendars.ContentUri!.BuildUpon()!.AppendQueryParameter("caller_is_syncadapter","true")!.AppendQueryParameter("account_name","launcher.qa")!.AppendQueryParameter("account_type","LOCAL")!.Build()!;
 void SeedCalendar()
 {
  try
  {
   CleanupCalendar();var values=new ContentValues();values.Put("account_name","launcher.qa");values.Put("account_type","LOCAL");values.Put("name","Launcher QA");values.Put("calendar_displayName","Launcher QA");values.Put("calendar_color",unchecked((int)0xFFE4FF00));values.Put("calendar_access_level",700);values.Put("ownerAccount","launcher.qa");values.Put("sync_events",1);values.Put("visible",1);
   var calendar=ContentResolver!.Insert(CalendarUri(),values)!;long id=ContentUris.ParseId(calendar);GetPreferences(FileCreationMode.Private)!.Edit()!.PutLong("qa-calendar",id)!.Apply();
   var entry=new ContentValues();entry.Put("calendar_id",id);entry.Put("title","Launcher QA planning");entry.Put("eventLocation","Sample room");entry.Put("dtstart",DateTimeOffset.Now.AddHours(1).ToUnixTimeMilliseconds());entry.Put("dtend",DateTimeOffset.Now.AddHours(2).ToUnixTimeMilliseconds());entry.Put("eventTimezone",Java.Util.TimeZone.Default!.ID);ContentResolver.Insert(Android.Provider.CalendarContract.Events.ContentUri!,entry);status!.Text="CALENDAR_SEEDED";
  }catch(Exception e){status!.Text="CALENDAR_ERROR:"+e.Message;}
 }
 void CleanupCalendar()
 {
  long id=GetPreferences(FileCreationMode.Private)!.GetLong("qa-calendar",-1);if(id<0)return;
  ContentResolver!.Delete(CalendarUri(),"_id=?",[id.ToString()]);GetPreferences(FileCreationMode.Private)!.Edit()!.Remove("qa-calendar")!.Apply();status!.Text="CALENDAR_CLEANED";
 }
 static void State(){session?.SetPlaybackState(new PlaybackState.Builder().SetActions(PlaybackState.ActionPlay|PlaybackState.ActionPause|PlaybackState.ActionSkipToNext|PlaybackState.ActionSkipToPrevious|PlaybackState.ActionSeekTo)!.SetState(playing?PlaybackStateCode.Playing:PlaybackStateCode.Paused,position,playing?1:0,SystemClock.ElapsedRealtime())!.Build());}
 void StartSample()
 {
  if(session is null){session=new MediaSession(this,"Launcher UI sample");session.SetCallback(new Controls());}
  var intent=new Intent(this,typeof(MainActivity));var open=PendingIntent.GetActivity(this,0,intent,PendingIntentFlags.UpdateCurrent|PendingIntentFlags.Immutable)!;session.SetSessionActivity(open);
  var art=Bitmap.CreateBitmap(500,500,Bitmap.Config.Argb8888!)!;using(var canvas=new Canvas(art)){canvas.DrawColor(Color.Rgb(41,65,70));using var p=new Paint(PaintFlags.AntiAlias){Color=Color.Rgb(174,194,120)};canvas.DrawCircle(350,130,200,p);p.Color=Color.Rgb(98,124,117);canvas.DrawCircle(100,420,240,p);}
  session.SetMetadata(new MediaMetadata.Builder().PutString(MediaMetadata.MetadataKeyTitle,"Evening light")!.PutString(MediaMetadata.MetadataKeyArtist,"Studio sessions")!.PutLong(MediaMetadata.MetadataKeyDuration,202000)!.PutBitmap(MediaMetadata.MetadataKeyAlbumArt,art)!.Build());session.Active=true;State();
  var manager=(NotificationManager)GetSystemService(NotificationService)!;manager.CreateNotificationChannel(new NotificationChannel("sample","UI sample",NotificationImportance.Default));
  foreach(var (id,title,text) in new[]{(1,"Alex","Coffee after work? I found a place near the park."),(2,"Sam","The photos from this weekend are ready.")})
  {
   var replyIntent=PendingIntent.GetBroadcast(this,42,new Intent(this,typeof(ReplyReceiver)),PendingIntentFlags.UpdateCurrent|PendingIntentFlags.Mutable)!;
   var input=new RemoteInput.Builder("reply").SetLabel("Reply").Build();var reply=new Notification.Action.Builder((Android.Graphics.Drawables.Icon?)null,"Reply",replyIntent).AddRemoteInput(input).Build();
   manager.Notify(id,new Notification.Builder(this,"sample").SetSmallIcon(Android.Resource.Drawable.IcDialogEmail).SetContentTitle(title).SetContentText(text).SetCategory(Notification.CategoryMessage).SetContentIntent(open).SetAutoCancel(true).AddAction(reply).Build());
  }
  manager.Notify(3,new Notification.Builder(this,"sample").SetSmallIcon(Android.Resource.Drawable.IcDialogInfo).SetContentTitle("Sample background task").SetContentText("This notification stays while the sample task is running.").SetOngoing(true).Build());
  status!.Text="Studio Player opened · sample session ready";
 }
 sealed class Controls:MediaSession.Callback
 {
  public override void OnPlay(){playing=true;State();}
  public override void OnPause(){playing=false;State();}
  public override void OnSkipToNext(){position=0;State();}
  public override void OnSkipToPrevious(){position=0;State();}
  public override void OnSeekTo(long pos){position=pos;State();}
 }
}

[BroadcastReceiver(Name="com.launcher.uifixture.ReplyReceiver",Exported=false)]
public sealed class ReplyReceiver:BroadcastReceiver
{
 public override void OnReceive(Context? context,Intent? intent)
 {
  if(context is null||intent is null)return;
  string reply=RemoteInput.GetResultsFromIntent(intent)?.GetCharSequence("reply")?.ToString()??"";
  var manager=(NotificationManager)context.GetSystemService(Context.NotificationService)!;
  manager.Notify(8,new Notification.Builder(context,"sample").SetSmallIcon(Android.Resource.Drawable.IcDialogEmail).SetContentTitle("Reply received").SetContentText(reply).Build());
 }
}
