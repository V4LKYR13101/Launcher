using Android.App;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Views;
using Android.Widget;
using LauncherApp.UI;

namespace LauncherApp;

public partial class MainActivity
{
    private SeekBar? mediaProgress;
    private TextView? mediaTime;
    private long mediaDuration;
    private bool mediaSeeking;
    private void MediaChanged()=>RunOnUiThread(()=>
    {
        if(!resumed||page!="Tiles"||searching)return;
        int y=scroll.ScrollY;RenderBody();var target=scroll;target.Post(()=>target.ScrollTo(0,y));
    });
    private static string TrackTime(long milliseconds)
    {var span=TimeSpan.FromMilliseconds(Math.Max(0,milliseconds));return $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";}
    private void AddMediaCard()
    {
        var controller=media.Controller;
        if(!state.ShowMediaCard||controller is null||controller.PlaybackState?.State is not (PlaybackStateCode.Playing or PlaybackStateCode.Paused or PlaybackStateCode.Buffering or PlaybackStateCode.Connecting))return;
        var metadata=controller.Metadata;
        string app=apps.FirstOrDefault(a=>a.PackageName==controller.PackageName)?.Label??controller.PackageName??"Media";
        string title=metadata?.GetString(MediaMetadata.MetadataKeyTitle)??app;
        string artist=metadata?.GetString(MediaMetadata.MetadataKeyArtist)??metadata?.GetString(MediaMetadata.MetadataKeyAlbumArtist)??"";
        var card=new FrameLayout(this){Background=Background(palette.Card,24),ClipToOutline=true};
        void OpenPlayer()=>WithAppUnlock(controller.PackageName!,OpenPlayerUnlocked);
        void OpenPlayerUnlocked()
        {
            try
            {
                var intent=PackageManager?.GetLaunchIntentForPackage(controller.PackageName!);
                if(intent is not null){intent.AddFlags(Android.Content.ActivityFlags.NewTask|Android.Content.ActivityFlags.ResetTaskIfNeeded);OpenIntent(intent);return;}
                var appEntry=apps.FirstOrDefault(a=>a.PackageName==controller.PackageName);
                if(appEntry is not null){StartAppDirect(appEntry);return;}
                if(controller.SessionActivity is {} activity){activity.Send();return;}
                Toast("This player does not expose an app screen.");
            }
            catch(PendingIntent.CanceledException)
            {var intent=PackageManager?.GetLaunchIntentForPackage(controller.PackageName!);if(intent is not null)OpenIntent(intent);}
        }
        card.Click+=(_,_)=>OpenPlayer();card.ContentDescription="Now playing · open "+app;
        var art=metadata?.GetBitmap(MediaMetadata.MetadataKeyAlbumArt)??metadata?.GetBitmap(MediaMetadata.MetadataKeyArt)??metadata?.GetBitmap(MediaMetadata.MetadataKeyDisplayIcon);
        if(art is not null)
        {
            var cover=new ImageView(this){Alpha=.55f};cover.SetScaleType(ImageView.ScaleType.CenterCrop);cover.SetImageBitmap(art);card.AddView(cover,new FrameLayout.LayoutParams(-1,-1));
        }
        var shade=new View(this){Background=new GradientDrawable(GradientDrawable.Orientation.TlBr,[new Color(0,0,0,180).ToArgb(),new Color(0,0,0,190).ToArgb()])};
        card.AddView(shade,new FrameLayout.LayoutParams(-1,-1));
        var content=Stack();content.SetPadding(Dp(14),Dp(8),Dp(14),Dp(7));card.AddView(content,new FrameLayout.LayoutParams(-1,-1));
        var transport=controller.GetTransportControls();
        var actions=controller.PlaybackState?.CustomActions??[];
        PlaybackState.CustomAction? Custom(params string[] names)=>actions.FirstOrDefault(a=>names.Any(n=>((a.Name?.ToString()??"")+" "+a.Action).Contains(n,StringComparison.OrdinalIgnoreCase)));
        View Control(string glyph,string label,Action? action,bool circle=false,int size=36)
        {
            var button=new FrameLayout(this){ContentDescription=action is null?label+" unavailable for this player":label,Alpha=action is null?.32f:1,Clickable=true};
            if(circle)button.Background=Background(new Color(255,255,255,24),30);
            button.AddView(new GlyphView(this,glyph,Color.White),new FrameLayout.LayoutParams(Dp(23),Dp(23),GravityFlags.Center));
            button.LayoutParameters=new LinearLayout.LayoutParams(Dp(size),Dp(size));
            button.Click+=(_,_)=>{if(action is null)return;try{action();}catch(Java.Lang.SecurityException){Toast("Playback control is unavailable.");}};
            return button;
        }
        var route=Stack(true);route.SetGravity(GravityFlags.CenterVertical);
        var output=Text(connections.AudioDeviceName()??app,10,true,new Color(230,230,230));output.SetSingleLine(true);output.Ellipsize=Android.Text.TextUtils.TruncateAt.End;
        route.AddView(output,new LinearLayout.LayoutParams(0,Dp(24),1));
        var repeat=Custom("repeat");route.AddView(Control("repeat","Repeat",repeat is null?null:()=>transport?.SendCustomAction(repeat,null),size:24));content.AddView(route,Match(Dp(24)));
        var track=Text(title+(string.IsNullOrEmpty(artist)?"":"  ·  "+artist),13,true,Color.White);track.SetSingleLine(true);track.Ellipsize=Android.Text.TextUtils.TruncateAt.Marquee;track.Selected=true;
        content.AddView(track,Match(Dp(26)));
        track.Click+=(_,_)=>OpenPlayer();
        mediaDuration=metadata?.GetLong(MediaMetadata.MetadataKeyDuration)??0;
        mediaProgress=new SeekBar(this){Max=1000,ProgressTintList=ColorStateList.ValueOf(palette.Accent),ProgressBackgroundTintList=ColorStateList.ValueOf(new Color(255,255,255,80)),SplitTrack=false};
        mediaProgress.SetPadding(Dp(1),0,Dp(1),0);
        var thumb=Background(palette.Accent,12,Color.White);thumb.SetSize(Dp(12),Dp(12));mediaProgress.SetThumb(thumb);
        mediaProgress.StartTrackingTouch+=(_,_)=>mediaSeeking=true;
        mediaProgress.StopTrackingTouch+=(_,_)=>{if(mediaDuration>0)transport?.SeekTo(mediaDuration*mediaProgress.Progress/1000);mediaSeeking=false;};
        mediaProgress.ProgressChanged+=(_,e)=>{if(e.FromUser&&mediaTime is not null)mediaTime.Text=TrackTime(mediaDuration*e.Progress/1000);};
        content.AddView(mediaProgress,Match(Dp(19)));
        var times=Stack(true);mediaTime=Text("00:00",9,color:new Color(190,190,190));times.AddView(mediaTime,new LinearLayout.LayoutParams(0,-2,1));
        times.AddView(Text(mediaDuration>0?TrackTime(mediaDuration):"Live",9,color:new Color(190,190,190)));content.AddView(times,Match(Dp(16)));
        var controls=Stack(true);controls.SetGravity(GravityFlags.Center);
        var previous=new LinearLayout.LayoutParams(Dp(38),Dp(38));previous.SetMargins(Dp(9),0,Dp(9),0);
        controls.AddView(Control("previous","Previous track",()=>transport?.SkipToPrevious(),true),previous);
        controls.AddView(Control(media.Playing?"pause":"play",media.Playing?"Pause playback":"Resume playback",()=>{if(media.Playing)transport?.Pause();else transport?.Play();},true),new LinearLayout.LayoutParams(Dp(40),Dp(40)));
        var next=new LinearLayout.LayoutParams(Dp(38),Dp(38));next.SetMargins(Dp(9),0,Dp(9),0);
        controls.AddView(Control("next","Next track",()=>transport?.SkipToNext(),true),next);
        content.AddView(controls,Match(Dp(42)));UpdateMediaProgress();
        var lp=Match(Dp(146));lp.SetMargins(0,0,0,Dp(16));body.AddView(card,lp);
    }
    private void UpdateMediaProgress()
    {
        if(mediaProgress is null||mediaSeeking)return;
        var playback=media.Controller?.PlaybackState;double position=playback?.Position??0;
        if(media.Playing&&playback is not null)position+=Math.Max(0,SystemClock.ElapsedRealtime()-playback.LastPositionUpdateTime)*playback.PlaybackSpeed;
        if(mediaDuration>0)position=Math.Min(position,mediaDuration);
        mediaProgress.Enabled=mediaDuration>0;mediaProgress.Progress=mediaDuration>0?(int)Math.Clamp(position/mediaDuration*1000,0,1000):0;
        string elapsed=mediaDuration>0?TrackTime((long)position):"";
        if(mediaTime is not null&&mediaTime.Text!=elapsed)mediaTime.Text=elapsed;
    }
}
