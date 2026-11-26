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
            !SettingMatches(title + " " + detail + " " + searchTerms))
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

    private int SelectedChoice(string title) => title switch
    {
        "Theme" => state.FollowSystemTheme ? 3 : Array.IndexOf(new[] { "Dark", "Light", "Sun" }, state.Theme),
        "Columns" => state.Columns - 3,
        "Tile height" => Array.IndexOf(new[] { 88, 100, 112, 132, 152 }, state.TileHeight),
        "Wallpaper dimming" => Array.IndexOf(new[] { 35, 50, 65, 80 }, state.WallpaperDim),
        "Tile corners" => Array.IndexOf(new[] { 0, 6, 12, 20, 28 }, state.CornerRadius),
        "Icon style" => Array.IndexOf(new[] { "Monochrome", "Grayscale", "Original" }, state.IconStyle),
        "Drawer alignment" => Array.IndexOf(new[] { "Top", "Center", "Bottom" }, state.Alignment),
        "Temperature units" => state.Weather.Fahrenheit ? 1 : 0,
        "Weather refresh" => Array.IndexOf(new[] { 0, 30, 60, 180, 360 }, state.Weather.RefreshMinutes),
        "Status row position" => state.StatusPlacement == "Top" ? 0 : 1,
        "Switching delay" => Array.IndexOf(new[] { 3, 5, 10, 20 }, state.LightThemeDelaySeconds),
        _ => -1
    };
    private void Choose(string title, string[] options, Action<int> action, int selected = -1) => Sheet(title, (panel, dialog) =>
    {
        int current = selected >= 0 ? selected : SelectedChoice(title);
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            bool active = i == current;
            var option = Text(options[i] + (active ? "   ✓" : ""), 15, active, active ? palette.Accent : palette.Text);
            option.Gravity = GravityFlags.Start | GravityFlags.CenterVertical;
            option.SetMinimumHeight(Dp(52));
            option.SetPadding(Dp(14), Dp(14), Dp(14), Dp(14));
            option.Selected = active;
            option.ContentDescription = options[i] + (active ? ", selected" : "");
            if (active)
                option.Background = Background(new Color(palette.Accent.R, palette.Accent.G, palette.Accent.B, (byte)20), 12);
            TouchFeedback(option, 10);
            option.Click += (_, _) => { dialog.Dismiss(); action(index); };
            panel.AddView(option, Match());
            if (i < options.Length - 1)
                panel.AddView(new View(this) { Background = new ColorDrawable(palette.Divider) }, Match(Dp(1)));
        }
    });

    private void Prompt(string title, string initial, Action<string> action)
    {
        EditText? input = null;
        var dialog = Sheet(title, (panel, sheet) =>
        {
            bool weather = title == "Weather location";
            bool secret = title.Contains("API key", StringComparison.OrdinalIgnoreCase);
            bool lux = title.Contains("(lux)");
            bool feedCount = title == "Articles to display";
            bool duration = title.Contains("duration", StringComparison.OrdinalIgnoreCase), address = title.Contains("address", StringComparison.OrdinalIgnoreCase), code = title == "Pairing code", colour = title.Contains("#");
            string help = feedCount ? "Choose how many stories appear initially, from 1 to 50." : lux ? "Enter the sensor level in lux. The Light threshold must be higher than the Dark threshold." : weather ? "Search for a city or town." : secret ? "Paste your provider key. Leave this blank to remove it." : duration ? "Use a duration such as 10 min, 30s or 1h." : address ? "Enter the address shown by Launcher Companion." : code ? "Enter the eight-digit code shown on your computer." : colour ? "Use a six-digit hex colour, such as #E4FF00." : title.Contains("command") || title.Contains("query") ? "Enter the search or value for this card." : "Enter a name below.";
            Paragraph(help, panel);
            var label = Text(feedCount ? "STORY COUNT" : lux ? "LIGHT LEVEL (LUX)" : weather ? "CITY" : secret ? "API KEY" : duration ? "DURATION" : address ? "ADDRESS" : code ? "PAIRING CODE" : colour ? "HEX COLOUR" : "NAME", 10, true, palette.Muted);
            label.SetPadding(Dp(4), 0, 0, Dp(8));
            panel.AddView(label, Match());
            input = new EditText(this) { Text = initial, Hint = weather ? "e.g. Cape Town, South Africa" : title == "New drawer" ? "e.g. Daily essentials" : title, TextSize = 18, Typeface = regular };
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
            DialogActions(actions, ("Cancel", sheet.Dismiss), (title == "Weather location" ? "Search" : title == "New drawer" ? "Create drawer" : "Save", () =>
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

    private string ValidatePrompt(string title, string value)
    {
        string text = value.Trim();
        if (title.Contains("API key", StringComparison.OrdinalIgnoreCase) || title == "Search aliases")
            return "";
        if (title.Contains('#'))
            return System.Text.RegularExpressions.Regex.IsMatch(text, "^#[0-9a-fA-F]{6}$") ? "" : "Use six hex digits, such as #E4FF00.";
        if (title == "Articles to display")
            return int.TryParse(text, out int count) && count is >= 1 and <= 50 ? "" : "Choose a number from 1 to 50.";
        if (title.Contains("(lux)"))
        {
            if ((!double.TryParse(text, out double lux) && !double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lux)) || !double.IsFinite(lux) || lux is < 0 or > 200000)
                return "Enter a light level from 0 to 200000 lux.";
            return title.StartsWith("Light") && lux <= state.DarkThemeLux || title.StartsWith("Dark") && lux >= state.LightThemeLux ? "The Light threshold must be higher than the Dark threshold." : "";
        }
        if (title.Contains("duration", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                RootCommand.TimerDuration(text);
                return "";
            }
            catch (ArgumentException e) { return e.Message; }
        }
        if (title.Contains("address", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                CompanionLink.ValidateAddress(text);
                return "";
            }
            catch (ArgumentException e) { return e.Message; }
        }
        if (title == "Pairing code")
            return text.Length == 8 && text.All(char.IsAsciiDigit) ? "" : "Enter the eight-digit code shown on the computer.";
        if (title == "Weather location")
            return text.Length >= 2 ? "" : "Enter at least two characters.";
        if (title == "Save a link")
            return OnlineCards.SafeLink(text) ? "" : "Enter a full http:// or https:// link.";
        return text.Length == 0 ? "Enter a value before saving." : "";
    }

    private void Confirm(string title, string message, string actionLabel, Action action) => Sheet(title, (panel, dialog) => Paragraph(message, panel), (footer, dialog) =>
    {
        DialogActions(footer, ("Cancel", dialog.Dismiss), (actionLabel, () => { dialog.Dismiss(); action(); }
        ));
    });

    private void QuickControls()
    {
        Sheet("Launcher", (panel, dialog) =>
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
            SettingRow(panel, "Light", state.Theme == "Light" ? "On" : "Soft light theme", () => Go(() => { ManualTheme("Light"); }), "sun");
            SettingRow(panel, "Sun", state.Theme == "Sun" ? "On" : "High contrast, outdoors", () => Go(() => { ManualTheme("Sun"); }), "sun");
            SettingRow(panel, "Dark", state.Theme == "Dark" ? "On" : "Quiet, dark surfaces", () => Go(() => { ManualTheme("Dark"); }), "focus");
            SettingRow(panel, "Accent", state.Accent, () => Go(ShowAccentChoice), "photo");
            SettingRow(panel, "Quick access", "Choose the apps on your home screen", () => Go(ShowQuickAccess), "pin");
            SettingRow(panel, "Focus", state.Focus ? "On · tap to turn off" : "Quiet outlined tiles", () => Go(() => { state.Focus = !state.Focus; state.FocusEndsAt = 0; Save(); Render(); }), "focus");
            SettingRow(panel, page == "Root" ? "Add widget" : "Add drawer", "", () => Go(() => { if (page == "Root") AddWidget(); else NewDrawer(); }), "plus");
            SettingRow(panel, "All apps", "Search your installed apps", () => Go(() => { page = "Tiles"; StartSearch(); }), "grid");
            SettingRow(panel, "Settings", "Make Launcher your own", () => Go(() => Navigate("Settings")), "settings");
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

        var personal = AddSettingsCard("Make it yours");
        SettingRow(personal, "Appearance", (state.FollowSystemTheme ? "Follow Android" : state.Theme) +
            $" · {state.Columns} columns", () => Navigate("Appearance"), "sun");
        SettingRow(personal, "Edit home layout", "Arrange tiles and cards · undo changes", ShowLayoutEditor, "grid");
        SettingRow(personal, "Apps & drawers", $"{Counted(state.Drawers.Count, "drawer")} · {HiddenAppCount} hidden",
            () => Navigate("Apps & drawers"), "grid");
        AddGestureSetting(personal);

        var features = AddSettingsCard("Launcher features");
        AddSearchSettingsLink(features);
        AddDownloadsSettingsLink(features);
        SettingRow(features, "Cards, feeds & weather", $"{Sources.Count(s => s.Enabled)} news sources · Today cards and widgets",
            () => ShowSettingsCategory("Cards, feeds & weather", AddContentSettings), "rss");
        SettingRow(features, "Notifications & panels", ConversationService.Enabled(this) ? "Notification access on" : "Notification access off",
            () => Navigate("Notifications & panels"), "message");
        AddSpeechSettingsLink(features);

        var device = AddSettingsCard("Device & data");
        AddWearSettingsLink(device);
        SettingRow(device, "Setup & permissions", IsDefaultHome() ? "Default Home set · device access" : "Set default Home · device access",
            () => ShowSettingsCategory("Setup & permissions", AddDeviceSettings), "phone");
        SettingRow(device, "Privacy", "Protected previews and message history", ShowPrivacyRecovery, "lock");
        SettingRow(device, "Backup & restore", "Export or restore your layout", () => ShowSettingsCategory("Backup & restore", AddBackupSettings), "file");
        SettingRow(device, "App updates", "Check for a new Launcher version", () => Navigate("Updates"), "down");

        var support = AddSettingsCard("Help");
        SettingRow(support, "How to use Launcher", "Gestures, drawers and customisation", ShowGuide, "note");
        Gap(body);
        Paragraph("Launcher · " + InstalledPackage().VersionName + "\nBuilt by *Kevin*.", null, TextAlignment.Center);
    }

    private void AddGestureSetting(LinearLayout parent) =>
        SettingRow(parent, "Gestures", "Double-tap to lock " + (state.DoubleTapToLock ? "on" : "off"),
            ShowGestureSetup, "phone", "screen lock tap double tap accessibility");

    private void AddWearSettingsLink(LinearLayout parent) =>
        SettingRow(parent, "Wear OS", "Set up and install Launcher Roll on your watch",
            () => Navigate(WearSettingsPage), "clock", "watch watchface watch face companion install");

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
        AddAppOrganisationSettings(AddSettingsCard("Organise apps"));
        AddFocusSetting(AddSettingsCard("Focus"));
    }

    private void AddAppOrganisationSettings(LinearLayout parent)
    {
        SettingRow(parent, "Edit home layout", "Arrange tiles and cards · undo changes", ShowLayoutEditor, "grid");
        SettingRow(parent, "Manage drawers", Counted(state.Drawers.Count, "drawer") + " · create, rename or reorder",
            () => Navigate("Drawers"), "grid");
        SettingRow(parent, "Hidden apps", Counted(HiddenAppCount, "app") + " hidden · restore visibility",
            () => Navigate("Hidden"), "app");
        SettingRow(parent, "Quick access", "Choose up to five apps for the Home dock", ShowQuickAccess, "pin", "favorites favourites pinned apps");
    }

    private void AddFocusSetting(LinearLayout parent)
    {
        ToggleRow(parent, "Focus mode", state.Focus, enabled =>
        {
            state.Focus = enabled;
            if (!enabled) state.FocusEndsAt = 0;
            Save();
        }, "quiet outlined tiles distraction");
        SettingsExplanation(parent, "Focus gives your app tiles a quieter outlined look.");
    }

    private void AddContentSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, "Home cards & widgets", "Notes, cards and Android widgets",
            () => go(() => Navigate("Root settings")), "root");
        SettingRow(parent, "News & feeds", Counted(Sources.Count(s => s.Enabled), "source") + " · " +
            Counted(state.FeedArticleCount, "story", "stories") + $" · images {(state.FeedImages ? "on" : "off")}",
            () => go(ManageFeeds), "rss");
        SettingRow(parent, "Saved stories", Counted(state.SavedArticles.Count, "saved story", "saved stories"),
            () => go(ShowSavedArticles), "rss");
        SettingRow(parent, "Weather", string.IsNullOrEmpty(state.Weather.City) ? "Choose city and units" : state.Weather.City,
            () => go(() => Navigate("Weather")), "cloud");
        SettingRow(parent, "Online cards", "RSS, web search and YouTube",
            () => go(() => Navigate("Integrations")), "link");
    }

    private void AddSpeechSettingsLink(LinearLayout parent) =>
        SettingRow(parent, SpeechSettingsPage, KokoroModelInstaller.IsInstalled(this)
                ? "Offline voice ready · playback and news"
                : "Voice model not installed · download and playback",
            () => OpenSpeechSettings(), "play", "Kokoro model files voice speech download storage");

    private void AddSpeechActions(LinearLayout parent)
    {
        SettingRow(parent, "Kokoro speech", "Voice selection, speed and offline playback",
            () => KokoroReaderDialog.Show(this), "play");
        SettingRow(parent, "Listen to latest news", "Read RSS headlines and summaries aloud",
            ReadLatestFeedsWithKokoro, "rss");
    }

    private void RenderNotificationSettings(LinearLayout? searchResults = null)
    {
        LinearLayout Group(string title) => searchResults ?? AddSettingsCard(title);

        AddNotificationAccessSetting(Group("Permissions"));

        var sources = Group("Sources & panels");
        SettingRow(sources, "Notification sources", state.ConversationMessagesOnly ? "Messages only · choose apps" : "All notification types · choose apps",
            PickConversationSources, "filter", "conversations messages filters");
        SettingRow(sources, "Quick panel", "Open Launcher controls and current notifications",
            () => Navigate("Quick panel"), "down");

        SettingRow(Group("History"), "Message history", state.KeepMessageHistory ? "On · saving new messages" : "Off · optional local history",
            MessageHistorySettings, "message");

        var alerts = Group("Alerts");
        SettingRow(alerts, "Notification whispers", NotificationWhisperPreferences.Enabled(this)
                ? "On · quiet spoken announcements" : "Off · choose apps and senders",
            ShowNotificationWhisperSettings, "message", "speech voice spoken alerts");
        SettingRow(alerts, "Timer alerts", "Android notifications and exact alarm access", OpenTimerPermissions, "clock");
    }

    private bool? notificationAccessBeforeSettings;

    private void AddNotificationAccessSetting(LinearLayout parent)
    {
        bool enabled = ConversationService.Enabled(this);
        SettingRow(parent, "Notification access", enabled ? "On · manage in Android settings" : "Off · enable to show notifications in Launcher",
            OpenNotificationAccessSettings, "lock", "permissions notification listener quick panel conversations");
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
            Confirm("Notification access", "Allow Launcher to show current notifications and message previews. Message history is optional and stored on this phone.",
                "Open Android settings", OpenAndroidSettings);
    }

    private void AddDeviceSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, "Setup & feature health", "Check access, compatibility and recovery",
            () => go(ShowFeatureHealth), "settings");
        SettingRow(parent, "Permissions", "Default home, usage and notification access",
            () => go(() => Navigate("Permissions")), "settings");
        SettingRow(parent, "Haptic feedback", state.HapticFeedback == "Off"
                ? "Off"
                : state.HapticFeedback + " · follows Android touch vibration",
            () => go(ShowHapticSettings), "phone");
        SettingRow(parent, "System panel", state.SystemPanelEnabled
                ? SystemPanelService.IsEnabled(this) ? "On · available across apps" : "On · enable accessibility access"
                : "Off · controls across apps",
            () => go(ShowSystemPanelSettings), "phone");
        SettingRow(parent, "App grayscale", "Display permission and foreground detection",
            () => go(() => Navigate("Grayscale setup")), "photo");
        // Desktop companion intentionally remains hidden until it is ready.
    }

    private void AddMaintenanceSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, "Privacy & recovery", "Protected previews, backups and data",
            () => go(ShowPrivacyRecovery), "lock");
        SettingRow(parent, "App updates", "Check, download and install from Kevlog",
            () => go(() => Navigate("Updates")), "down");
        AddBackupSettings(parent, go);
        SettingRow(parent, "How to use Launcher", "Gestures, drawers, tiles and privacy",
            () => go(ShowGuide), "note");
    }

    private void AddBackupSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, "Back up layout", "Export settings as a JSON file", () => go(ExportBackup), "file", "backup");
        SettingRow(parent, "Restore layout", "Import a previously saved layout", () => go(ImportBackup), "file");
    }

    private void RenderSettingsSearchResults()
    {
        Action<Action> open = action => action();
        AddGestureSetting(body);
        SettingRow(body, "Appearance presets", "Classic and Studio · theme and layout",
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
            var empty = Text("No settings match “" + settingsQuery.Trim() +
                "”. Try theme, icon, voice, permissions or privacy.", 14, color: palette.Muted);
            empty.SetPadding(0, Dp(20), 0, Dp(20));
            body.AddView(empty, Match());
        }
    }

    private void RenderAppearance()
    {
        var appearance = AddSettingsCard("Theme & effects");
        SettingRow(appearance, "Theme", state.FollowSystemTheme ? "Follow Android" : state.Theme, ShowThemeChoice, "sun");
        SettingRow(appearance, "Accent", state.Accent, ShowAccentChoice, "photo");
        AddGlassSetting(appearance);
        AddTouchGlowSetting(appearance);

        var customisation = AddSettingsCard("Customise");
        SettingRow(customisation, "Theme & wallpaper", state.UseWallpaper ? $"Wallpaper on · {state.WallpaperDim}% dimming" : "Wallpaper off · automatic theme and dimming",
            () => Navigate("Theme & wallpaper"), "photo");
        SettingRow(customisation, "Tiles & icons", $"{state.Columns} columns · {state.IconStyle}",
            () => Navigate("Tiles & icons"), "grid");
        SettingRow(customisation, "Motion & depth", state.LayeredInterface ? "Layered interface on" : "Layered interface off",
            () => Navigate("Motion & depth"), "focus");
        SettingRow(customisation, "Navigation & status", state.ShowHomeNavigation ? "Page tabs on" : "Page tabs off",
            () => Navigate("Navigation & status"), "settings");

        var presetCard = AddSettingsCard("Presets");
        var presets = Stack(true);
        presets.SetPadding(0, Dp(6), 0, Dp(6));
        foreach (string preset in new[] { "Classic", "Studio" })
        {
            var choice = preset;
            var button = Button(choice, () => Confirm("Apply " + choice + " preset?",
                "This changes multiple appearance settings.", "Apply preset", () =>
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

    private void AddTouchGlowSetting(LinearLayout parent) => ToggleRow(parent, "Touch glow", state.TouchGlow, enabled =>
    {
        state.TouchGlow = enabled;
        if (!enabled)
            touchGlow?.Clear();
        Save();
    }, "accent color colour finger tap ripple");

    private void ShowThemeChoice() => Choose("Theme", ["Dark", "Light", "Sun", "Follow Android"], index =>
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
            ToggleRow(parent, "Liquid glass cards", state.LiquidGlassCards,
                value =>
                {
                    state.LiquidGlassCards = value;
                    Save();
                    if (IsStyledSettingsPage(page))
                        Render();
                }, "notifications today root app tiles search translucent refraction wallpaper");
    }

    private void AddAppearanceThemeSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, "Automatic theme", state.AutomaticLightTheme ? "Enabled · light sensor" : "Off · manual or system theme",
            () => go(() => Navigate("Automatic theme")), "sun");
        SettingRow(parent, "Theme", state.FollowSystemTheme ? "Follow Android" : state.Theme,
            () => go(ShowThemeChoice), "sun");
        SettingRow(parent, "Accent", state.Accent, () => go(ShowAccentChoice), "photo");
        SettingRow(parent, "Wallpaper dimming", state.WallpaperDim + "% · other screens",
            () => go(() => Choose("Wallpaper dimming", ["Subtle · 35%", "Balanced · 50%", "Deep · 65%", "Strong · 80%"], index =>
            {
                state.WallpaperDim = new[] { 35, 50, 65, 80 }[index];
                Save();
                Render();
            })), "photo");
        ToggleRow(parent, "Use system wallpaper", state.UseWallpaper, value =>
        {
            state.UseWallpaper = value;
            Save();
            Render();
        }, "other screens");
        ToggleRow(parent, "Home accent glow", state.HomeAccentGlow, value =>
        {
            state.HomeAccentGlow = value;
            Save();
            Render();
        });
    }

    private void AddAppearanceTileSettings(LinearLayout parent, Action<Action> go)
    {
        SettingRow(parent, "Columns", state.Columns.ToString(),
            () => go(() => Choose("Columns", ["3", "4", "5", "6"], index =>
            {
                state.Columns = index + 3;
                Save();
                Render();
            })), "grid");
        SettingRow(parent, "Tile height", state.TileHeight + " dp",
            () => go(() => Choose("Tile height", ["Compact · 88", "Balanced · 100", "Classic · 112", "Spacious · 132", "Large · 152"], index =>
            {
                state.TileHeight = new[] { 88, 100, 112, 132, 152 }[index];
                Save();
                Render();
            })));
        SettingRow(parent, "Tile corners", state.CornerRadius + " dp",
            () => go(() => Choose("Tile corners", ["Square", "Subtle · 6", "Soft · 12", "Round · 20", "Pill · 28"], index =>
            {
                state.CornerRadius = new[] { 0, 6, 12, 20, 28 }[index];
                Save();
                Render();
            })));
        SettingRow(parent, "Tile icon size", state.TileIconScale == 100 ? "Default · 100%" : state.TileIconScale + "%",
            () => go(ShowTileIconSize), "photo");
        SettingRow(parent, "Icon style", state.IconStyle,
            () => go(() => Choose("Icon style", ["Monochrome", "Grayscale", "Original"], index =>
            {
                state.IconStyle = new[] { "Monochrome", "Grayscale", "Original" }[index];
                Save();
                Render();
            })), "photo");
        SettingRow(parent, "Icon pack", state.IconPack.Length == 0 ? "Apps’ own icons" :
            BuiltinIcons.Libraries.FirstOrDefault(p => p.Package == state.IconPack)?.Label ?? state.IconPack,
            () => go(() => PickIconPack()), "photo");
        ToggleRow(parent, "App labels", state.ShowLabels, value => { state.ShowLabels = value; Save(); }, "tile names text");
        ToggleRow(parent, "Show usage time", state.ShowUsage, value => { state.ShowUsage = value; Save(); }, "screen time");
        ToggleRow(parent, "Notification count badges", state.ShowNotificationBadges, value =>
        {
            state.ShowNotificationBadges = value;
            Save();
        }, "unread counts tile dots");
        ToggleRow(parent, "Media playback card", state.ShowMediaCard, value =>
        {
            state.ShowMediaCard = value;
            Save();
        }, "music now playing");
        SettingsExplanation(parent, "Monochrome uses themed app icons where supported; other app icons remain recognisable in grayscale.");
    }

    private void AddAppearanceMotionSettings(LinearLayout parent, Action<Action> go)
    {
        Switch? tiltSwitch = null;
        Switch? transitionsSwitch = null;
        ToggleRow(parent, "Layered interface", state.LayeredInterface, value =>
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
        }, "3d perspective card reveal parallax animations");
        SettingsExplanation(parent, "Master switch for app expansion, page and drawer transitions, scrolling effects and device tilt.");

        int before = parent.ChildCount;
        ToggleRow(parent, "Tilt depth", state.TiltDepth, value =>
        {
            if (value && !depthMotion.Available)
            {
                Toast("This phone does not provide a rotation sensor.");
                if (ReferenceEquals(parent, body)) RenderBody();
                return;
            }
            state.TiltDepth = value;
            Save();
        }, "gyroscope rotation sensor motion");
        if (parent.ChildCount > before)
        {
            tiltSwitch = parent.GetChildAt(parent.ChildCount - 1) as Switch;
            if (tiltSwitch is not null) tiltSwitch.Enabled = state.LayeredInterface && depthMotion.Available;
        }
        SettingsExplanation(parent, depthMotion.Available
            ? "Subtle movement from the phone rotation sensor. It pauses while touching the screen."
            : "Tilt depth is unavailable on this device; other effects still work.");

        before = parent.ChildCount;
        ToggleRow(parent, "Page transitions", state.PageTransitions, value =>
        {
            state.PageTransitions = value;
            if (!value) EndPageSlideForInterruption();
            Save();
        }, "animation switching pages");
        if (parent.ChildCount > before)
        {
            transitionsSwitch = parent.GetChildAt(parent.ChildCount - 1) as Switch;
            if (transitionsSwitch is not null) transitionsSwitch.Enabled = state.LayeredInterface;
        }
    }

    private void AddAppearanceNavigationSettings(LinearLayout parent, Action<Action> go)
    {
        AddHomeSwipeSetting(parent, go);
        SettingRow(parent, "Drawer alignment", state.Alignment,
            () => go(() => Choose("Drawer alignment", ["Top", "Center", "Bottom"], index =>
            {
                state.Alignment = new[] { "Top", "Center", "Bottom" }[index];
                Save();
                Render();
            })));
        ToggleRow(parent, "Centered drawer headings", state.CenterHeadings, value =>
        {
            state.CenterHeadings = value;
            Save();
        }, "drawer title placement");
        ToggleRow(parent, "Show page tabs", state.ShowHomeNavigation, value =>
        {
            state.ShowHomeNavigation = value;
            Save();
        }, "home navigation buttons");
        SettingRow(parent, "Page tab buttons", state.HomeNavigationPages.Count == 0
                ? "None shown" : string.Join(" · ", state.HomeNavigationPages.Select(PageLabel)),
            () => go(ShowPageTabChoices), "grid");
        ToggleRow(parent, "Minimal status bar", state.MinimalStatusBar, value =>
        {
            state.MinimalStatusBar = value;
            Save();
            Render();
        }, "clock battery status");
        ToggleRow(parent, "Show battery on home", state.ShowHomeBattery, value =>
        {
            state.ShowHomeBattery = value;
            Save();
        }, "battery percentage indicator");
        SettingRow(parent, "Status row position", state.StatusPlacement == "Top" ? "Top corners" : "Below camera cutout",
            () => go(() => Choose("Status row position", ["Top corners", "Below camera cutout"], index =>
            {
                state.StatusPlacement = index == 0 ? "Top" : "Safe";
                Save();
                Render();
            })));
    }

    private void AddHomeSwipeSetting(LinearLayout parent, Action<Action> go) =>
        SettingRow(parent, "Swipe right on Home", state.SwipeRightOpensDiscover ? GoogleDiscover : "Today",
            () => go(() => Choose("Swipe right on Home", ["Today", GoogleDiscover], index =>
            {
                state.SwipeRightOpensDiscover = index == 1;
                Save();
                if (page is "Settings" or "Navigation & status")
                    RenderBody();
            }, state.SwipeRightOpensDiscover ? 1 : 0)), "grid", "Google Discover feed Today root gestures");

    private void ShowPageTabChoices() => Sheet("Page tab buttons", (panel, dialog) =>
    {
        Paragraph("Choose which buttons appear below home. Swipe between pages or use the Launcher menu to reach a page whose button is hidden.", panel);
        foreach (string target in new[] { "Root", "Tiles", "Conversations" })
        {
            string pageId = target;
            ToggleRow(panel, PageLabel(pageId), state.HomeNavigationPages.Contains(pageId), value => { if (value) { if (!state.HomeNavigationPages.Contains(pageId)) state.HomeNavigationPages.Add(pageId); } else state.HomeNavigationPages.Remove(pageId); Save(); });
        }
    }, (footer, dialog) => footer.AddView(Button("Done", () => { dialog.Dismiss(); RenderBody(); }, true), Match()));

    private void RenderDrawerSettings()
    {
        ToggleRow(body, "Drag headings to fold", state.DragDrawerHeadings, value => { state.DragDrawerHeadings = value; Save(); });
        Paragraph("Optional vertical drag on drawer headings. Leave off for normal page scrolling. Tap any heading to open or close its drawer.");
        body.AddView(Button("+  New drawer", NewDrawer, true), Match());
        Gap(body);
        foreach (var drawer in state.Drawers)
        {
            SettingRow(body, drawer.Name, $"{apps.Count(a => state.Apps[a.Id].DrawerId == drawer.Id)} apps", () => EditDrawer(drawer), "grid");
        }
        Gap(body);
        Paragraph("Hold a drawer heading on the home screen to edit it. Apps in a removed drawer return to App box.");
    }
    private void NewDrawer() => Prompt("New drawer", "", s =>
    {
        if (string.IsNullOrWhiteSpace(s))
            return;
        if (state.Drawers.Count >= 40)
        {
            Toast("You can create up to 40 drawers.");
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
        Sheet(drawer.Name, (panel, dialog) =>
        {
            void Done(Action change)
            {
                change();
                Save();
                dialog.Dismiss();
                Render();
            }
            SettingRow(panel, "Rename", drawer.Name, () => Prompt("Drawer name", drawer.Name, s => Done(() => drawer.Name = LauncherState.CleanName(s, drawer.Name))));
            SettingRow(panel, "Drawer icon", "Symbols, libraries & installed packs", () => { dialog.Dismiss(); PickDrawerIcon(drawer); }, DrawerIcons.All.Contains(drawer.Icon) ? drawer.Icon : DrawerIcons.ForDrawer(drawer.Id));
            SettingRow(panel, "Choose apps", "Select the apps in this drawer", () => { dialog.Dismiss(); SelectDrawerApps(drawer); }, "grid");
            ToggleRow(panel, "Expanded", drawer.Expanded, v => Done(() => drawer.Expanded = v));
            SettingRow(panel, "Move up", "", () => Done(() => MoveDrawer(drawer, -1)), "up");
            SettingRow(panel, "Move down", "", () => Done(() => MoveDrawer(drawer, 1)), "down");
            if (drawer.Id != "other")
                SettingRow(panel, "Remove drawer", "Apps move to App box", () =>
                Confirm("Remove " + drawer.Name + "?", "Its apps will move to App box.", "Remove drawer", () => Done(() => state.RemoveDrawer(drawer.Id))));
            panel.AddView(Button("Done", () => dialog.Dismiss(), true), Match());
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
        Sheet(drawer.Name, (panel, dialog) =>
        {
            Paragraph("Choose the apps for this drawer.", panel);
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var app = options[i];
                var check = new CheckBox(this) { Text = app.Label + (state.Apps[app.Id].Hidden ? " (hidden)" : ""), Checked = selected[i], Typeface = regular, TextSize = 15 };
                check.ButtonTintList = AccentTint();
                BindToggleHaptics(check);
                check.SetTextColor(palette.Text);
                check.SetMinimumHeight(Dp(52));
                check.CheckedChange += (_, e) => selected[index] = e.IsChecked;
                panel.AddView(check, Match());
            }
        }, (footer, dialog) =>
        {
            footer.AddView(Button("Save", () =>
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
            footer.AddView(Button("Cancel", () => dialog.Dismiss()), Match());
        });
    }
    private void RenderHidden()
    {
        Paragraph("Hidden apps are removed from Launcher tiles and search. This does not secure or disable them.");
        var hidden = apps.Where(a => !a.PrivateProfile && state.Apps[a.Id].Hidden).ToArray();
        if (hidden.Length == 0)
            Paragraph("No hidden apps. Hold an app tile to hide it.");
        foreach (var app in hidden)
            SettingRow(body, app.Label, "Tap to show", () => { state.Apps[app.Id].Hidden = false; Save(); Render(); }, "plus");
    }
    private void RenderPermissions()
    {
        Title("You’re in control.");
        Paragraph("Launcher works without optional permissions. Enable only the features you want.");
        SettingRow(body, "Default home app", IsDefaultHome() ? "Launcher is your home" : "Choose Launcher in Android’s chooser", RequestHome, "grid");
        SettingRow(body, "Usage access", AppCatalog.HasUsageAccess(this) ? "Enabled" : "Optional · real app usage and daily goals", () => OpenIntent(new Intent(Android.Provider.Settings.ActionUsageAccessSettings, Uri.Parse("package:" + PackageName))), "clock");
        AddNotificationAccessSetting(body);
        SettingRow(body, "Connection names", connections.CanReadBluetooth && connections.CanReadWifiName ? "Enabled" : "Optional · Wi-Fi and connected Bluetooth names", RequestConnectionDetails, "wifi");
        Paragraph("Access is optional and used by the features you enable. App grayscale uses an accessibility service and a one-time display permission. File sharing uses a folder you select. Connection names may require nearby-device and location access; weather uses the city you enter. Work profiles and Android 15+ Private Space require Launcher as the default home. Android controls which profiles are accessible.");
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
    private void ShowGuide() => Sheet("Using Launcher", (panel, dialog) =>
    {
        Paragraph("Tap a drawer heading to expand it. Hold it to rename, reorder or choose its apps.", panel);
        Paragraph("Tap an app to open it. Hold an app to move, rename, pin, highlight, expand, hide or set a daily usage goal.", panel);
        Paragraph("Swipe right for Root; swipe left for Conversations. The menu inside search also opens Root, Apps and Notifications.", panel);
        Paragraph("Pull down within the top of the Tiles screen for Launcher’s quick panel. Tap the minimal status row to open it too. The screen-edge notification shade belongs to Android.", panel);
        Paragraph("Hold the search icon, or tap the three dots, for theme and focus controls. Focus gives app tiles a quiet outlined appearance.", panel);
        Paragraph("Root has editable notes, calendar, calculator, a focus timer and support for your installed Android widgets.", panel);
        panel.AddView(Button("Got it", () => dialog.Dismiss(), true), Match());
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
            AuthenticateDevice("layout restore", Pick);
        else
            Pick();
    }
}
