namespace Launcher.Core;

public static class BackupRestore
{
    public static LauncherState Select(LauncherState current, LauncherState backup, bool appearance, bool apps, bool notes, bool workspace)
    {
        var result = LauncherState.Deserialize((appearance ? backup : current).Serialize());
        var original = LauncherState.Deserialize(current.Serialize());
        if (!apps)
        {
            result.Apps = original.Apps;
            result.Drawers = original.Drawers;
            result.QuickAccessApps = original.QuickAccessApps;
            result.Shortcuts = original.Shortcuts;
        }
        else
        {
            var source = LauncherState.Deserialize(backup.Serialize());
            result.Apps = source.Apps;
            result.Drawers = source.Drawers;
            result.QuickAccessApps = source.QuickAccessApps;
            result.Shortcuts = source.Shortcuts;
        }
        var content = LauncherState.Deserialize((notes ? backup : current).Serialize());
        result.Notes = content.Notes;
        result.Note = content.Note;
        result.DeletedNoteIds = content.DeletedNoteIds;
        content = LauncherState.Deserialize((workspace ? backup : current).Serialize());
        result.Timers = content.Timers;
        result.Tickets = content.Tickets;
        result.ExtraCards = content.ExtraCards;
        result.RootOrder = content.RootOrder;
        result.RootHidden = content.RootHidden;
        result.RootPinned = content.RootPinned;
        result.CollapsedCards = content.CollapsedCards;
        result.RssSources = content.RssSources;
        result.SavedArticles = content.SavedArticles;
        result.ReadArticleUrls = content.ReadArticleUrls;
        // Device grants, setup and widget bindings never come from a backup.
        result.Reminders = workspace ? backup.Reminders : original.Reminders;
        result.Widgets = original.Widgets;
        result.Shortcuts = original.Shortcuts;
        result.CalendarIds = original.CalendarIds;
        result.SystemPanelEnabled = original.SystemPanelEnabled;
        result.KeepMessageHistory = original.KeepMessageHistory;
        result.SetupCompleted = original.SetupCompleted;
        result.Normalize();
        return result;
    }
}
