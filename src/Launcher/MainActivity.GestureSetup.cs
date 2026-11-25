using Android.Content;
using LauncherApp.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace LauncherApp
{
    public partial class MainActivity : Activity
    {
        private void ShowGestureSetup() => Sheet(Localize(Resource.String.ui_screen_gestures), (panel, dialog) =>
        {
            Paragraph(
                Localize(Resource.String.ui_configure_how_launcher_responds_to_gestures_on_your_home) +
                Localize(Resource.String.ui_gestures_only_work_within_launcher_and_do_not_interfere),
                panel);

            SectionCaption(panel, Localize(Resource.String.ui_screen_locking));

            ToggleRow(
                panel,
                Localize(Resource.String.ui_double_tap_to_lock_screen),
                state.DoubleTapToLock,
                enabled =>
                {
                    state.DoubleTapToLock = enabled;
                    Save();

                    if (enabled && !SystemPanelService.IsEnabled(this))
                    {
                        Toast(Localize(Resource.String.ui_enable_launcher_accessibility_access_to_use_screen_locking));
                    }
                });

            Paragraph(
                Localize(Resource.String.ui_double_tap_an_empty_area_on_the_today_or) +
                Localize(Resource.String.ui_tapping_app_icons_widgets_and_other_controls_will_continue),
                panel);

            Gap(panel, 8);

            SectionCaption(panel, Localize(Resource.String.ui_required_access));

            bool accessibilityEnabled = SystemPanelService.IsEnabled(this);

            SettingRow(
                panel,
                Localize(Resource.String.ui_launcher_accessibility),
                accessibilityEnabled
                    ? Localize(Resource.String.ui_connected_screen_locking_available)
                    : Localize(Resource.String.ui_not_connected_required_for_screen_locking),
                () =>
                {
                    OpenIntent(new Intent(
                        Android.Provider.Settings.ActionAccessibilitySettings));
                },
                "lock");

            Paragraph(
                accessibilityEnabled
                    ? Localize(Resource.String.ui_launcher_can_request_android_to_lock_the_screen_using)
                    : Localize(Resource.String.ui_android_requires_accessibility_access_to_lock_the_screen) +
                      Localize(Resource.String.ui_open_accessibility_settings_and_enable_launcher_system_panel) +
                      Localize(Resource.String.ui_you_do_not_need_to_enable_the_system_panel),
                panel);

            Gap(panel, 8);

            if (accessibilityEnabled && state.DoubleTapToLock)
            {
                panel.AddView(
                    Button(
                        Localize(Resource.String.ui_test_screen_lock),
                        () =>
                        {
                            dialog.Dismiss();

                            if (!SystemPanelService.TryLockScreen())
                            {
                                Toast(Localize(Resource.String.ui_unable_to_lock_the_screen_check_launcher_accessibility_access));
                            }
                        }),
                    Match());
            }

        }, (footer, dialog) =>
        {
            footer.AddView(
                Button(
                    Localize(Resource.String.ui_done),
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
