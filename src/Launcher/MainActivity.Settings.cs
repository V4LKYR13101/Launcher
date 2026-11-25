using Android.Graphics.Drawables;
using Android.App;
using Android.App.Roles;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.UI;
using Uri = Android.Net.Uri;
using LauncherApp.Services.Speech;

namespace LauncherApp;

public partial class MainActivity
{
    private int matchingSettings;
    private void SettingRow(LinearLayout parent, string title, string detail, Action action, string glyph = "more", string searchTerms = "")
    {
        if (ReferenceEquals(parent, body) &&
            !SettingMatches(title + " " + detail + " " + searchTerms + " " + SettingsAliases(title)))
            return;

        if (ReferenceEquals(parent, body))
            matchingSettings++;

        var row = Stack(true);
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetMinimumHeight(Dp(64));
        bool showChevron = parent is SettingsCard || ReferenceEquals(parent, body) && FilteringSettings;
        if (showChevron)
        {
            var leadingIcon = new GlyphView(this, glyph, palette.Muted) { ImportantForAccessibility = ImportantForAccessibility.No };
            row.AddView(leadingIcon, new LinearLayout.LayoutParams(Dp(22), Dp(22)) { MarginEnd = Dp(12) });
        }

        var labels = Stack();
        labels.SetPadding(0, Dp(10), Dp(10), Dp(10));

        // Title with optional accent-coloured words
        var titleView = Text(title, 15, true);
        ApplyAccentMarkers(titleView, title);

        labels.AddView(titleView, Match());

        if (detail.Length > 0)
        {
            // Description with optional accent-coloured words
            var sub = Text(detail, 12, color: palette.Muted);

            ApplyAccentMarkers(sub, detail);

            sub.SetPadding(0, Dp(4), 0, 0);
            sub.SetMaxLines(2);
            sub.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            sub.SetLineSpacing(Dp(2), 1);

            labels.AddView(sub, Match());
        }

        row.AddView(
            labels,
            new LinearLayout.LayoutParams(0, -2, 1)
        );

        row.AddView(
            new GlyphView(this, showChevron ? "chevron" : glyph, palette.Muted) { ImportantForAccessibility = ImportantForAccessibility.No },
            new LinearLayout.LayoutParams(Dp(18), Dp(18))
        );

        TouchFeedback(row);

        row.Click += (_, _) => action();

        parent.AddView(row, Match());
    }

    private void ToggleRow(LinearLayout parent, string title, bool value, Action<bool> action, string searchTerms = "")
    {
        if (ReferenceEquals(parent, body) && !SettingMatches(title + " " + searchTerms))
            return;
        if (ReferenceEquals(parent, body))
            matchingSettings++;
        var toggle = new Switch(this) { Text = title, Checked = value, TextSize = 14, Typeface = regular };
        toggle.ThumbTintList = AccentTint();
        toggle.TrackTintList = SwitchTrackTint();
        toggle.SetTextColor(new Android.Content.Res.ColorStateList([new[] { -Android.Resource.Attribute.StateEnabled }, Array.Empty<int>()], [palette.Muted.ToArgb(), palette.Text.ToArgb()]));
        toggle.SetPadding(0, Dp(8), 0, Dp(8));
        toggle.SetMinimumHeight(Dp(52));
        BindToggleHaptics(toggle);
        toggle.CheckedChange += (_, e) => action(e.IsChecked);
        parent.AddView(toggle, Match());
    }

    private int SelectedChoice(int title) => title switch
    {
        Resource.String.ui_theme => state.FollowSystemTheme ? 3 : Array.IndexOf(new[] { "Dark", "Light", "Sun" }, state.Theme),
        Resource.String.ui_columns => state.Columns - 3,
        Resource.String.ui_tile_height => Array.IndexOf(new[] { 88, 100, 112, 132, 152 }, state.TileHeight),
        Resource.String.ui_wallpaper_dimming => Array.IndexOf(new[] { 35, 50, 65, 80 }, state.WallpaperDim),
        Resource.String.ui_tile_corners => Array.IndexOf(new[] { 0, 6, 12, 20, 28 }, state.CornerRadius),
        Resource.String.ui_icon_style => Array.IndexOf(new[] { "Monochrome", "Grayscale", "Original" }, state.IconStyle),
        Resource.String.ui_drawer_alignment => Array.IndexOf(new[] { "Top", "Center", "Bottom" }, state.Alignment),
        Resource.String.ui_temperature_units => state.Weather.Fahrenheit ? 1 : 0,
        Resource.String.ui_weather_refresh => Array.IndexOf(new[] { 0, 30, 60, 180, 360 }, state.Weather.RefreshMinutes),
        Resource.String.ui_status_row_position => state.StatusPlacement == "Top" ? 0 : 1,
        Resource.String.ui_switching_delay => Array.IndexOf(new[] { 3, 5, 10, 20 }, state.LightThemeDelaySeconds),
        _ => -1
    };
    private void Choose(int title, string[] options, Action<int> action, int selected = -1) =>
        Choose(Localize(title), options, action, selected >= 0 ? selected : SelectedChoice(title));

