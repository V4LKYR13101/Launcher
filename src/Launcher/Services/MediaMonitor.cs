using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using MediaController=Android.Media.Session.MediaController;
namespace LauncherApp.Services;

public sealed class MediaMonitor(Context context,Action changed):Java.Lang.Object,MediaSessionManager.IOnActiveSessionsChangedListener
{
    private readonly MediaSessionManager? manager=(MediaSessionManager?)context.GetSystemService(Context.MediaSessionService);
    private readonly Handler handler=new(Looper.MainLooper!);
    private readonly List<(MediaController Controller,MediaCallback Callback)> sessions=[];
    private bool listening;
    private string selectedPackage="";
    public MediaController[] Candidates=>sessions.Select(s=>s.Controller).Where(c=>c.PlaybackState?.State is PlaybackStateCode.Playing or PlaybackStateCode.Buffering or PlaybackStateCode.Paused or PlaybackStateCode.Connecting).ToArray();
    public void SelectPackage(string package){selectedPackage=package;Select();}
    public MediaController? Controller {get;private set;}
    public bool Playing=>Controller?.PlaybackState?.State is PlaybackStateCode.Playing or PlaybackStateCode.Buffering or PlaybackStateCode.Connecting;
    public void Start()
    {
        if(!ConversationService.Enabled(context))return;
        try
        {
            var component=new ComponentName(context,Java.Lang.Class.FromType(typeof(ConversationService)));
            if(!listening){manager?.AddOnActiveSessionsChangedListener(this,component,handler);listening=true;}
            OnActiveSessionsChanged(manager?.GetActiveSessions(component));
        }
        catch(Java.Lang.SecurityException){Stop();}
    }
    public void Stop()
    {
        if(listening){try{manager?.RemoveOnActiveSessionsChangedListener(this);}catch(Java.Lang.SecurityException){}listening=false;}
        Clear();Controller=null;
    }
    private void Clear()
    {
        foreach(var item in sessions)try{item.Controller.UnregisterCallback(item.Callback);}catch(Java.Lang.RuntimeException){}
        sessions.Clear();
    }
    public void OnActiveSessionsChanged(IList<MediaController>? controllers)
    {
        Clear();
        // A newly active session can publish its playback state after the session-list
        // callback. Observe candidates too, so its transition from None is not lost.
        foreach(var controller in (controllers??[]).Take(20))
        {
            var callback=new MediaCallback(Select,()=>{sessions.RemoveAll(s=>s.Controller==controller);Select();});
            try{controller.RegisterCallback(callback,handler);sessions.Add((controller,callback));}catch(Java.Lang.RuntimeException){}
        }
        Select();
    }
    private void Select()
    {
        Controller=sessions.Select(s=>s.Controller).OrderByDescending(c=>c.PackageName==selectedPackage).ThenByDescending(c=>c.PlaybackState?.State is PlaybackStateCode.Playing or PlaybackStateCode.Buffering)
            .FirstOrDefault(c=>c.PlaybackState?.State is PlaybackStateCode.Playing or PlaybackStateCode.Buffering or PlaybackStateCode.Paused or PlaybackStateCode.Connecting);
        changed();
    }
    private sealed class MediaCallback(Action changed,Action destroyed):MediaController.Callback
    {
        private PlaybackStateCode? previous;
        private long previousActions=-1;
        public override void OnMetadataChanged(MediaMetadata? metadata)=>changed();
        public override void OnPlaybackStateChanged(PlaybackState? state){if(previous==state?.State&&previousActions==(state?.Actions??0))return;previous=state?.State;previousActions=state?.Actions??0;changed();}
        public override void OnSessionDestroyed()=>destroyed();
    }
}
