using Android.Content;
using Android.Views;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private CompanionLink companion = null!;
    private bool companionSession;
    private void RenderCompanion()
    {
        Title("Desktop companion");
        Paragraph("Open Launcher Companion on a Windows computer on the same Wi-Fi. Pair with its address and code, then compare the certificate fingerprint. Notes and shared files stay on your network.");
        Paragraph("Keep Launcher visible while using the companion. The connection pauses whenever you leave Launcher. Transfers are limited to 16 MB per file.");
        SettingRow(body, "Desktop", companion.Paired ? companion.Address : "Not paired", PairCompanion, "computer");
        SettingRow(body, "Connection", companion.Status, () => { if (companionSession) { companionSession = false; companion.Stop(); } else { companionSession = true; companion.Start(); } RenderBody(); }, "link");
        SettingRow(body, "Shared folder", companion.Tree.Length > 0 ? "Selected · tap to change" : "Choose one folder for photos and files", () =>
        {
            var intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission | ActivityFlags.GrantPrefixUriPermission);
            StartActivityForResult(intent, 815);
        }, "file");
        ToggleRow(body, "Allow desktop uploads", companion.Uploads, v => companion.SetUploads(v));
        Paragraph("Desktop can read files inside the selected folder and edit notes. Uploads create new files. It cannot delete your files. Pairing resets when you close and restart the desktop program.");
        if (companion.Tree.Length > 0)
            body.AddView(Button("Stop sharing folder", () => { try { ContentResolver!.ReleasePersistableUriPermission(Android.Net.Uri.Parse(companion.Tree)!, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission); } catch (Java.Lang.SecurityException) { } companion.SetFolder(""); companionSession = false; RenderBody(); }), Match());
        body.AddView(Button("Forget computer", () => { companion.Forget(); companionSession = false; RenderBody(); }), Match());
    }
    private void PairCompanion() => Prompt("Desktop companion address", companion.Address.Length > 0 ? companion.Address : "https://192.168.1.10:17842", async value =>
    {
        try
        {
            string address = CompanionLink.ValidateAddress(value);
            string fingerprint = await CompanionLink.Probe(address);
            if (IsDestroyed)
                return;
            string formatted = string.Join(" ", Enumerable.Range(0, 16).Select(i => fingerprint.Substring(i * 4, 4)));
            Confirm("Compare fingerprints", formatted + "\n\nOnly continue if this matches the fingerprint shown on your own desktop.", "Matches", () => Prompt("Pairing code", "", async pin =>
            {
                try
                {
                    companion.Stop();
                    await companion.Pair(address, fingerprint, pin);
                    if (IsDestroyed)
                        return;
                    companionSession = true;
                    companion.Start();
                    RenderBody();
                    Toast("Computer paired.");
                }
                catch (Exception e) when (e is HttpRequestException or IOException or ArgumentException or TaskCanceledException) { Toast("Pairing failed. Check the code and desktop connection."); }
            }));
        }
        catch (Exception e) when (e is HttpRequestException or IOException or ArgumentException or TaskCanceledException) { Toast("Could not connect. Check the address, Wi-Fi and Windows firewall."); }
    });
    private void WorkspaceUpdated() => RunOnUiThread(() =>
    {
        if (IsDestroyed)
            return;
        var latest = store.Load();
        state.SystemPanelEnabled = latest.SystemPanelEnabled;
        state.Notes = latest.Notes;
        state.DeletedNoteIds = latest.DeletedNoteIds;
        state.RootOrder = latest.RootOrder;
        state.Timers = latest.Timers;
        state.Reminders = latest.Reminders;
        if (resumed && page == "Root" && WorkspaceEditing.NoteId is null && !searching)
            RenderBody();
    });
}
