using Android.Content;
using LauncherApp.Services;

namespace LauncherApp;

public partial class MainActivity
{
    private void RenderGrayscaleSetup()
    {
        if (LauncherDistribution.IsPlay)
        {
            Paragraph(Localize(Resource.String.ui_play_optional_services_unavailable));
            return;
        }
        Title(Localize(Resource.String.ui_app_grayscale));
        ToggleRow(body, Localize(Resource.String.ui_grayscale_whole_display), GrayscaleService.Global(this), value =>
        {
            if (!GrayscaleService.Ready(this))
            {
                Toast(Localize(Resource.String.ui_complete_both_setup_steps_first));
                RenderBody();
                return;
            }
            GrayscaleService.SetGlobal(this, value);
        });
        Paragraph(Localize(Resource.String.ui_choose_an_apps_display_colour_by_holding_its_tile));
        SettingRow(body, Localize(Resource.String.ui_1_foreground_app_detection), GrayscaleService.IsEnabled(this) ? Localize(Resource.String.ui_enabled) : Localize(Resource.String.ui_enable_launcher_app_grayscale_in_android_accessibility_settings), () =>
            Confirm(Localize(Resource.String.ui_foreground_app_detection), Localize(Resource.String.ui_launcher_uses_window_change_events_to_identify_the_app), Localize(Resource.String.ui_open_android_settings), () => OpenIntent(new Intent(Android.Provider.Settings.ActionAccessibilitySettings))), "settings");
        SettingRow(body, Localize(Resource.String.ui_2_display_permission), GrayscaleService.HasSystemAccess(this) ? Localize(Resource.String.ui_granted) : Localize(Resource.String.ui_one_time_permission_from_your_computer), () =>
            Sheet(Localize(Resource.String.ui_display_permission), (panel, dialog) =>
            {
                Paragraph(Localize(Resource.String.ui_connect_the_phone_to_your_computer_with_usb_debugging), panel);
                var command = Text(Localize(Resource.String.ui_adb_shell_pm_grant_com_launcher_home_android_permission), 14);
                command.SetTextIsSelectable(true);
                panel.AddView(command, Match());
                Paragraph(Localize(Resource.String.ui_then_return_here_android_does_not_offer_a_normal), panel);
                panel.AddView(Button(Localize(Resource.String.ui_copy_command), () =>
                {
                    ((ClipboardManager)GetSystemService(ClipboardService)!).PrimaryClip = ClipData.NewPlainText("Launcher display permission", command.Text);
                    Toast(Localize(Resource.String.ui_command_copied));
                }), Match());
            }), "settings");
        Gap(body);
        Paragraph(GrayscaleService.Ready(this) ? Localize(Resource.String.ui_setup_complete_per_app_grayscale_choices_are_ready_for) : Localize(Resource.String.ui_both_setup_steps_are_needed_before_a_saved_grayscale));
        Paragraph(Localize(Resource.String.ui_colour_correction_applies_to_the_whole_display_while_the));
        body.AddView(Button(Localize(Resource.String.ui_turn_off_service_and_restore_colours), () => { GrayscaleService.StopAndRestore(); Render(); }), Match());
        Gap(body);
        body.AddView(Button(Localize(Resource.String.ui_android_colour_correction), () => OpenIntent(new Intent(Android.Provider.Settings.ActionAccessibilitySettings))), Match());
    }
}
