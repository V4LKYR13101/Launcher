using Android.Content;
using LauncherApp.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace LauncherApp
{
    public partial class MainActivity : Activity
    {
        private void ShowGestureSetup() => Sheet("Screen gestures", (panel, dialog) =>
        {
            Paragraph(
                "Configure how Launcher responds to gestures on your home screen. " +
                "Gestures only work within Launcher and do not interfere with Android's system gestures.",
                panel);

            SectionCaption(panel, "Screen locking");

            ToggleRow(
                panel,
                "Double-tap to lock screen",
                state.DoubleTapToLock,
                enabled =>
                {
                    state.DoubleTapToLock = enabled;
                    Save();

                    if (enabled && !SystemPanelService.IsEnabled(this))
                    {
                        Toast("Enable Launcher accessibility access to use screen locking.");
                    }
                });

            Paragraph(
                "Double-tap an *empty area* on the Today or Apps screen to lock your phone. " +
                "Tapping app icons, widgets and other controls will continue working normally.",
                panel);

            Gap(panel, 8);

            SectionCaption(panel, "Required access");

            bool accessibilityEnabled = SystemPanelService.IsEnabled(this);

            SettingRow(
                panel,
                "Launcher accessibility",
                accessibilityEnabled
                    ? "Connected · screen locking available"
                    : "Not connected · required for screen locking",
                () =>
                {
                    OpenIntent(new Intent(
                        Android.Provider.Settings.ActionAccessibilitySettings));
                },
                "lock");

            Paragraph(
                accessibilityEnabled
                    ? "Launcher can request Android to lock the screen using its existing accessibility service."
                    : "Android requires accessibility access to lock the screen. " +
                      "Open Accessibility settings and enable *Launcher system panel*. " +
                      "You do not need to enable the system panel overlay.",
                panel);

            Gap(panel, 8);

            if (accessibilityEnabled && state.DoubleTapToLock)
            {
                panel.AddView(
                    Button(
                        "Test screen lock",
                        () =>
                        {
                            dialog.Dismiss();

                            if (!SystemPanelService.TryLockScreen())
                            {
                                Toast("Unable to lock the screen. Check Launcher accessibility access.");
                            }
                        }),
                    Match());
            }

        }, (footer, dialog) =>
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
}
