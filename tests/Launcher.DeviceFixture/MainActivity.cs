using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.OS;

namespace Launcher.DeviceFixture;

// Explicit synthetic Android session/notification fixture. Never installed on the personal phone.
[Activity(Name="com.launcher.qa.MainActivity",Label="Music demo",MainLauncher=true,Exported=true)]
public sealed class MainActivity:Activity
{
    private static MediaSession? session;
    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        if(Intent?.GetBooleanExtra("open_player",false)==true)
        {
            var label=new Android.Widget.TextView(this){Text="Music demo\n\nOpen skies\nLauncher sample",TextSize=24};
            label.SetPadding(40,100,40,40);SetContentView(label);return;
        }
        session?.Release();session=new MediaSession(this,"Launcher regression fixture");
        session.SetSessionActivity(PendingIntent.GetActivity(this,0,new Intent(this,typeof(MainActivity)).PutExtra("open_player",true),PendingIntentFlags.Immutable));
        session.SetCallback(new Controls());Metadata("QA sample track");Playback(PlaybackStateCode.Playing);session.Active=true;
        var notifications=(NotificationManager)GetSystemService(NotificationService)!;
        notifications.CreateNotificationChannel(new NotificationChannel("qa","Launcher fixture",NotificationImportance.Low));
        notifications.Notify(1,new Notification.Builder(this,"qa").SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)!
            .SetContentTitle("Saved for later")!.SetContentText("Three new tracks in your listening list. Sample notification.")!.SetNumber(3)!.Build());
        StartActivity(new Intent().SetComponent(new ComponentName("com.launcher.home","com.launcher.home.MainActivity")));
    }
    private static void Metadata(string title)
    {
        var art=Android.Graphics.Bitmap.CreateBitmap(640,640,Android.Graphics.Bitmap.Config.Argb8888!)!;
        using var canvas=new Android.Graphics.Canvas(art);using var paint=new Android.Graphics.Paint();
        paint.SetShader(new Android.Graphics.LinearGradient(0,0,640,640,
            new[]{Android.Graphics.Color.Rgb(70,104,104).ToArgb(),Android.Graphics.Color.Rgb(185,153,86).ToArgb(),Android.Graphics.Color.Rgb(40,64,70).ToArgb()},null,Android.Graphics.Shader.TileMode.Clamp!));
        canvas.DrawRect(0,0,640,640,paint);paint.SetShader(null);paint.Color=Android.Graphics.Color.Argb(90,240,241,212);canvas.DrawCircle(400,210,140,paint);
        session!.SetMetadata(new MediaMetadata.Builder().PutString(MediaMetadata.MetadataKeyTitle,title=="QA sample track"?"Open skies":"Evening light")!
            .PutString(MediaMetadata.MetadataKeyArtist,"Launcher sample")!.PutBitmap(MediaMetadata.MetadataKeyAlbumArt,art)!.PutLong(MediaMetadata.MetadataKeyDuration,180000)!.Build());
    }
    private static void Playback(PlaybackStateCode code)=>session!.SetPlaybackState(new PlaybackState.Builder()
        .SetActions(PlaybackState.ActionPlay|PlaybackState.ActionPause|PlaybackState.ActionSkipToNext|PlaybackState.ActionSkipToPrevious)!
        .SetState(code,15000,code==PlaybackStateCode.Playing?1:0)!.Build());
    private sealed class Controls:MediaSession.Callback
    {
        public override void OnPlay()=>Playback(PlaybackStateCode.Playing);
        public override void OnPause()=>Playback(PlaybackStateCode.Paused);
        public override void OnSkipToNext()=>Metadata("QA second track");
        public override void OnSkipToPrevious()=>Metadata("QA sample track");
    }
}
