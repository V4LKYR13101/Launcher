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
        Title(Localize(Resource.String.ui_desktop_companion));
        Paragraph(Localize(Resource.String.ui_open_launcher_companion_on_a_windows_computer_on_the));
        Paragraph(Localize(Resource.String.ui_keep_launcher_visible_while_using_the_companion_the_connection));
        SettingRow(body, Localize(Resource.String.ui_desktop), companion.Paired ? companion.Address : Localize(Resource.String.ui_not_paired), PairCompanion, "computer");
        SettingRow(body, Localize(Resource.String.ui_connection), companion.Status, () => { if (companionSession) { companionSession = false; companion.Stop(); } else { companionSession = true; companion.Start(); } RenderBody(); }, "link");
        SettingRow(body, Localize(Resource.String.ui_shared_folder), companion.Tree.Length > 0 ? Localize(Resource.String.ui_selected_tap_to_change) : Localize(Resource.String.ui_choose_one_folder_for_photos_and_files), () =>
        {
            var intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission | ActivityFlags.GrantPrefixUriPermission);
            StartActivityForResult(intent, 815);
        }, "file");
        ToggleRow(body, Localize(Resource.String.ui_allow_desktop_uploads), companion.Uploads, v => companion.SetUploads(v));
        Paragraph(Localize(Resource.String.ui_desktop_can_read_files_inside_the_selected_folder_and));
        if (companion.Tree.Length > 0)
            body.AddView(Button(Localize(Resource.String.ui_stop_sharing_folder), () => { try { ContentResolver!.ReleasePersistableUriPermission(Android.Net.Uri.Parse(companion.Tree)!, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission); } catch (Java.Lang.SecurityException) { } companion.SetFolder(""); companionSession = false; RenderBody(); }), Match());
        body.AddView(Button(Localize(Resource.String.ui_forget_computer), () => { companion.Forget(); companionSession = false; RenderBody(); }), Match());
    }
    private void PairCompanion() => Prompt(Resource.String.ui_desktop_companion_address, companion.Address.Length > 0 ? companion.Address : "https://192.168.1.10:17842", async value =>
    {
        try
        {
            string address = CompanionLink.ValidateAddress(value);
            string fingerprint = await CompanionLink.Probe(address);
            if (IsDestroyed)
                return;
            string formatted = string.Join(" ", Enumerable.Range(0, 16).Select(i => fingerprint.Substring(i * 4, 4)));
            Confirm(Localize(Resource.String.ui_compare_fingerprints), formatted + Localize(Resource.String.ui_only_continue_if_this_matches_the_fingerprint_shown_on), Localize(Resource.String.ui_matches), () => Prompt(Resource.String.ui_pairing_code, "", async pin =>
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
                    Toast(Localize(Resource.String.ui_computer_paired));
                }
                catch (Exception e) when (e is HttpRequestException or IOException or ArgumentException or TaskCanceledException) { Toast(Localize(Resource.String.ui_pairing_failed_check_the_code_and_desktop_connection)); }
            }));
        }
        catch (Exception e) when (e is HttpRequestException or IOException or ArgumentException or TaskCanceledException) { Toast(Localize(Resource.String.ui_could_not_connect_check_the_address_wi_fi_and)); }
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
        if (resumed && page == "Root" && todayLayoutEditor is null && WorkspaceEditing.NoteId is null && !searching)
            RenderBody();
    });
}
