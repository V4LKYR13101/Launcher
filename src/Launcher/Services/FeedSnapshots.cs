using System.Text.Json;
using Launcher.Core;
namespace LauncherApp.Services;

public sealed record FeedSnapshot(string Url,CardLink[] Links,long UpdatedAt);

public static class FeedSnapshots
{
    public static Dictionary<string,FeedSnapshot> Read(string folder,IReadOnlyList<RssSource> sources)
    {
        try
        {
            var file=System.IO.Path.Combine(folder,"feeds.json");
            if(!File.Exists(file)||new FileInfo(file).Length>3_000_000)return [];
            var saved=JsonSerializer.Deserialize<Dictionary<string,FeedSnapshot>>(File.ReadAllText(file))??[];
            return saved.Where(p=>sources.Any(s=>s.Id==p.Key&&s.Url==p.Value?.Url)&&p.Value.Links is not null)
                .ToDictionary(p=>p.Key,p=>p.Value with {Links=p.Value.Links.Where(a=>a is not null&&OnlineCards.SafeLink(a.Url)).Take(100).ToArray()});
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException){return [];}
    }

    public static void Write(string folder,Dictionary<string,FeedSnapshot> snapshots)
    {
        try
        {
            var file=System.IO.Path.Combine(folder,"feeds.json");var json=JsonSerializer.Serialize(snapshots);
            if(json.Length>3_000_000)return;
            File.WriteAllText(file+".tmp",json);File.Move(file+".tmp",file,true);
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){}
    }
}
