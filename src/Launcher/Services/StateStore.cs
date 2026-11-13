using Android.Content;
using Launcher.Core;

namespace LauncherApp.Services;

public sealed class StateStore(Context context)
{
    private readonly ISharedPreferences prefs = context.GetSharedPreferences("launcher-state", FileCreationMode.Private)!;
    public LauncherState Load()
    {
        try { return LauncherState.Deserialize(prefs.GetString("state", "")!); }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException or ArgumentException)
        {
            // Preserve the unreadable value for recovery; don't overwrite on read.
            var old = prefs.GetString("state", null);
            if (!string.IsNullOrEmpty(old)) prefs.Edit()!.PutString("recovery", old)!.Apply();
            return new();
        }
    }
    public void Save(LauncherState state) => prefs.Edit()!.PutString("state", state.Serialize())!.Apply();
}
