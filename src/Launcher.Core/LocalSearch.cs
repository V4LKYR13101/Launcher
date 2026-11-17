namespace Launcher.Core;

public sealed record LocalSearchResult(string Kind,string Id,string Title,string Preview);
public static class LocalSearch
{
    public static LocalSearchResult[] Find(LauncherState state,string query)
    {
        string[] terms=query.Trim().Split(' ',StringSplitOptions.RemoveEmptyEntries);if(terms.Length==0)return [];
        bool Matches(params string[] fields)=>terms.All(term=>fields.Any(field=>field.Contains(term,StringComparison.CurrentCultureIgnoreCase)));
        return state.Notes.Where(n=>Matches(n.Title,n.Text)).Select(n=>new LocalSearchResult("Note",n.Id,n.Title,n.Text.Split('\n')[0]))
            .Concat(state.Tickets.Where(t=>Matches(t.Title,t.Route,t.Reference,t.Date)).Select(t=>new LocalSearchResult("Journey",t.Id,t.Title,t.Route+" · "+t.Date)))
            .Concat(state.SavedArticles.Where(a=>Matches(a.Title,a.Detail,a.Url)).Select(a=>new LocalSearchResult("Saved story",a.Url,a.Title,a.Detail)))
            .Concat(state.Reminders.Where(r=>Matches(r.Title)).Select(r=>new LocalSearchResult("Reminder",r.Id,r.Title,r.Completed?"Completed":"Scheduled reminder")))
            .Take(50).ToArray();
    }
}