    private void Choose(string title, string[] options, Action<int> action, int selected = -1) => Sheet(title, (panel, dialog) =>
    {
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            bool active = i == selected;
            var option = Text(options[i] + (active ? "   ✓" : ""), 15, active, active ? palette.Accent : palette.Text);
            option.Gravity = GravityFlags.Start | GravityFlags.CenterVertical;
            option.SetMinimumHeight(Dp(52));
            option.SetPadding(Dp(14), Dp(14), Dp(14), Dp(14));
            option.Selected = active;
            option.ContentDescription = options[i] + (active ? Localize(Resource.String.ui_selected) : "");
            if (active)
                option.Background = Background(new Color(palette.Accent.R, palette.Accent.G, palette.Accent.B, (byte)20), 12);
            TouchFeedback(option, 10);
            option.Click += (_, _) => { dialog.Dismiss(); action(index); };
            panel.AddView(option, Match());
            if (i < options.Length - 1)
                panel.AddView(new View(this) { Background = new ColorDrawable(palette.Divider) }, Match(Dp(1)));
        }
    });

    private void Prompt(int title, string initial, Action<string> action)
    {
        EditText? input = null;
        var dialog = Sheet(Localize(title), (panel, sheet) =>
        {
            bool weather = title == Resource.String.ui_weather_location;
            bool secret = title is Resource.String.ui_brave_search_api_key or Resource.String.ui_youtube_data_api_key;
            bool lux = title is Resource.String.ui_light_threshold_lux or Resource.String.ui_dark_threshold_lux;
            bool feedCount = title == Resource.String.ui_articles_to_display;
            bool duration = title == Resource.String.ui_timer_duration, address = title == Resource.String.ui_desktop_companion_address, code = title == Resource.String.ui_pairing_code, colour = title == Resource.String.ui_accent_rrggbb;
            string help = feedCount ? Localize(Resource.String.ui_choose_how_many_stories_appear_initially_from_1_to) : lux ? Localize(Resource.String.ui_enter_the_sensor_level_in_lux_the_light_threshold) : weather ? Localize(Resource.String.ui_search_for_a_city_or_town_2) : secret ? Localize(Resource.String.ui_paste_your_provider_key_leave_this_blank_to_remove) : duration ? Localize(Resource.String.ui_use_a_duration_such_as_10_min_30s_or) : address ? Localize(Resource.String.ui_enter_the_address_shown_by_launcher_companion) : code ? Localize(Resource.String.ui_enter_the_eight_digit_code_shown_on_your_computer) : colour ? Localize(Resource.String.ui_use_a_six_digit_hex_colour_such_as_e4ff00) : title is Resource.String.ui_web_query or Resource.String.ui_youtube_query ? Localize(Resource.String.ui_enter_the_search_or_value_for_this_card) : Localize(Resource.String.ui_enter_a_name_below);
            Paragraph(help, panel);
            var label = Text(feedCount ? Localize(Resource.String.ui_story_count_upper) : lux ? Localize(Resource.String.ui_light_level_lux_upper) : weather ? Localize(Resource.String.ui_city_upper) : secret ? Localize(Resource.String.ui_api_key_upper) : duration ? Localize(Resource.String.ui_duration_upper) : address ? Localize(Resource.String.ui_address_upper) : code ? Localize(Resource.String.ui_pairing_code_upper) : colour ? Localize(Resource.String.ui_hex_colour_upper) : Localize(Resource.String.ui_name_upper), 10, true, palette.Muted);
            label.SetPadding(Dp(4), 0, 0, Dp(8));
            panel.AddView(label, Match());
            input = new EditText(this) { Text = initial, Hint = weather ? Localize(Resource.String.ui_e_g_cape_town_south_africa) : title == Resource.String.ui_new_drawer ? Localize(Resource.String.ui_e_g_daily_essentials) : Localize(title), TextSize = 18, Typeface = regular };
            input.SetSingleLine(true);
            if (lux)
                input.InputType = Android.Text.InputTypes.ClassNumber | Android.Text.InputTypes.NumberFlagDecimal;
            else if (secret)
                input.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationPassword;
            else if (address)
                input.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationUri;
            else if (code || feedCount)
                input.InputType = Android.Text.InputTypes.ClassNumber;
            input.Typeface = regular;
            input.SetTextColor(palette.Text);
            input.SetHintTextColor(palette.Muted);
            input.Background = Background(palette.Card, 12, palette.Divider);
            input.SetPadding(Dp(14), Dp(12), Dp(14), Dp(12));
            if (weather)
                StyleSearchEntry(input, 12, inSheet: true);
            else
                StyleEntry(input);
            input.SetSelection(initial.Length);
            panel.AddView(input, Match(Math.Max(Dp(56), Dp(18 * FontScale * 1.3f + 24))));
            Gap(panel, 6);
        }, (actions, sheet) =>
        {
            DialogActions(actions, (Localize(Resource.String.ui_cancel), sheet.Dismiss), (title == Resource.String.ui_weather_location ? Localize(Resource.String.ui_search) : title == Resource.String.ui_new_drawer ? Localize(Resource.String.ui_create_drawer) : Localize(Resource.String.ui_save), () =>
            {
                string value = input?.Text ?? "", error = ValidatePrompt(title, value);
                if (error.Length > 0)
                {
                    input!.Error = error;
                    input.RequestFocus();
                    return;
                }
                sheet.Dismiss();
                action(value);
            }
            ));
        });
        input!.RequestFocus();
        input.PostDelayed(() => ((Android.Views.InputMethods.InputMethodManager?)GetSystemService(InputMethodService))?.ShowSoftInput(input, Android.Views.InputMethods.ShowFlags.Implicit), 180);
    }

    private string ValidatePrompt(int title, string value)
    {
        string text = value.Trim();
        if (title is Resource.String.ui_brave_search_api_key or Resource.String.ui_youtube_data_api_key || title == Resource.String.ui_search_aliases)
            return "";
        if (title == Resource.String.ui_accent_rrggbb)
            return System.Text.RegularExpressions.Regex.IsMatch(text, "^#[0-9a-fA-F]{6}$") ? "" : Localize(Resource.String.ui_use_six_hex_digits_such_as_e4ff00);
        if (title == Resource.String.ui_articles_to_display)
            return int.TryParse(text, System.Globalization.NumberStyles.Integer, UiCulture, out int count) && count is >= 1 and <= 50 ? "" : Localize(Resource.String.ui_choose_a_number_from_1_to_50);
        if (title is Resource.String.ui_light_threshold_lux or Resource.String.ui_dark_threshold_lux)
        {
            if ((!double.TryParse(text, System.Globalization.NumberStyles.Float, UiCulture, out double lux) && !double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lux)) || !double.IsFinite(lux) || lux is < 0 or > 200000)
                return Localize(Resource.String.ui_enter_a_light_level_from_0_to_200000_lux);
            return title == Resource.String.ui_light_threshold_lux && lux <= state.DarkThemeLux || title == Resource.String.ui_dark_threshold_lux && lux >= state.LightThemeLux ? Localize(Resource.String.ui_the_light_threshold_must_be_higher_than_the_dark) : "";
        }
        if (title == Resource.String.ui_timer_duration)
        {
            try
            {
                RootCommand.TimerDuration(text);
                return "";
            }
            catch (ArgumentException e) { return PromptError(e); }
        }
        if (title == Resource.String.ui_desktop_companion_address)
        {
            try
            {
                CompanionLink.ValidateAddress(text);
                return "";
            }
            catch (ArgumentException e) { return PromptError(e); }
        }
        if (title == Resource.String.ui_pairing_code)
            return text.Length == 8 && text.All(char.IsAsciiDigit) ? "" : Localize(Resource.String.ui_enter_the_eight_digit_code_shown_on_the_computer);
        if (title == Resource.String.ui_weather_location)
            return text.Length >= 2 ? "" : Localize(Resource.String.ui_enter_at_least_two_characters);
        if (title == Resource.String.ui_save_a_link)
            return OnlineCards.SafeLink(text) ? "" : Localize(Resource.String.ui_enter_a_full_http_or_https_link);
        return text.Length == 0 ? Localize(Resource.String.ui_enter_a_value_before_saving) : "";
    }

    private void Confirm(string title, string message, string actionLabel, Action action) => Sheet(title, (panel, dialog) => Paragraph(message, panel), (footer, dialog) =>
    {
        DialogActions(footer, (Localize(Resource.String.ui_cancel), dialog.Dismiss), (actionLabel, () => { dialog.Dismiss(); action(); }
        ));
    });

    private void QuickControls()
    {
        Sheet(Localize(Resource.String.ui_launcher), (panel, dialog) =>
        {
            void Go(Action action)
            {
                dialog.Dismiss();
                action();
            }
            var navigation = Stack(FontScale < 1.4f);
            foreach (var (label, target) in new[] { ("Today", "Root"), ("Apps", "Tiles"), ("Notifications", "Conversations") })
            {
                var lp = FontScale >= 1.4f ? Match() : new LinearLayout.LayoutParams(0, -1, 1);
                lp.SetMargins(Dp(3), 0, Dp(3), 0);
                navigation.AddView(Button(label, () => Go(() => Navigate(target))), lp);
            }
            panel.AddView(navigation, Match());
            Gap(panel, 8);
            SettingRow(panel, Localize(Resource.String.ui_light), state.Theme == "Light" ? Localize(Resource.String.ui_on) : Localize(Resource.String.ui_soft_light_theme), () => Go(() => { ManualTheme("Light"); }), "sun");
            SettingRow(panel, Localize(Resource.String.ui_sun), state.Theme == "Sun" ? Localize(Resource.String.ui_on) : Localize(Resource.String.ui_high_contrast_outdoors), () => Go(() => { ManualTheme("Sun"); }), "sun");
            SettingRow(panel, Localize(Resource.String.ui_dark), state.Theme == "Dark" ? Localize(Resource.String.ui_on) : Localize(Resource.String.ui_quiet_dark_surfaces), () => Go(() => { ManualTheme("Dark"); }), "focus");
            SettingRow(panel, Localize(Resource.String.ui_accent), state.Accent, () => Go(ShowAccentChoice), "photo");
            SettingRow(panel, Localize(Resource.String.ui_quick_access), Localize(Resource.String.ui_choose_the_apps_on_your_home_screen), () => Go(ShowQuickAccess), "pin");
            SettingRow(panel, Localize(Resource.String.ui_focus), state.Focus ? Localize(Resource.String.ui_on_tap_to_turn_off) : Localize(Resource.String.ui_quiet_outlined_tiles), () => Go(() => { state.Focus = !state.Focus; state.FocusEndsAt = 0; Save(); Render(); }), "focus");
            SettingRow(panel, page == "Root" ? Localize(Resource.String.ui_add_widget) : Localize(Resource.String.ui_add_drawer), "", () => Go(() => { if (page == "Root") AddWidget(); else NewDrawer(); }), "plus");
            SettingRow(panel, Localize(Resource.String.ui_all_apps), Localize(Resource.String.ui_search_your_installed_apps), () => Go(() => { page = "Tiles"; StartSearch(); }), "grid");
            SettingRow(panel, Localize(Resource.String.ui_settings), Localize(Resource.String.ui_make_launcher_your_own), () => Go(() => Navigate("Settings")), "settings");
        });
    }

    private sealed class SettingsCard(Context context) : LinearLayout(context) { }

    private LinearLayout AddSettingsCard(string? title = null)
    {
        if (title is not null)
            SectionCaption(body, title);
        var card = new SettingsCard(this) { Orientation = Orientation.Vertical };
        card.SetPaddingRelative(Dp(14), Dp(4), Dp(14), Dp(4));
        if (!ApplyGlassCard(card))
            card.Background = Surface(palette.Card, 22);
        body.AddView(card, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(6) });
        return card;
    }

    private void RenderSettings()
    {
        matchingSettings = 0;

        if (FilteringSettings)
        {
            RenderSettingsSearchResults();
            return;
        }

        var personal = AddSettingsCard(Localize(Resource.String.ui_make_it_yours));
        AddLanguageSetting(personal);
        SettingRow(personal, Localize(Resource.String.ui_appearance), (state.FollowSystemTheme ? Localize(Resource.String.ui_follow_android) : AppearanceValue(state.Theme)) +
            Localize(Resource.String.ui_value_columns, state.Columns), () => Navigate("Appearance"), "sun");
        SettingRow(personal, Localize(Resource.String.ui_edit_home_layout), Localize(Resource.String.ui_arrange_tiles_and_cards_undo_changes), ShowLayoutEditor, "grid");
        SettingRow(personal, Localize(Resource.String.ui_apps_drawers), Localize(Resource.String.ui_value_value_hidden, Counted(state.Drawers.Count, Resource.Plurals.drawer), HiddenAppCount),
            () => Navigate("Apps & drawers"), "grid");
        AddGestureSetting(personal);

        var features = AddSettingsCard(Localize(Resource.String.ui_launcher_features));
        AddSearchSettingsLink(features);
        AddDownloadsSettingsLink(features);
        SettingRow(features, Localize(Resource.String.ui_cards_feeds_weather), Localize(Resource.String.ui_value_news_sources_today_cards_and_widgets, Sources.Count(s => s.Enabled)),
            () => ShowSettingsCategory(Localize(Resource.String.ui_cards_feeds_weather), AddContentSettings), "rss");
        SettingRow(features, Localize(Resource.String.ui_notifications_panels), ConversationService.Enabled(this) ? Localize(Resource.String.ui_notification_access_on) : Localize(Resource.String.ui_notification_access_off),
            () => Navigate("Notifications & panels"), "message");
        AddSpeechSettingsLink(features);

        var device = AddSettingsCard(Localize(Resource.String.ui_device_data));
        AddWearSettingsLink(device);
        SettingRow(device, Localize(Resource.String.ui_setup_permissions), IsDefaultHome() ? Localize(Resource.String.ui_default_home_set_device_access) : Localize(Resource.String.ui_set_default_home_device_access),
            () => ShowSettingsCategory(Localize(Resource.String.ui_setup_permissions), AddDeviceSettings), "phone");
        SettingRow(device, Localize(Resource.String.ui_privacy), Localize(Resource.String.ui_protected_previews_and_message_history), ShowPrivacyRecovery, "lock");
        SettingRow(device, Localize(Resource.String.ui_backup_restore), Localize(Resource.String.ui_export_or_restore_your_layout), () => ShowSettingsCategory(Localize(Resource.String.ui_backup_restore), AddBackupSettings), "file");
        SettingRow(device, Localize(Resource.String.ui_app_updates), Localize(Resource.String.ui_check_for_a_new_launcher_version), () => Navigate("Updates"), "down");

        var support = AddSettingsCard(Localize(Resource.String.ui_help));
        SettingRow(support, Localize(Resource.String.ui_how_to_use_launcher), Localize(Resource.String.ui_gestures_drawers_and_customisation), ShowGuide, "note");
        Gap(body);
        Paragraph(Localize(Resource.String.ui_launcher_3) + InstalledPackage().VersionName + Localize(Resource.String.ui_built_by_kevin), null, TextAlignment.Center);
    }

    private void AddGestureSetting(LinearLayout parent) =>
        SettingRow(parent, Localize(Resource.String.ui_gestures), Localize(Resource.String.ui_double_tap_to_lock) + (state.DoubleTapToLock ? Localize(Resource.String.ui_on_2) : Localize(Resource.String.ui_off_2)),
            ShowGestureSetup, "phone", Localize(Resource.String.ui_screen_lock_tap_double_tap_accessibility));

    private void AddWearSettingsLink(LinearLayout parent) =>
        SettingRow(parent, Localize(Resource.String.ui_wear_os), Localize(Resource.String.ui_set_up_and_install_launcher_roll_on_your_watch),
            () => Navigate(WearSettingsPage), "clock", Localize(Resource.String.ui_watch_watchface_watch_face_companion_install));

    // Dismiss category sheets before opening a second screen or dialog.
    // Switches remain in place so several related choices can be changed together.
    private void ShowSettingsCategory(string title, Action<LinearLayout, Action<Action>> addRows) =>
        Sheet(title, (panel, dialog) =>
        {
            void Go(Action next)
            {
                dialog.Dismiss();
                next();
            }
            addRows(panel, Go);
        });

    private void SettingsExplanation(LinearLayout parent, string description)
    {
        if (!ReferenceEquals(parent, body) || !FilteringSettings)
            Paragraph(description, parent);
    }

    private int HiddenAppCount => apps.Count(app => !app.PrivateProfile && state.Apps[app.Id].Hidden);

    private void RenderAppsAndDrawersSettings()
    {
        AddAppOrganisationSettings(AddSettingsCard(Localize(Resource.String.ui_organise_apps)));
        AddFocusSetting(AddSettingsCard(Localize(Resource.String.ui_focus)));
    }

    private void AddAppOrganisationSettings(LinearLayout parent)
    {
        SettingRow(parent, Localize(Resource.String.ui_edit_home_layout), Localize(Resource.String.ui_arrange_tiles_and_cards_undo_changes), ShowLayoutEditor, "grid");
        SettingRow(parent, Localize(Resource.String.ui_manage_drawers), Counted(state.Drawers.Count, Resource.Plurals.drawer) + Localize(Resource.String.ui_create_rename_or_reorder),
            () => Navigate("Drawers"), "grid");
        SettingRow(parent, Localize(Resource.String.ui_hidden_apps), Counted(HiddenAppCount, Resource.Plurals.app) + Localize(Resource.String.ui_hidden_restore_visibility),
            () => Navigate("Hidden"), "app");
        SettingRow(parent, Localize(Resource.String.ui_quick_access), Localize(Resource.String.ui_choose_up_to_five_apps_for_the_home_dock), ShowQuickAccess, "pin", Localize(Resource.String.ui_favorites_favourites_pinned_apps));
    }

    private void AddFocusSetting(LinearLayout parent)
    {
        ToggleRow(parent, Localize(Resource.String.ui_focus_mode), state.Focus, enabled =>
        {
            state.Focus = enabled;
            if (!enabled) state.FocusEndsAt = 0;
            Save();
        }, Localize(Resource.String.ui_quiet_outlined_tiles_distraction));
        SettingsExplanation(parent, Localize(Resource.String.ui_focus_gives_your_app_tiles_a_quieter_outlined_look));
    }

    private void AddContentSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, Localize(Resource.String.ui_home_cards_widgets), Localize(Resource.String.ui_notes_cards_and_android_widgets),
            () => go(() => Navigate("Root settings")), "root");
        SettingRow(parent, Localize(Resource.String.ui_news_feeds), Counted(Sources.Count(s => s.Enabled), Resource.Plurals.source) + " · " +
            Counted(state.FeedArticleCount, Resource.Plurals.story) + Localize(state.FeedImages ? Resource.String.ui_feed_images_on : Resource.String.ui_feed_images_off),
            () => go(ManageFeeds), "rss");
        SettingRow(parent, Localize(Resource.String.ui_saved_stories), Counted(state.SavedArticles.Count, Resource.Plurals.saved_story),
            () => go(ShowSavedArticles), "rss");
        SettingRow(parent, Localize(Resource.String.ui_weather), string.IsNullOrEmpty(state.Weather.City) ? Localize(Resource.String.ui_choose_city_and_units) : state.Weather.City,
            () => go(() => Navigate("Weather")), "cloud");
        SettingRow(parent, Localize(Resource.String.ui_online_cards), Localize(Resource.String.ui_rss_web_search_and_youtube),
            () => go(() => Navigate("Integrations")), "link");
    }

    private void AddSpeechSettingsLink(LinearLayout parent) =>
        SettingRow(parent, PageLabel(SpeechSettingsPage), KokoroModelInstaller.IsInstalled(this)
                ? Localize(Resource.String.ui_offline_voice_ready_playback_and_news)
                : Localize(Resource.String.ui_voice_model_not_installed_download_and_playback),
            () => OpenSpeechSettings(), "play", Localize(Resource.String.ui_kokoro_model_files_voice_speech_download_storage));

    private void AddSpeechActions(LinearLayout parent)
    {
        SettingRow(parent, Localize(Resource.String.ui_kokoro_speech_2), Localize(Resource.String.ui_voice_selection_speed_and_offline_playback),
            () => KokoroReaderDialog.Show(this), "play");
        SettingRow(parent, Localize(Resource.String.ui_listen_to_latest_news), Localize(Resource.String.ui_read_rss_headlines_and_summaries_aloud),
            ReadLatestFeedsWithKokoro, "rss");
    }

    private void RenderNotificationSettings(LinearLayout? searchResults = null)
    {
        LinearLayout Group(string title) => searchResults ?? AddSettingsCard(title);

        AddNotificationAccessSetting(Group(Localize(Resource.String.ui_permissions)));

        var sources = Group(Localize(Resource.String.ui_sources_panels));
        SettingRow(sources, Localize(Resource.String.ui_notification_sources), state.ConversationMessagesOnly ? Localize(Resource.String.ui_messages_only_choose_apps) : Localize(Resource.String.ui_all_notification_types_choose_apps),
            PickConversationSources, "filter", Localize(Resource.String.ui_conversations_messages_filters));
        SettingRow(sources, Localize(Resource.String.ui_quick_panel), Localize(Resource.String.ui_open_launcher_controls_and_current_notifications),
            () => Navigate("Quick panel"), "down");

        SettingRow(Group(Localize(Resource.String.ui_history)), Localize(Resource.String.ui_message_history), state.KeepMessageHistory ? Localize(Resource.String.ui_on_saving_new_messages) : Localize(Resource.String.ui_off_optional_local_history),
            MessageHistorySettings, "message");

        var alerts = Group(Localize(Resource.String.ui_alerts));
        SettingRow(alerts, Localize(Resource.String.ui_notification_whispers), NotificationWhisperPreferences.Enabled(this)
                ? Localize(Resource.String.ui_on_quiet_spoken_announcements) : Localize(Resource.String.ui_off_choose_apps_and_senders),
            ShowNotificationWhisperSettings, "message", Localize(Resource.String.ui_speech_voice_spoken_alerts));
        SettingRow(alerts, Localize(Resource.String.ui_timer_alerts), Localize(Resource.String.ui_android_notifications_and_exact_alarm_access), OpenTimerPermissions, "clock");
    }

    private bool? notificationAccessBeforeSettings;

    private void AddNotificationAccessSetting(LinearLayout parent)
    {
        bool enabled = ConversationService.Enabled(this);
        SettingRow(parent, Localize(Resource.String.ui_notification_access), enabled ? Localize(Resource.String.ui_on_manage_in_android_settings) : Localize(Resource.String.ui_off_enable_to_show_notifications_in_launcher),
            OpenNotificationAccessSettings, "lock", Localize(Resource.String.ui_permissions_notification_listener_quick_panel_conversations));
    }

    private void OpenNotificationAccessSettings()
    {
        HideKeyboard();
        bool enabled = ConversationService.Enabled(this);
        void OpenAndroidSettings()
        {
            notificationAccessBeforeSettings = enabled;
            OpenIntent(new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS"));
        }

        if (enabled)
            OpenAndroidSettings();
        else
            Confirm(Localize(Resource.String.ui_notification_access), Localize(Resource.String.ui_allow_launcher_to_show_current_notifications_and_message_previews),
                Localize(Resource.String.ui_open_android_settings), OpenAndroidSettings);
    }

    private void AddDeviceSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, Localize(Resource.String.ui_setup_feature_health), Localize(Resource.String.ui_check_access_compatibility_and_recovery),
            () => go(ShowFeatureHealth), "settings");
        SettingRow(parent, Localize(Resource.String.ui_permissions), Localize(Resource.String.ui_default_home_usage_and_notification_access),
            () => go(() => Navigate("Permissions")), "settings");
        SettingRow(parent, Localize(Resource.String.ui_haptic_feedback), state.HapticFeedback == "Off"
                ? Localize(Resource.String.ui_off)
                : state.HapticFeedback + Localize(Resource.String.ui_follows_android_touch_vibration),
            () => go(ShowHapticSettings), "phone");
        SettingRow(parent, Localize(Resource.String.ui_system_panel), state.SystemPanelEnabled
                ? SystemPanelService.IsEnabled(this) ? Localize(Resource.String.ui_on_available_across_apps) : Localize(Resource.String.ui_on_enable_accessibility_access)
                : Localize(Resource.String.ui_off_controls_across_apps),
            () => go(ShowSystemPanelSettings), "phone");
        SettingRow(parent, Localize(Resource.String.ui_app_grayscale), Localize(Resource.String.ui_display_permission_and_foreground_detection),
            () => go(() => Navigate("Grayscale setup")), "photo");
        // Desktop companion intentionally remains hidden until it is ready.
    }

    private void AddMaintenanceSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, Localize(Resource.String.ui_privacy_recovery), Localize(Resource.String.ui_protected_previews_backups_and_data),
            () => go(ShowPrivacyRecovery), "lock");
        SettingRow(parent, Localize(Resource.String.ui_app_updates), Localize(Resource.String.ui_check_download_and_install_from_kevlog),
            () => go(() => Navigate("Updates")), "down");
        AddBackupSettings(parent, go);
        SettingRow(parent, Localize(Resource.String.ui_how_to_use_launcher), Localize(Resource.String.ui_gestures_drawers_tiles_and_privacy),
            () => go(ShowGuide), "note");
    }

    private void AddBackupSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, Localize(Resource.String.ui_back_up_layout), Localize(Resource.String.ui_export_settings_as_a_json_file), () => go(ExportBackup), "file", Localize(Resource.String.ui_backup));
        SettingRow(parent, Localize(Resource.String.ui_restore_layout), Localize(Resource.String.ui_import_a_previously_saved_layout), () => go(ImportBackup), "file");
    }

    private void RenderSettingsSearchResults()
    {
        Action<Action> open = action => action();
        AddLanguageSetting(body);
        AddGestureSetting(body);
        SettingRow(body, Localize(Resource.String.ui_appearance_presets), Localize(Resource.String.ui_classic_and_studio_theme_and_layout),
            () => Navigate("Appearance"), "sun");
        AddGlassSetting(body);
        AddTouchGlowSetting(body);
        AddAppearanceThemeSettings(body, open);
        AddAppearanceTileSettings(body, open);
        AddAppearanceMotionSettings(body, open);
        AddAppearanceNavigationSettings(body, open);
        AddAppOrganisationSettings(body);
        AddFocusSetting(body);
        AddContentSettings(body, open);
        AddSpeechSettingsLink(body);
        AddSpeechActions(body);
        AddImageSearchSetting(body);
        AddDocumentSearchSetting(body);
        AddDownloadsSettingsLink(body);
        RenderNotificationSettings(body);
        AddDeviceSettings(body, open);
        AddWearSettingsLink(body);
        AddMaintenanceSettings(body, open);

        if (matchingSettings == 0)
        {
            var empty = Text(Localize(Resource.String.ui_no_settings_match_value_try_theme_icon_voice_permissions, settingsQuery.Trim()), 14, color: palette.Muted);
            empty.SetPadding(0, Dp(20), 0, Dp(20));
            body.AddView(empty, Match());
        }
    }

    private void RenderAppearance()
    {
        var appearance = AddSettingsCard(Localize(Resource.String.ui_theme_effects));
        SettingRow(appearance, Localize(Resource.String.ui_theme), state.FollowSystemTheme ? Localize(Resource.String.ui_follow_android) : AppearanceValue(state.Theme), ShowThemeChoice, "sun");
        SettingRow(appearance, Localize(Resource.String.ui_accent), state.Accent, ShowAccentChoice, "photo");
        AddGlassSetting(appearance);
        AddTouchGlowSetting(appearance);

        var customisation = AddSettingsCard(Localize(Resource.String.ui_customise));
        SettingRow(customisation, Localize(Resource.String.ui_theme_wallpaper), state.UseWallpaper ? Localize(Resource.String.ui_wallpaper_on_value_dimming, state.WallpaperDim) : Localize(Resource.String.ui_wallpaper_off_automatic_theme_and_dimming),
            () => Navigate("Theme & wallpaper"), "photo");
        SettingRow(customisation, Localize(Resource.String.ui_tiles_icons), Localize(Resource.String.ui_value_columns_value, state.Columns, AppearanceValue(state.IconStyle)),
            () => Navigate("Tiles & icons"), "grid");
        SettingRow(customisation, Localize(Resource.String.ui_motion_depth), state.LayeredInterface ? Localize(Resource.String.ui_layered_interface_on) : Localize(Resource.String.ui_layered_interface_off),
            () => Navigate("Motion & depth"), "focus");
        SettingRow(customisation, Localize(Resource.String.ui_navigation_status), state.ShowHomeNavigation ? Localize(Resource.String.ui_page_tabs_on) : Localize(Resource.String.ui_page_tabs_off),
            () => Navigate("Navigation & status"), "settings");

        var presetCard = AddSettingsCard(Localize(Resource.String.ui_presets));
        var presets = Stack(true);
        presets.SetPadding(0, Dp(6), 0, Dp(6));
        foreach (string preset in new[] { "Classic", "Studio" })
        {
            var choice = preset;
            var button = Button(AppearanceValue(choice), () => Confirm(Localize(Resource.String.ui_apply_value_preset, AppearanceValue(choice)),
                Localize(Resource.String.ui_this_changes_multiple_appearance_settings), Localize(Resource.String.ui_apply_preset), () =>
                {
                    state.AutomaticLightTheme = false;
                    state.ApplyPreset(choice);
                    Save();
                    Render();
                }));
            var lp = new LinearLayout.LayoutParams(0, -2, 1) { MarginEnd = preset == "Classic" ? Dp(8) : 0 };
            presets.AddView(button, lp);
        }
        presetCard.AddView(presets, Match());
    }

    private void RenderAppearanceSection(Action<LinearLayout, Action<Action>> addRows) =>
        addRows(AddSettingsCard(), action => action());

    private void AddTouchGlowSetting(LinearLayout parent) => ToggleRow(parent, Localize(Resource.String.ui_touch_glow), state.TouchGlow, enabled =>
    {
        state.TouchGlow = enabled;
        if (!enabled)
            touchGlow?.Clear();
        Save();
    }, Localize(Resource.String.ui_accent_color_colour_finger_tap_ripple));

    private void ShowThemeChoice() => Choose(Resource.String.ui_theme, [Localize(Resource.String.ui_dark), Localize(Resource.String.ui_light), Localize(Resource.String.ui_sun), Localize(Resource.String.ui_follow_android)], index =>
    {
        if (index == 3)
        {
            state.FollowSystemTheme = true;
            state.AutomaticLightTheme = false;
            SyncSystemTheme();
            Save();
            Render();
        }
        else
            ManualTheme(new[] { "Dark", "Light", "Sun" }[index]);
    });

    private void AddGlassSetting(LinearLayout parent)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            ToggleRow(parent, Localize(Resource.String.ui_liquid_glass_cards), state.LiquidGlassCards,
                value =>
                {
                    state.LiquidGlassCards = value;
                    Save();
                    if (IsStyledSettingsPage(page))
                        Render();
                }, Localize(Resource.String.ui_notifications_today_root_app_tiles_search_translucent_refraction_wallpaper));
    }

    private void AddAppearanceThemeSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, Localize(Resource.String.ui_automatic_theme), state.AutomaticLightTheme ? Localize(Resource.String.ui_enabled_light_sensor) : Localize(Resource.String.ui_off_manual_or_system_theme),
            () => go(() => Navigate("Automatic theme")), "sun");
        SettingRow(parent, Localize(Resource.String.ui_theme), state.FollowSystemTheme ? Localize(Resource.String.ui_follow_android) : AppearanceValue(state.Theme),
            () => go(ShowThemeChoice), "sun");
        SettingRow(parent, Localize(Resource.String.ui_accent), state.Accent, () => go(ShowAccentChoice), "photo");
        SettingRow(parent, Localize(Resource.String.ui_wallpaper_dimming), state.WallpaperDim + Localize(Resource.String.ui_other_screens),
            () => go(() => Choose(Resource.String.ui_wallpaper_dimming, [Localize(Resource.String.ui_subtle_35), Localize(Resource.String.ui_balanced_50), Localize(Resource.String.ui_deep_65), Localize(Resource.String.ui_strong_80)], index =>
            {
                state.WallpaperDim = new[] { 35, 50, 65, 80 }[index];
                Save();
                Render();
            })), "photo");
        ToggleRow(parent, Localize(Resource.String.ui_use_system_wallpaper), state.UseWallpaper, value =>
        {
            state.UseWallpaper = value;
            Save();
            Render();
        }, Localize(Resource.String.ui_other_screens_2));
        ToggleRow(parent, Localize(Resource.String.ui_home_accent_glow), state.HomeAccentGlow, value =>
        {
            state.HomeAccentGlow = value;
            Save();
            Render();
        });
    }

    private void AddAppearanceTileSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, Localize(Resource.String.ui_columns), state.Columns.ToString(),
            () => go(() => Choose(Resource.String.ui_columns, ["3", "4", "5", "6"], index =>
            {
                state.Columns = index + 3;
                Save();
                Render();
            })), "grid");
        SettingRow(parent, Localize(Resource.String.ui_tile_height), state.TileHeight + Localize(Resource.String.ui_dp),
            () => go(() => Choose(Resource.String.ui_tile_height, [Localize(Resource.String.ui_compact_88), Localize(Resource.String.ui_balanced_100), Localize(Resource.String.ui_classic_112), Localize(Resource.String.ui_spacious_132), Localize(Resource.String.ui_large_152)], index =>
            {
                state.TileHeight = new[] { 88, 100, 112, 132, 152 }[index];
                Save();
                Render();
            })));
        SettingRow(parent, Localize(Resource.String.ui_tile_corners), state.CornerRadius + Localize(Resource.String.ui_dp),
            () => go(() => Choose(Resource.String.ui_tile_corners, [Localize(Resource.String.ui_square), Localize(Resource.String.ui_subtle_6), Localize(Resource.String.ui_soft_12), Localize(Resource.String.ui_round_20), Localize(Resource.String.ui_pill_28)], index =>
            {
                state.CornerRadius = new[] { 0, 6, 12, 20, 28 }[index];
                Save();
                Render();
            })));
        SettingRow(parent, Localize(Resource.String.ui_tile_icon_size), state.TileIconScale == 100 ? Localize(Resource.String.ui_default_100) : state.TileIconScale + "%",
            () => go(ShowTileIconSize), "photo");
        SettingRow(parent, Localize(Resource.String.ui_icon_style), AppearanceValue(state.IconStyle),
            () => go(() => Choose(Resource.String.ui_icon_style, [Localize(Resource.String.ui_monochrome), Localize(Resource.String.ui_grayscale), Localize(Resource.String.ui_original)], index =>
            {
                state.IconStyle = new[] { "Monochrome", "Grayscale", "Original" }[index];
                Save();
                Render();
            })), "photo");
        SettingRow(parent, Localize(Resource.String.ui_icon_pack), state.IconPack.Length == 0 ? Localize(Resource.String.ui_apps_own_icons) :
            BuiltinIcons.Libraries.FirstOrDefault(p => p.Package == state.IconPack)?.Label ?? state.IconPack,
            () => go(() => PickIconPack()), "photo");
        ToggleRow(parent, Localize(Resource.String.ui_app_labels), state.ShowLabels, value => { state.ShowLabels = value; Save(); }, Localize(Resource.String.ui_tile_names_text));
        ToggleRow(parent, Localize(Resource.String.ui_show_usage_time), state.ShowUsage, value => { state.ShowUsage = value; Save(); }, Localize(Resource.String.ui_screen_time));
        ToggleRow(parent, Localize(Resource.String.ui_notification_count_badges), state.ShowNotificationBadges, value =>
        {
            state.ShowNotificationBadges = value;
            Save();
        }, Localize(Resource.String.ui_unread_counts_tile_dots));
        ToggleRow(parent, Localize(Resource.String.ui_media_playback_card), state.ShowMediaCard, value =>
        {
            state.ShowMediaCard = value;
            Save();
        }, Localize(Resource.String.ui_music_now_playing));
        SettingsExplanation(parent, Localize(Resource.String.ui_monochrome_uses_themed_app_icons_where_supported_other_app));
    }

    private void AddAppearanceMotionSettings(LinearLayout parent, Action<Action> go)
    {
        Switch? tiltSwitch = null;
        Switch? transitionsSwitch = null;
        ToggleRow(parent, Localize(Resource.String.ui_layered_interface), state.LayeredInterface, value =>
        {
            state.LayeredInterface = value;
            if (!value)
            {
                StopAppLayer();
                EndPageSlideForInterruption();
                StopRootFolds();
                StopDrawerAnimations();
                depthMotion.Stop();
                ResetDeviceDepth();
            }
            Save();
            if (tiltSwitch is not null) tiltSwitch.Enabled = value && depthMotion.Available;
            if (transitionsSwitch is not null) transitionsSwitch.Enabled = value;
            if (ReferenceEquals(parent, body)) RenderBody();
        }, Localize(Resource.String.ui_3d_perspective_card_reveal_parallax_animations));
        SettingsExplanation(parent, Localize(Resource.String.ui_master_switch_for_app_expansion_page_and_drawer_transitions));

        int before = parent.ChildCount;
        ToggleRow(parent, Localize(Resource.String.ui_tilt_depth), state.TiltDepth, value =>
        {
            if (value && !depthMotion.Available)
            {
                Toast(Localize(Resource.String.ui_this_phone_does_not_provide_a_rotation_sensor));
                if (ReferenceEquals(parent, body)) RenderBody();
                return;
            }
            state.TiltDepth = value;
            Save();
        }, Localize(Resource.String.ui_gyroscope_rotation_sensor_motion));
        if (parent.ChildCount > before)
        {
            tiltSwitch = parent.GetChildAt(parent.ChildCount - 1) as Switch;
            if (tiltSwitch is not null) tiltSwitch.Enabled = state.LayeredInterface && depthMotion.Available;
        }
        SettingsExplanation(parent, depthMotion.Available
            ? Localize(Resource.String.ui_subtle_movement_from_the_phone_rotation_sensor_it_pauses)
            : Localize(Resource.String.ui_tilt_depth_is_unavailable_on_this_device_other_effects));

        before = parent.ChildCount;
        ToggleRow(parent, Localize(Resource.String.ui_page_transitions), state.PageTransitions, value =>
        {
            state.PageTransitions = value;
            if (!value) EndPageSlideForInterruption();
            Save();
        }, Localize(Resource.String.ui_animation_switching_pages));
        if (parent.ChildCount > before)
        {
            transitionsSwitch = parent.GetChildAt(parent.ChildCount - 1) as Switch;
            if (transitionsSwitch is not null) transitionsSwitch.Enabled = state.LayeredInterface;
        }
    }

    private void AddAppearanceNavigationSettings(LinearLayout parent, Action<Action> go)
    {
        AddHomeSwipeSetting(parent, go);
        SettingRow(parent, Localize(Resource.String.ui_drawer_alignment), AppearanceValue(state.Alignment),
            () => go(() => Choose(Resource.String.ui_drawer_alignment, [Localize(Resource.String.ui_top), Localize(Resource.String.ui_center), Localize(Resource.String.ui_bottom)], index =>
            {
                state.Alignment = new[] { "Top", "Center", "Bottom" }[index];
                Save();
                Render();
            })));
        ToggleRow(parent, Localize(Resource.String.ui_centered_drawer_headings), state.CenterHeadings, value =>
        {
            state.CenterHeadings = value;
            Save();
        }, Localize(Resource.String.ui_drawer_title_placement));
        ToggleRow(parent, Localize(Resource.String.ui_show_page_tabs), state.ShowHomeNavigation, value =>
        {
            state.ShowHomeNavigation = value;
            Save();
        }, Localize(Resource.String.ui_home_navigation_buttons));
        SettingRow(parent, Localize(Resource.String.ui_page_tab_buttons), state.HomeNavigationPages.Count == 0
                ? Localize(Resource.String.ui_none_shown) : string.Join(" · ", state.HomeNavigationPages.Select(PageLabel)),
            () => go(ShowPageTabChoices), "grid");
        ToggleRow(parent, Localize(Resource.String.ui_minimal_status_bar), state.MinimalStatusBar, value =>
        {
            state.MinimalStatusBar = value;
            Save();
            Render();
        }, Localize(Resource.String.ui_clock_battery_status));
        ToggleRow(parent, Localize(Resource.String.ui_show_battery_on_home), state.ShowHomeBattery, value =>
        {
            state.ShowHomeBattery = value;
            Save();
        }, Localize(Resource.String.ui_battery_percentage_indicator));
        SettingRow(parent, Localize(Resource.String.ui_status_row_position), state.StatusPlacement == "Top" ? Localize(Resource.String.ui_top_corners) : Localize(Resource.String.ui_below_camera_cutout),
            () => go(() => Choose(Resource.String.ui_status_row_position, [Localize(Resource.String.ui_top_corners), Localize(Resource.String.ui_below_camera_cutout)], index =>
            {
                state.StatusPlacement = index == 0 ? "Top" : "Safe";
                Save();
                Render();
            })));
    }

    private void AddHomeSwipeSetting(LinearLayout parent, Action<Action> go) =>
        SettingRow(parent, Localize(Resource.String.ui_swipe_right_on_home), state.SwipeRightOpensDiscover ? GoogleDiscover : Localize(Resource.String.ui_today),
            () => go(() => Choose(Resource.String.ui_swipe_right_on_home, [Localize(Resource.String.ui_today), GoogleDiscover], index =>
            {
                state.SwipeRightOpensDiscover = index == 1;
                Save();
                if (page is "Settings" or "Navigation & status")
                    RenderBody();
            }, state.SwipeRightOpensDiscover ? 1 : 0)), "grid", Localize(Resource.String.ui_google_discover_feed_today_root_gestures));

    private void ShowPageTabChoices() => Sheet(Localize(Resource.String.ui_page_tab_buttons), (panel, dialog) =>
    {
        Paragraph(Localize(Resource.String.ui_choose_which_buttons_appear_below_home_swipe_between_pages), panel);
        foreach (string target in new[] { "Root", "Tiles", "Conversations" })
        {
            string pageId = target;
            ToggleRow(panel, PageLabel(pageId), state.HomeNavigationPages.Contains(pageId), value => { if (value) { if (!state.HomeNavigationPages.Contains(pageId)) state.HomeNavigationPages.Add(pageId); } else state.HomeNavigationPages.Remove(pageId); Save(); });
        }
    }, (footer, dialog) => footer.AddView(Button(Localize(Resource.String.ui_done), () => { dialog.Dismiss(); RenderBody(); }, true), Match()));

    private void RenderDrawerSettings()
    {
        ToggleRow(body, Localize(Resource.String.ui_drag_headings_to_fold), state.DragDrawerHeadings, value => { state.DragDrawerHeadings = value; Save(); });
        Paragraph(Localize(Resource.String.ui_optional_vertical_drag_on_drawer_headings_leave_off_for));
        body.AddView(Button(Localize(Resource.String.ui_new_drawer_action), NewDrawer, true), Match());
        Gap(body);
        foreach (var drawer in state.Drawers)
        {
            SettingRow(body, DrawerName(drawer), Counted(apps.Count(a => state.Apps[a.Id].DrawerId == drawer.Id), Resource.Plurals.app), () => EditDrawer(drawer), "grid");
        }
        Gap(body);
        Paragraph(Localize(Resource.String.ui_hold_a_drawer_heading_on_the_home_screen_to));
    }
    private void NewDrawer() => Prompt(Resource.String.ui_new_drawer, "", s =>
    {
        if (string.IsNullOrWhiteSpace(s))
            return;
        if (state.Drawers.Count >= 40)
        {
            Toast(Localize(Resource.String.ui_you_can_create_up_to_40_drawers));
            return;
        }
        var drawer = new Drawer { Name = LauncherState.CleanName(s, "Drawer"), Expanded = true };
        state.Drawers.Insert(Math.Max(0, state.Drawers.Count - 1), drawer);
        Save();
        Render();
        SelectDrawerApps(drawer);
    });
    private void EditDrawer(Drawer drawer)
    {
        Sheet(DrawerName(drawer), (panel, dialog) =>
        {
            void Done(Action change)
            {
                change();
                Save();
                dialog.Dismiss();
                Render();
            }
            SettingRow(panel, Localize(Resource.String.ui_rename), DrawerName(drawer), () => Prompt(Resource.String.ui_drawer_name, drawer.Name, s => Done(() => drawer.Name = LauncherState.CleanName(s, drawer.Name))));
            SettingRow(panel, Localize(Resource.String.ui_drawer_icon), Localize(Resource.String.ui_symbols_libraries_installed_packs), () => { dialog.Dismiss(); PickDrawerIcon(drawer); }, DrawerIcons.All.Contains(drawer.Icon) ? drawer.Icon : DrawerIcons.ForDrawer(drawer.Id));
            SettingRow(panel, Localize(Resource.String.ui_choose_apps), Localize(Resource.String.ui_select_the_apps_in_this_drawer), () => { dialog.Dismiss(); SelectDrawerApps(drawer); }, "grid");
            ToggleRow(panel, Localize(Resource.String.ui_expanded_2), drawer.Expanded, v => Done(() => drawer.Expanded = v));
            SettingRow(panel, Localize(Resource.String.ui_move_up), "", () => Done(() => MoveDrawer(drawer, -1)), "up");
            SettingRow(panel, Localize(Resource.String.ui_move_down), "", () => Done(() => MoveDrawer(drawer, 1)), "down");
            if (drawer.Id != "other")
                SettingRow(panel, Localize(Resource.String.ui_remove_drawer), Localize(Resource.String.ui_apps_move_to_app_box), () =>
                Confirm(Localize(Resource.String.ui_remove_named_drawer, DrawerName(drawer)), Localize(Resource.String.ui_its_apps_will_move_to_app_box), Localize(Resource.String.ui_remove_drawer), () => Done(() => state.RemoveDrawer(drawer.Id))));
            panel.AddView(Button(Localize(Resource.String.ui_done), () => dialog.Dismiss(), true), Match());
        });
    }
    private void MoveDrawer(Drawer drawer, int direction)
    {
        int i = state.Drawers.IndexOf(drawer), j = i + direction;
        if (j < 0 || j >= state.Drawers.Count)
            return;
        (state.Drawers[i], state.Drawers[j]) = (state.Drawers[j], state.Drawers[i]);
    }
    private void SelectDrawerApps(Drawer drawer)
    {
        var options = apps.Where(a => !a.PrivateProfile).OrderBy(a => a.Label).ToArray();
        var selected = options.Select(a => state.Apps[a.Id].DrawerId == drawer.Id).ToArray();
        Sheet(DrawerName(drawer), (panel, dialog) =>
        {
            Paragraph(Localize(Resource.String.ui_choose_the_apps_for_this_drawer), panel);
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var app = options[i];
                var check = new CheckBox(this) { Text = app.Label + (state.Apps[app.Id].Hidden ? Localize(Resource.String.ui_hidden_2) : ""), Checked = selected[i], Typeface = regular, TextSize = 15 };
                check.ButtonTintList = AccentTint();
                BindToggleHaptics(check);
                check.SetTextColor(palette.Text);
                check.SetMinimumHeight(Dp(52));
                check.CheckedChange += (_, e) => selected[index] = e.IsChecked;
                panel.AddView(check, Match());
            }
        }, (footer, dialog) =>
        {
            footer.AddView(Button(Localize(Resource.String.ui_save), () =>
            {
                for (int i = 0; i < options.Length; i++)
                {
                    var rule = state.Apps[options[i].Id];
                    if (selected[i])
                        rule.DrawerId = drawer.Id;
                    else if (rule.DrawerId == drawer.Id)
                        rule.DrawerId = "other";
                }
                Save();
                dialog.Dismiss();
                Render();
            }, true), Match());
            Gap(footer, 8);
            footer.AddView(Button(Localize(Resource.String.ui_cancel), () => dialog.Dismiss()), Match());
        });
    }
    private void RenderHidden()
    {
        Paragraph(Localize(Resource.String.ui_hidden_apps_are_removed_from_launcher_tiles_and_search));
        var hidden = apps.Where(a => !a.PrivateProfile && state.Apps[a.Id].Hidden).ToArray();
        if (hidden.Length == 0)
            Paragraph(Localize(Resource.String.ui_no_hidden_apps_hold_an_app_tile_to_hide));
        foreach (var app in hidden)
            SettingRow(body, app.Label, Localize(Resource.String.ui_tap_to_show), () => { state.Apps[app.Id].Hidden = false; Save(); Render(); }, "plus");
    }
    private void RenderPermissions()
    {
        Title(Localize(Resource.String.ui_youre_in_control));
        Paragraph(Localize(Resource.String.ui_launcher_works_without_optional_permissions_enable_only_the_features));
        SettingRow(body, Localize(Resource.String.ui_default_home_app), IsDefaultHome() ? Localize(Resource.String.ui_launcher_is_your_home) : Localize(Resource.String.ui_choose_launcher_in_androids_chooser), RequestHome, "grid");
        SettingRow(body, Localize(Resource.String.ui_usage_access), AppCatalog.HasUsageAccess(this) ? Localize(Resource.String.ui_enabled) : Localize(Resource.String.ui_optional_real_app_usage_and_daily_goals), () => OpenIntent(new Intent(Android.Provider.Settings.ActionUsageAccessSettings, Uri.Parse("package:" + PackageName))), "clock");
        AddNotificationAccessSetting(body);
        SettingRow(body, Localize(Resource.String.ui_connection_names), connections.CanReadBluetooth && connections.CanReadWifiName ? Localize(Resource.String.ui_enabled) : Localize(Resource.String.ui_optional_wi_fi_and_connected_bluetooth_names), RequestConnectionDetails, "wifi");
        Paragraph(Localize(Resource.String.ui_access_is_optional_and_used_by_the_features_you));
    }
    private bool IsDefaultHome()
    {
        var home = new Intent(Intent.ActionMain);
        home.AddCategory(Intent.CategoryHome);
        return PackageManager!.ResolveActivity(home, Android.Content.PM.PackageInfoFlags.MatchDefaultOnly)?.ActivityInfo?.PackageName == PackageName;
    }
    private void RequestHome()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            var roles = (RoleManager?)GetSystemService(RoleService);
            if (roles?.IsRoleAvailable(RoleManager.RoleHome) == true && !roles.IsRoleHeld(RoleManager.RoleHome))
            {
                StartActivityForResult(roles.CreateRequestRoleIntent(RoleManager.RoleHome), 601);
                return;
            }
        }
        OpenIntent(new Intent(Android.Provider.Settings.ActionHomeSettings));
    }
    private void ShowGuide() => Sheet(Localize(Resource.String.ui_using_launcher), (panel, dialog) =>
    {
        Paragraph(Localize(Resource.String.ui_tap_a_drawer_heading_to_expand_it_hold_it), panel);
        Paragraph(Localize(Resource.String.ui_tap_an_app_to_open_it_hold_an_app), panel);
        Paragraph(Localize(Resource.String.ui_swipe_right_for_root_swipe_left_for_conversations_the), panel);
        Paragraph(Localize(Resource.String.ui_pull_down_within_the_top_of_the_tiles_screen), panel);
        Paragraph(Localize(Resource.String.ui_hold_the_search_icon_or_tap_the_three_dots), panel);
        Paragraph(Localize(Resource.String.ui_root_has_editable_notes_calendar_calculator_a_focus_timer), panel);
        panel.AddView(Button(Localize(Resource.String.ui_got_it), () => dialog.Dismiss(), true), Match());
    });
    private void ExportBackup()
    {
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/json");
        intent.PutExtra(Intent.ExtraTitle, "launcher-layout.json");
        StartActivityForResult(intent, 701);
    }
    private void ImportBackup()
    {
        void Pick()
        {
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
            StartActivityForResult(intent, 702);
        }
        if (state.Apps.Values.Any(r => r.BiometricLocked))
            AuthenticateDevice(Localize(Resource.String.ui_layout_restore), Pick);
        else
            Pick();
    }
}
