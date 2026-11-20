namespace Launcher.Core;

public sealed record BadgeSignal(string Key, string Package, string Group, int Count, bool Summary, bool Clearable);

public static class NotificationBadges
{
    public static Dictionary<string, int> Count(IEnumerable<BadgeSignal> signals)
    {
        var result = new Dictionary<string, int>();
        foreach (var app in signals.Where(s => s.Clearable).GroupBy(s => s.Package))
        {
            int total = 0;
            foreach (var group in app.GroupBy(s => s.Group))
            {
                var children = group.Where(s => !s.Summary).ToArray();
                total += children.Length > 0 ? children.Sum(s => Math.Clamp(s.Count, 1, 9999))
                    : group.Where(s => s.Summary).Select(s => Math.Clamp(s.Count, 1, 9999)).DefaultIfEmpty(0).Max();
            }
            if (total > 0)
                result[app.Key] = total;
        }
        return result;
    }
    public static string Label(int count) => count > 99 ? "99+" : Math.Max(0, count).ToString();
}
