using Android.Content;
using Launcher.Core;

namespace LauncherApp.Services;

public sealed class StateStore(Context context)
{
    private static readonly object Gate = new();
    public static event Action? WorkspaceChanged;
    public static void NotifyWorkspaceChanged() => WorkspaceChanged?.Invoke();
    private readonly ISharedPreferences prefs = context.GetSharedPreferences("launcher-state", FileCreationMode.Private)!;
    private string Current => Path.Combine(context.FilesDir!.AbsolutePath, "launcher-state.json");
    private string Previous => Current + ".previous";
    private string Recovery => Current + ".recovery";
    public bool RecoveryAvailable => File.Exists(Recovery) || prefs.Contains("recovery");
    public bool PreviousAvailable => File.Exists(Previous);
    public bool RecoveredOnLoad
    {
        get; private set;
    }
    public LauncherState Load()
    {
        lock (Gate)
            return Read();
    }
    private LauncherState Read()
    {
        string? raw = null;
        try
        {
            raw = File.Exists(Current) ? ReadBounded(Current) : prefs.GetString("state", null);
            if (string.IsNullOrEmpty(raw))
            {
                if (File.Exists(Current))
                    throw new InvalidDataException("The state file is empty.");
                var initial = new LauncherState();
                initial.Normalize();
                return initial;
            }
            return LauncherState.Deserialize(raw);
        }
        catch (Exception e) when (Invalid(e) || e is IOException or UnauthorizedAccessException)
        {
            RecoveredOnLoad = true;
            try
            {
                if (File.Exists(Current) && !File.Exists(Recovery))
                    File.Copy(Current, Recovery);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (!File.Exists(Current) && !string.IsNullOrEmpty(raw) && !prefs.Contains("recovery"))
                prefs.Edit()!.PutString("recovery", raw)!.Apply();
            try
            {
                if (File.Exists(Previous))
                    return LauncherState.Deserialize(ReadBounded(Previous));
            }
            catch (Exception previous) when (Invalid(previous) || previous is IOException or UnauthorizedAccessException) { }
            var fallback = new LauncherState();
            fallback.Normalize();
            return fallback;
        }
    }
    private static bool Invalid(Exception e) => e is System.Text.Json.JsonException or InvalidDataException or ArgumentException;
    private static string ReadBounded(string path)
    {
        using var reader = new StreamReader(path);
        var text = new System.Text.StringBuilder();
        var block = new char[16384];
        int count;
        while ((count = reader.Read(block)) > 0)
        {
            if (text.Length + count > LauncherState.MaximumJsonCharacters)
                throw new InvalidDataException("State is too large.");
            text.Append(block, 0, count);
        }
        return text.ToString();
    }
    public LauncherState LoadPrevious()
    {
        lock (Gate)
            return LauncherState.Deserialize(ReadBounded(Previous));
    }
    public void Save(LauncherState state, bool mergeNotes = true)
    {
        lock (Gate)
        {
            if (mergeNotes)
            {
                var latest = Read();
                WorkspaceReconciliation.PreserveDeliveredAlerts(state, latest);
                state.DeletedNoteIds = state.DeletedNoteIds.Concat(latest.DeletedNoteIds).Distinct().TakeLast(1000).ToList();
                state.Notes = state.Notes.Concat(latest.Notes).GroupBy(n => n.Id).Select(g => g.OrderByDescending(n => n.UpdatedAt).First()).Where(n => !state.DeletedNoteIds.Contains(n.Id)).Take(200).ToList();
            }
            string json = state.Serialize(), temporary = Current + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8, 16384, true))
                    {
                        writer.Write(json);
                        writer.Flush();
                    }
                    stream.Flush(true);
                }
                if (File.Exists(Current))
                {
                    try
                    {
                        LauncherState.Deserialize(ReadBounded(Current));
                        File.Copy(Current, Previous, true);
                    }
                    catch (Exception e) when (Invalid(e)) { }
                }
                File.Move(temporary, Current, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    public void EditWorkspace(Func<LauncherState, bool> edit)
    {
        bool changed;
        lock (Gate)
        {
            var current = Read();
            changed = edit(current);
            if (changed)
                Save(current, false);
        }
        if (changed)
            NotifyWorkspaceChanged();
    }
    public void EditNotes(Action<LauncherState> edit)
    {
        lock (Gate)
        {
            var current = Read();
            edit(current);
            Save(current, false);
        }
        NotifyWorkspaceChanged();
    }
}
