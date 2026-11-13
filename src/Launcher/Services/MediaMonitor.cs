using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using MediaController=Android.Media.Session.MediaController;

namespace LauncherApp.Services;

public sealed class MediaMonitor(Context context,Action changed) : Java.Lang.Object,MediaSessionManager.IOnActiveSessionsChangedListener
{
    private readonly MediaSessionManager? manager=(MediaSessionManager?)context.GetSystemService(Context.MediaSessionService);
    private readonly Handler handler=new(Looper.MainLooper!);
    private MediaCallback? callback;
    private bool listening;
    public MediaController? Controller {get;private set;}
    public bool Playing=>Controller?.PlaybackState?.State is PlaybackStateCode.Playing or PlaybackStateCode.Buffering or PlaybackStateCode.Connecting;
    public void Start()
    {
        if(listening || !ConversationService.Enabled(context))return;
        try
        {
            var component=new ComponentName(context,Java.Lang.Class.FromType(typeof(ConversationService)));
            manager?.AddOnActiveSessionsChangedListener(this,component,handler);listening=true;
            Select(manager?.GetActiveSessions(component));
        }
        catch(Java.Lang.SecurityException){Stop();}
    }
    public void Stop()
    {
        if(listening){try{manager?.RemoveOnActiveSessionsChangedListener(this);}catch(Java.Lang.SecurityException){}listening=false;}
        if(callback is not null)Controller?.UnregisterCallback(callback);
        callback=null;Controller=null;
    }
    public void OnActiveSessionsChanged(IList<MediaController>? controllers)=>Select(controllers);
    private void Select(IList<MediaController>? controllers)
    {
        if(callback is not null)Controller?.UnregisterCallback(callback);
        Controller=controllers?.OrderByDescending(c=>c.PlaybackState?.State is PlaybackStateCode.Playing or PlaybackStateCode.Buffering)
            .FirstOrDefault(c=>c.PlaybackState?.State is PlaybackStateCode.Playing or PlaybackStateCode.Buffering or PlaybackStateCode.Paused or PlaybackStateCode.Connecting);
        callback=Controller is null?null:new MediaCallback(changed);
        if(callback is not null)Controller?.RegisterCallback(callback,handler);
        changed();
    }
    private sealed class MediaCallback(Action changed) : MediaController.Callback
    {
        private PlaybackStateCode? previous;
        public override void OnMetadataChanged(MediaMetadata? metadata)=>changed();
        public override void OnPlaybackStateChanged(PlaybackState? state)
        {if(previous==state?.State)return;previous=state?.State;changed();}
        public override void OnSessionDestroyed()=>changed();
    }
}
