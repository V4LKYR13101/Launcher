using Android.Content;
namespace LauncherApp.Services;

public sealed record AgendaEvent(long Id, string Title, string Location, long Begins, long Ends, bool AllDay);
public static class AgendaReader
{
    public static bool Allowed(Context context) => context.CheckSelfPermission(Android.Manifest.Permission.ReadCalendar) == Android.Content.PM.Permission.Granted;
    public static AgendaEvent[] Read(Context context, IReadOnlyList<long> calendars)
    {
        if (!Allowed(context))
            return [];
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var builder = Android.Provider.CalendarContract.Instances.ContentUri!.BuildUpon()!;
        ContentUris.AppendId(builder, now - 86400000);
        ContentUris.AppendId(builder, now + 7 * 86400000L);
        string selection = "visible=1 AND end>?";
        var args = new List<string> { now.ToString() };
        if (calendars.Count > 0)
        {
            selection += " AND calendar_id IN (" + string.Join(',', calendars.Select(_ => "?")) + ")";
            args.AddRange(calendars.Select(id => id.ToString()));
        }
        using var cursor = context.ContentResolver!.Query(builder.Build()!, ["event_id", "title", "eventLocation", "begin", "end", "allDay"], selection, args.ToArray(), "begin ASC");
        var result = new List<AgendaEvent>();
        while (cursor?.MoveToNext() == true && result.Count < 12)
            result.Add(new(cursor.GetLong(0), cursor.GetString(1) ?? "Event", cursor.IsNull(2) ? "" : cursor.GetString(2)!, cursor.GetLong(3), cursor.GetLong(4), cursor.GetInt(5) != 0));
        return result.ToArray();
    }
    public static (long Id, string Name)[] Calendars(Context context)
    {
        if (!Allowed(context))
            return [];
        using var cursor = context.ContentResolver!.Query(Android.Provider.CalendarContract.Calendars.ContentUri!, ["_id", "calendar_displayName"], "visible=1", null, "calendar_displayName");
        var result = new List<(long, string)>();
        while (cursor?.MoveToNext() == true && result.Count < 100)
            result.Add((cursor.GetLong(0), cursor.GetString(1) ?? "Calendar"));
        return result.ToArray();
    }
}
