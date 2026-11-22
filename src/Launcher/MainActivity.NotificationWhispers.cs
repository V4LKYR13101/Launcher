
using Android.Views;
using Android.Widget;
using LauncherApp.Services;
using LauncherApp.Services.Speech;

namespace LauncherApp;

public partial class MainActivity
{
    private void ShowNotificationWhisperSettings()
    {
        var options = apps
            .Where(a => !a.PrivateProfile)
            .Select(a => (
                Package: a.PackageName,
                Name: a.Label))
            .Concat(
                ConversationService.NotificationSnapshot()
                    .Select(n => (
                        Package: n.Package,
                        Name: apps.FirstOrDefault(
                            a => a.PackageName == n.Package)?.Label
                            ?? n.Package)))
            .Where(a => a.Package != PackageName)
            .DistinctBy(a => a.Package)
            .OrderBy(a => a.Name)
            .ToArray();

        Sheet("Notification whispers", (panel, dialog) =>
        {
            Paragraph(
                "Use a quiet Kokoro voice for notifications from " +
                "*selected apps*. Message contents are never read aloud.",
                panel);

            ToggleRow(
                panel,
                "Enable notification whispers",
                NotificationWhisperPreferences.Enabled(this),
                value =>
                    NotificationWhisperPreferences.SetEnabled(
                        this, value));

            ToggleRow(
                panel,
                "Messages only",
                NotificationWhisperPreferences.MessagesOnly(this),
                value =>
                    NotificationWhisperPreferences.SetMessagesOnly(
                        this, value));

            Gap(panel, 8);

            Paragraph(
                "Select apps below. Optionally add a sender or title " +
                "filter so only matching notifications are whispered.",
                panel);

            var search = new EditText(this)
            {
                Hint = "Search apps",
                TextSize = 15,
                Typeface = regular,
                Background = Background(palette.Panel, 14)
            };

            search.SetSingleLine(true);
            search.SetTextColor(palette.Text);
            search.SetHintTextColor(palette.Muted);
            search.SetPadding(
                Dp(14), Dp(12), Dp(14), Dp(12));

            StyleSearchEntry(search, 14, inSheet: true);

            panel.AddView(search, Match());

            Gap(panel, 8);

            var list = Stack();
            panel.AddView(list, Match());

            string query = "";

            void Rebuild()
            {
                list.RemoveAllViews();

                foreach (var option in options.Where(a =>
                    a.Name.Contains(
                        query,
                        StringComparison.CurrentCultureIgnoreCase) ||
                    a.Package.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    string package = option.Package;
                    string name = option.Name;

                    var group = Stack();

                    var checkbox = new CheckBox(this)
                    {
                        Text = name,
                        Checked =
                            NotificationWhisperPreferences
                                .IsPackageEnabled(this, package),
                        TextSize = 15,
                        Typeface = regular,
                        ButtonTintList = AccentTint()
                    };

                    checkbox.SetTextColor(palette.Text);
                    checkbox.SetMinimumHeight(Dp(48));

                    BindToggleHaptics(checkbox);

                    checkbox.CheckedChange += (_, e) =>
                        NotificationWhisperPreferences
                            .SetPackageEnabled(
                                this, package, e.IsChecked);

                    group.AddView(checkbox, Match());

                    string filter =
                        NotificationWhisperPreferences
                            .GetTitleFilter(this, package);

                    string description = filter.Length == 0
                        ? "Any sender or title"
                        : "Title contains: " + filter;

                    var filterButton = Text(
                        description + "  ·  Edit",
                        12,
                        color: palette.Muted);

                    filterButton.SetPadding(
                        Dp(36), Dp(3), 0, Dp(12));

                    TouchFeedback(filterButton);

                    filterButton.Click += (_, _) =>
                        EditNotificationWhisperFilter(
                            package,
                            name,
                            Rebuild);

                    group.AddView(filterButton, Match());

                    list.AddView(group, Match());
                }
            }

            search.TextChanged += (_, _) =>
            {
                query = search.Text ?? "";
                Rebuild();
            };

            Rebuild();
        },
        (footer, dialog) =>
        {
            footer.AddView(
                Button(
                    "Done",
                    () =>
                    {
                        dialog.Dismiss();
                        RenderBody();
                    },
                    true),
                Match());
        });
    }

    private void EditNotificationWhisperFilter(
        string package,
        string appName,
        Action refresh)
    {
        EditText? input = null;

        Sheet("Whisper filter · " + appName, (panel, dialog) =>
        {
            Paragraph(
                "Only whisper notifications when the sender or " +
                "notification title contains this text. " +
                "Leave blank to allow all notifications from this app.",
                panel);

            input = new EditText(this)
            {
                Text = NotificationWhisperPreferences
                    .GetTitleFilter(this, package),
                Hint = "e.g. Danielle",
                TextSize = 16,
                Typeface = regular,
                Background = Background(palette.Card, 12)
            };

            input.SetSingleLine(true);
            input.SetTextColor(palette.Text);
            input.SetHintTextColor(palette.Muted);
            input.SetPadding(
                Dp(14), Dp(12), Dp(14), Dp(12));

            StyleEntry(input);
            panel.AddView(input, Match());
        },
        (footer, dialog) =>
        {
            DialogActions(
                footer,
                ("Cancel", dialog.Dismiss),
                ("Save", () =>
                {
                    NotificationWhisperPreferences.SetTitleFilter(
                        this,
                        package,
                        input?.Text ?? "");

                    dialog.Dismiss();
                    refresh();
                }
            ));
        });
    }
}
