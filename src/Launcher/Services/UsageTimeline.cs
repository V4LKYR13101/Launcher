using Android.App.Usage;
using Android.Content;
namespace LauncherApp.Services;

public readonly record struct UsageSpan(string Package,long Begin,long End);
public static class UsageTimeline
{
    private sealed class Session
    {public HashSet<string> Activities {get;}=[];public long Begin;}
    public static List<UsageSpan> Read(Context context,long start,long end)
    {
        var result=new List<UsageSpan>();var sessions=new Dictionary<string,Session>();
        var manager=(UsageStatsManager?)context.GetSystemService(Context.UsageStatsService);
        var events=manager?.QueryEvents(start-86_400_000,end);if(events is null)return result;
        var item=new UsageEvents.Event();
        void Close(string package,Session session,long at)
        {long begin=Math.Max(start,session.Begin),stop=Math.Min(end,at);if(stop>begin)result.Add(new(package,begin,stop));}
        while(events.HasNextEvent)
        {
            events.GetNextEvent(item);int type=(int)item.EventType;
            if(type is 15 or 26)
            {foreach(var (package,session) in sessions)Close(package,session,item.TimeStamp);sessions.Clear();continue;}
            if(item.PackageName is not {} key||type is not (1 or 2 or 23))continue;
            string activity=item.ClassName??"";
            if(type==1)
            {
                if(!sessions.TryGetValue(key,out var session))sessions[key]=session=new(){Begin=item.TimeStamp};
                session.Activities.Add(activity);
            }
            else if(sessions.TryGetValue(key,out var session)&&session.Activities.Remove(activity)&&session.Activities.Count==0)
            {Close(key,session,item.TimeStamp);sessions.Remove(key);}
        }
        foreach(var (package,session) in sessions)Close(package,session,end);
        return result;
    }
}
