using Android.App;
using Android.Widget;
using Android.Views;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private void MessageHistorySettings() => Sheet(Localize(Resource.String.ui_message_history), (panel, dialog) =>
    {
        Paragraph(Localize(Resource.String.ui_keep_messages_made_available_by_notifications_on_this_phone), panel);
        panel.AddView(Button(state.KeepMessageHistory ? Localize(Resource.String.ui_stop_collecting_messages) : Localize(Resource.String.ui_enable_local_message_history), () =>
        {
            state.KeepMessageHistory = !state.KeepMessageHistory;
            Save();
            dialog.Dismiss();
            if (state.KeepMessageHistory)
                ConversationService.CaptureAvailableHistory();
            RenderBody();
        }, true), Match());
        Gap(panel, 8);
        panel.AddView(Button(Localize(Resource.String.ui_open_saved_conversations), () => { dialog.Dismiss(); ShowMessageHistory(); }), Match());
        Gap(panel, 8);
        panel.AddView(Button(Localize(Resource.String.ui_clear_saved_history), () => Confirm(Localize(Resource.String.ui_clear_saved_history_2), Localize(Resource.String.ui_delete_all_messages_saved_by_launcher_messages_in_the), Localize(Resource.String.ui_clear_history), () => { using var history = new MessageHistory(this); history.Clear(); dialog.Dismiss(); Toast(Localize(Resource.String.ui_saved_history_cleared)); })), Match());
    });
    private void ShowMessageHistory(string? package = null, string? thread = null)
    {
        if (((KeyguardManager?)GetSystemService(KeyguardService))?.IsDeviceLocked == true)
        {
            Toast(Localize(Resource.String.ui_unlock_your_phone_first));
            return;
        }
        using var history = new MessageHistory(this);
        var messages = history.Read(package, thread).Where(m => !state.ConversationExcludedApps.Contains(m.Package)).ToArray();
        Sheet(thread is null ? Localize(Resource.String.ui_saved_conversations) : Localize(Resource.String.ui_conversation_history), (panel, dialog) =>
        {
            if (messages.Length == 0)
                Paragraph(Localize(Resource.String.ui_no_saved_messages_yet_enable_history_then_allow_new), panel);
            if (thread is null)
                foreach (var group in messages.GroupBy(m => (m.Package, m.Thread)))
                {
                    var item = group.First();
                    bool hidden = ConcealPreview(item.Package);
                    SettingRow(panel, hidden ? PackageLabel(item.Package) : item.Title, hidden ? Localize(Resource.String.ui_protected_conversation_unlock_to_read) : item.Text, () => WithAppUnlock(item.Package, () => { dialog.Dismiss(); ShowMessageHistory(item.Package, item.Thread); }), "message");
                }
            else
            {
                foreach (var message in messages.Reverse())
                {
                    var text = Text((message.Outgoing ? Localize(Resource.String.ui_you) : message.Sender) + " · " + DateTimeOffset.FromUnixTimeMilliseconds(message.Time).ToLocalTime().ToString("g"), 11, true, message.Outgoing ? palette.Accent : palette.Muted);
                    panel.AddView(text, Match());
                    Paragraph(message.Text, panel);
                }
                var live = ConversationService.NotificationSnapshot().FirstOrDefault(n => n.Package == package && ConversationService.Thread(n) == thread);
                if (live is not null && ConversationService.CanReply(live.Key))
                    panel.AddView(Button(Localize(Resource.String.ui_reply), () => { dialog.Dismiss(); ReplyTo(live); }, true), Match());
            }
        });
    }
    private void ReplyTo(Conversation notification) => WithAppUnlock(notification.Package, () =>
    {
        EditText? input = null;
        var sheet = Sheet(Localize(Resource.String.ui_reply_to) + notification.Title, (panel, dialog) =>
        {
            input = FormField(panel, Localize(Resource.String.ui_message), "", Localize(Resource.String.ui_your_reply), multiline: true);
            input.SetFilters([new Android.Text.InputFilterLengthFilter(4000)]);
            Paragraph(Localize(Resource.String.ui_uses_this_apps_current_notification_reply_action_launcher_cannot), panel);
        }, (footer, dialog) => DialogActions(footer, (Localize(Resource.String.ui_cancel), dialog.Dismiss), (Localize(Resource.String.ui_send_reply), () =>
        {
            string text = input?.Text?.Trim() ?? "";
            if (text.Length == 0)
            {
                input?.RequestFocus();
                return;
            }
            if (ConversationService.Reply(this, notification, text))
            {
                dialog.Dismiss();
                Toast(Localize(Resource.String.ui_reply_passed_to_the_app));
            }
            else
                Toast(Localize(Resource.String.ui_this_reply_action_is_no_longer_available_open_the));
        }
        )));
        input!.RequestFocus();
        input.PostDelayed(() => { if (sheet.IsShowing) ((Android.Views.InputMethods.InputMethodManager?)GetSystemService(InputMethodService))?.ShowSoftInput(input, Android.Views.InputMethods.ShowFlags.Implicit); }, 180);
    });
}
