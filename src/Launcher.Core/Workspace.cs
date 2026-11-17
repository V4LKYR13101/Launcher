namespace Launcher.Core;

public sealed class WorkspaceNote
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Title {get;set;}="Note";
    public string Text {get;set;}="";
    public bool Checklist {get;set;}
    public long UpdatedAt {get;set;}=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
public sealed class WorkspaceTimer
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Title {get;set;}="Timer";
    public long DurationMs {get;set;}=300000;
    public long RemainingMs {get;set;}=300000;
    public long EndsAt {get;set;}
    public long LastAlertEndsAt {get;set;}
}
public sealed class WorkspaceReminder
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Title {get;set;}="Reminder";
    public long DueAt {get;set;}
    public long LastAlertDueAt {get;set;}
    public int RepeatDays {get;set;}
    public bool Completed {get;set;}
}
public static class WorkspaceReconciliation
{
    public static void PreserveDeliveredAlerts(LauncherState pending,LauncherState latest)
    {
        var timers=latest.Timers.Where(t=>t.LastAlertEndsAt>0).ToDictionary(t=>t.Id);
        foreach(var timer in pending.Timers)
        {
            if(!timers.TryGetValue(timer.Id,out var saved))continue;
            if(timer.EndsAt==saved.LastAlertEndsAt){timer.EndsAt=saved.EndsAt;timer.RemainingMs=saved.RemainingMs;}
            timer.LastAlertEndsAt=saved.LastAlertEndsAt;
        }
        var reminders=latest.Reminders.Where(r=>r.LastAlertDueAt>0).ToDictionary(r=>r.Id);
        foreach(var reminder in pending.Reminders)
        {
            if(!reminders.TryGetValue(reminder.Id,out var saved))continue;
            if(!reminder.Completed&&reminder.DueAt==saved.LastAlertDueAt){reminder.DueAt=saved.DueAt;reminder.Completed=saved.Completed;}
            reminder.LastAlertDueAt=saved.LastAlertDueAt;
        }
    }
}
public static class Checklist
{
    public static (bool Done,string Text)[] Read(string text)=>text.Split('\n').Where(s=>!string.IsNullOrWhiteSpace(s)).Take(100).Select(s=>(s.StartsWith("[x]",StringComparison.OrdinalIgnoreCase),(s.StartsWith("[ ]")||s.StartsWith("[x]",StringComparison.OrdinalIgnoreCase)?s[3..]:s).Trim())).ToArray();
    public static string Toggle(string text,int index,bool done)=>string.Join('\n',Read(text).Select((item,i)=>(i==index?done:item.Done)?"[x] "+item.Text:"[ ] "+item.Text));
}
public sealed class WorkspaceCard
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Kind {get;set;}="rss";
    public string Title {get;set;}="";
    public string Query {get;set;}="";
}
public sealed class TravelTicket
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Title {get;set;}="Journey";
    public string Route {get;set;}="";
    public string Reference {get;set;}="";
    public string Date {get;set;}="";
    public string Attachment {get;set;}="";
}
public sealed record ForecastHour(string Time,double Temperature,int Code);
public sealed record ForecastDay(string Date,double High,double Low,int Code);

public static class RootCommand
{
    public static (string Kind,string Value) Parse(string text)
    {
        var parts=text.Trim().Split(' ',2,StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length==0)return ("","");
        string kind=parts[0].ToLowerInvariant(),value=parts.Length>1?parts[1].Trim():"";
        return kind switch {"note" or "notes"=>("note",value),"timer"=>("timer",value),"weather"=>("weather",value),
            "music" or "media"=>("media",value),"rss" or "feed"=>("rss",value),"youtube" or "yt"=>("youtube",value),
            "resources" or "device"=>("resources",value),
            "ticket"=>("ticket",value),"web" or "search"=>("web",value),_=>("web",text.Trim())};
    }
    public static long TimerDuration(string value)
    {
        var match=System.Text.RegularExpressions.Regex.Match(value.Trim(),@"^(\d+(?:\.\d+)?)\s*(s|sec|seconds?|m|min|minutes?|h|hours?)?$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if(!match.Success||!double.TryParse(match.Groups[1].Value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var amount))throw new ArgumentException("Use a duration such as 10 min, 30s or 1h.");
        double factor=match.Groups[2].Value.ToLowerInvariant() switch {"s" or "sec" or "second" or "seconds"=>1000,"h" or "hour" or "hours"=>3600000,_=>60000};
        return (long)Math.Clamp(amount*factor,1000,86400000);
    }
}
