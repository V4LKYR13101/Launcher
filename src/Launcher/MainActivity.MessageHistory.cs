using Android.App;
using Android.Widget;
using Android.Views;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private void MessageHistorySettings() => Sheet("Message history", (panel, dialog) =>
    {
        Paragraph("Keep messages made available by notifications on this phone. History is private to Launcher, retained for 30 days or 5,000 messages, and excluded from layout backups. It cannot import earlier private conversations from another app.", panel);
        panel.AddView(Button(state.KeepMessageHistory ? "Stop collecting messages" : "Enable local message history", () =>
        {
            state.KeepMessageHistory = !state.KeepMessageHistory;
            Save();
            dialog.Dismiss();
            if (state.KeepMessageHistory)
                ConversationService.CaptureAvailableHistory();
            RenderBody();
        }, true), Match());
        Gap(panel, 8);
        panel.AddView(Button("Open saved conversations", () => { dialog.Dismiss(); ShowMessageHistory(); }), Match());
        Gap(panel, 8);
        panel.AddView(Button("Clear saved history", () => Confirm("Clear saved history?", "Delete all messages saved by Launcher? Messages in the original apps are unchanged.", "Clear history", () => { using var history = new MessageHistory(this); history.Clear(); dialog.Dismiss(); Toast("Saved history cleared."); })), Match());
    });
    private void ShowMessageHistory(string? package = null, string? thread = null)
    {
        if (((KeyguardManager?)GetSystemService(KeyguardService))?.IsDeviceLocked == true)
        {
            Toast("Unlock your phone first.");
            return;
        }
        using var history = new MessageHistory(this);
        var messages = history.Read(package, thread).Where(m => !state.ConversationExcludedApps.Contains(m.Package)).ToArray();
        Sheet(thread is null ? "Saved conversations" : "Conversation history", (panel, dialog) =>
        {
            if (messages.Length == 0)
                Paragraph("No saved messages yet. Enable history, then allow new message notifications to arrive.", panel);
            if (thread is null)
                foreach (var group in messages.GroupBy(m => (m.Package, m.Thread)))
                {
                    var item = group.First();
                    bool hidden = ConcealPreview(item.Package);
                    SettingRow(panel, hidden ? PackageLabel(item.Package) : item.Title, hidden ? "Protected conversation · unlock to read" : item.Text, () => WithAppUnlock(item.Package, () => { dialog.Dismiss(); ShowMessageHistory(item.Package, item.Thread); }), "message");
                }
            else
            {
                foreach (var message in messages.Reverse())
                {
                    var text = Text((message.Outgoing ? "You" : message.Sender) + " · " + DateTimeOffset.FromUnixTimeMilliseconds(message.Time).ToLocalTime().ToString("g"), 11, true, message.Outgoing ? palette.Accent : palette.Muted);
                    panel.AddView(text, Match());
                    Paragraph(message.Text, panel);
                }
                var live = ConversationService.NotificationSnapshot().FirstOrDefault(n => n.Package == package && ConversationService.Thread(n) == thread);
                if (live is not null && ConversationService.CanReply(live.Key))
                    panel.AddView(Button("Reply", () => { dialog.Dismiss(); ReplyTo(live); }, true), Match());
            }
        });
    }
    private void ReplyTo(Conversation notification) => WithAppUnlock(notification.Package, () =>
    {
        EditText? input = null;
        var sheet = Sheet("Reply to " + notification.Title, (panel, dialog) =>
        {
            input = FormField(panel, "Message", "", "Your reply", multiline: true);
            input.SetFilters([new Android.Text.InputFilterLengthFilter(4000)]);
            Paragraph("Uses this app’s current notification reply action. Launcher cannot confirm delivery to the recipient.", panel);
        }, (footer, dialog) => DialogActions(footer, ("Cancel", dialog.Dismiss), ("Send reply", () =>
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
                Toast("Reply passed to the app.");
            }
            else
                Toast("This reply action is no longer available. Open the app to reply.");
        }
        )));
        input!.RequestFocus();
        input.PostDelayed(() => { if (sheet.IsShowing) ((Android.Views.InputMethods.InputMethodManager?)GetSystemService(InputMethodService))?.ShowSoftInput(input, Android.Views.InputMethods.ShowFlags.Implicit); }, 180);
    });
}
