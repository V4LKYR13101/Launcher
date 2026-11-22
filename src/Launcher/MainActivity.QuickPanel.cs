using Android.App;
using Android;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Views;
using Android.Widget;
using LauncherApp.Services;
using LauncherApp.UI;

namespace LauncherApp;

public partial class MainActivity
{
    private View? homeBattery, homeDrawerMarker;

    private int BatteryPercent()
    {
        using var battery = RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryChanged));
        int level = battery?.GetIntExtra(BatteryManager.ExtraLevel, 0) ?? 0;
        int scale = battery?.GetIntExtra(BatteryManager.ExtraScale, 100) ?? 100;
        return scale > 0 ? level * 100 / scale : 0;
    }


    private string StatusClockText(DateTime now) => now.ToString(page == "Tiles" ? "HH:mm" : "HH:mm   ·   ddd, d MMM");
    private static string HomeStatusDateText(DateTime now) => now.ToString("dddd, dd MMMM");

    private void AddMinimalStatus()
    {
        var status = Stack(true);
        launcherStatusRow = status;
        bool centered = page == "Tiles" && !searching;
        status.SetGravity(centered ? GravityFlags.Center : GravityFlags.CenterVertical);
        status.SetPadding(Dp(12), centered ? 0 : Dp(4), Dp(12), centered ? 0 : Dp(4));
        var clockInk = AccentGlowDrawable.Blend(palette.Text, palette.Accent, palette.Light ? .17f : .3f);
        statusTime = Text(StatusClockText(DateTime.Now), centered ? (FontScale >= 1.4f ? 40 : 64) : 10,
            !centered, centered ? clockInk : palette.Muted);
        statusTime.SetSingleLine(true);
        statusNotificationText = Text("", 10, true, palette.Accent);
        statusNotificationText.SetSingleLine(true);

        batteryIcon = new BatteryView(this, centered ? palette.Muted : palette.Accent, palette.Muted);
        batteryPercentText = Text(BatteryPercent() + "%", 10, true, centered ? palette.Muted : palette.Accent);
        var battery = Stack(true);
        battery.SetGravity(GravityFlags.Center);
        var iconParams = new LinearLayout.LayoutParams(Dp(centered ? 17 : 26), Dp(centered ? 12 : 15));
        iconParams.RightMargin = Dp(4);
        battery.AddView(batteryIcon, iconParams);
        battery.AddView(batteryPercentText);

        if (centered)
        {
            statusTime.Gravity = GravityFlags.Center;
            statusTime.SetIncludeFontPadding(false);
            statusTime.Typeface = OperatingSystem.IsAndroidVersionAtLeast(28)
                ? Typeface.Create(Typeface.Default, 100, false)
                : Typeface.Create("sans-serif-thin", TypefaceStyle.Normal);
            statusTime.TextScaleX = FontScale >= 1.4f ? 1.07f : 1.16f;
            status.AddView(statusTime, new LinearLayout.LayoutParams(-2, -2));
        }
        else
        {
            status.AddView(statusTime, new LinearLayout.LayoutParams(0, -2, 1));
            var notificationParams = new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(12) };
            status.AddView(statusNotificationText, notificationParams);
            status.AddView(battery, new LinearLayout.LayoutParams(-2, -2));
        }
        UpdateBatteryUi();
        UpdateStatusNotifications();
        status.ContentDescription = "Open quick panel";
        BindTapHaptics(status);
        status.Click += (_, _) => Navigate("Quick panel");
        if (centered)
        {
            BindHoldHaptics(status);
            status.LongClick += (_, _) => QuickControls();
            var cluster = new RelativeLayout(this);
            status.Id = View.GenerateViewId();
            var timePosition = new RelativeLayout.LayoutParams(-2, -2);
            timePosition.AddRule(LayoutRules.CenterInParent);
            cluster.AddView(status, timePosition);

            var details = Stack();
            details.SetGravity(GravityFlags.Center);
            statusDate = Text(HomeStatusDateText(DateTime.Now), 11, color: palette.Muted);
            statusDate.SetSingleLine(true);
            statusDate.SetIncludeFontPadding(false);
            statusDate.Gravity = statusNotificationText.Gravity = GravityFlags.Center;
            MatchHomeDateWidth();
            details.AddView(statusDate, new LinearLayout.LayoutParams(-2, -2));
            details.AddView(statusNotificationText, new LinearLayout.LayoutParams(-2, -2));
            var detailPosition = new RelativeLayout.LayoutParams(-2, -2) { TopMargin = -Dp(4) };
            detailPosition.AddRule(LayoutRules.Below, status.Id);
            detailPosition.AddRule(LayoutRules.CenterHorizontal);
            cluster.AddView(details, detailPosition);
            details.LayoutChange += (_, _) => PositionHomeBattery();

            homeWeatherSlot = new FrameLayout(this);
            var weatherPosition = new RelativeLayout.LayoutParams(-2, -2);
            weatherPosition.AddRule(LayoutRules.Above, status.Id);
            weatherPosition.AddRule(LayoutRules.CenterHorizontal);
            cluster.AddView(homeWeatherSlot, weatherPosition);

            if (state.ShowHomeBattery)
            {
                var batteryPosition = new RelativeLayout.LayoutParams(-2, -2);
                batteryPosition.AddRule(LayoutRules.CenterHorizontal);
                homeBattery = battery;
                cluster.AddView(battery, batteryPosition);
                cluster.LayoutChange += (_, _) => PositionHomeBattery();
            }
            scene.AddView(cluster, new FrameLayout.LayoutParams(-1, -1));
            launcherStatusRow = cluster;
        }
        else
            shell.AddView(status, Match(Dp(30)));
    }

    private void PositionHomeBattery()
    {
        var anchor = statusNotificationText is { Visibility: ViewStates.Visible, Height: > 0 } badge ? badge : statusDate;
        if (homeBattery is not { Height: > 0 } battery || homeDrawerMarker is not { Height: > 0 } marker
            || anchor is not { Height: > 0 } || !marker.IsAttachedToWindow)
            return;

        using var anchorBounds = new Rect(0, 0, anchor.Width, anchor.Height);
        using var markerBounds = new Rect(0, 0, marker.Width, marker.Height);
        scene.OffsetDescendantRectToMyCoords(anchor, anchorBounds);
        scene.OffsetDescendantRectToMyCoords(marker, markerBounds);
        float middle = (anchorBounds.ExactCenterY() + markerBounds.ExactCenterY()) / 2;
        battery.TranslationY = middle - battery.Top - battery.Height / 2f;
    }

    private void MatchHomeDateWidth()
    {
        if (statusDate is not { } date || statusTime is not { } clock)
            return;
        float dateWidth = date.Paint!.MeasureText(date.Text ?? "");
        float timeWidth = clock.Paint!.MeasureText(clock.Text ?? "");
        if (dateWidth > 0 && timeWidth > 0 && Math.Abs(timeWidth - dateWidth) > .5f)
            date.SetTextSize(Android.Util.ComplexUnitType.Px, date.TextSize * timeWidth / dateWidth);
    }


    private void UpdateStatusNotifications()
    {
        if (statusNotificationText is null)
            return;

        // Notification access must be enabled.
        if (!ConversationService.Enabled(this))
        {
            statusNotificationText.Visibility = ViewStates.Gone;
            return;
        }

        // Count actionable notifications.
        // Exclude persistent, non-dismissible notifications.
        int count = ConversationService
            .NotificationSnapshot()
            .Count(n => n.Dismissible);

        if (count == 0)
        {
            statusNotificationText.Visibility = ViewStates.Gone;
            return;
        }

        // Compact indicator: ● 1, ● 2, ... ● 9+
        string label = count > 9
            ? "9+"
            : count.ToString();

        statusNotificationText.Text = $"● {label}";

        statusNotificationText.ContentDescription =
            $"{count} active notifications";

        statusNotificationText.Visibility = ViewStates.Visible;
    }



    private void RenderQuickPanel()
    {
        var clock = Text(DateTime.Now.ToString("HH:mm"), 36, true);
        body.AddView(clock, Match());
        Paragraph(DateTime.Now.ToString("dddd, d MMMM") + "   ·   " + BatteryPercent() + "% battery");
        Gap(body, 8);
        var grid = Stack();
        void Pair((string Label, string Glyph, string Detail, bool Active, Action Action) a, (string Label, string Glyph, string Detail, bool Active, Action Action) b)
        {
            var row = Stack(true);
            foreach (var item in new[] { a, b })
            {
                var foreground = item.Active ? palette.OnAccent : palette.Text;
                var tile = Stack(true);
                tile.SetGravity(GravityFlags.CenterVertical);
                tile.SetMinimumHeight(Dp(76));
                tile.SetPadding(Dp(12), Dp(10), Dp(10), Dp(10));
                tile.Background = Background(item.Active ? palette.Accent : palette.Card, 14);
                tile.AddView(new GlyphView(this, item.Glyph, foreground), new LinearLayout.LayoutParams(Dp(28), Dp(28)));
                var labels = Stack();
                labels.SetPadding(Dp(12), 0, 0, 0);
                var name = Text(item.Label, 14, true, foreground);
                name.SetSingleLine(true);
                name.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                labels.AddView(name, Match());
                var detail = Text(item.Detail, 11, color: item.Active ? foreground : palette.Muted);
                detail.SetMaxLines(FontScale > 1.3f ? 1 : 2);
                detail.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                detail.SetPadding(0, Dp(4), 0, 0);
                labels.AddView(detail, Match());
                tile.AddView(labels, new LinearLayout.LayoutParams(0, -2, 1));
                TouchFeedback(tile, 14);
                tile.Click += (_, _) => item.Action();
                var lp = new LinearLayout.LayoutParams(0, -1, 1);
                lp.SetMargins(Dp(4), Dp(4), Dp(4), Dp(4));
                row.AddView(tile, lp);
            }
            grid.AddView(row, Match());
        }
        var wifi = connections.Wifi();
        var bluetooth = connections.Bluetooth();
        var sound = ((AudioManager?)GetSystemService(AudioService))?.RingerMode ?? RingerMode.Normal;
        Pair(("Wi-Fi", "wifi", wifi.Detail, wifi.Active, () => OpenIntent(new Intent(OperatingSystem.IsAndroidVersionAtLeast(29) ? "android.settings.panel.action.WIFI" : Android.Provider.Settings.ActionWifiSettings))),
            ("Bluetooth", "bluetooth", bluetooth.Detail, bluetooth.Active, () => { if (!connections.CanReadBluetooth) RequestConnectionDetails(); else OpenIntent(new Intent(Android.Provider.Settings.ActionBluetoothSettings)); }
        ));
        Pair(("Sound", "sound", sound.ToString(), sound == RingerMode.Normal, () => OpenIntent(new Intent(OperatingSystem.IsAndroidVersionAtLeast(29) ? "android.settings.panel.action.VOLUME" : Android.Provider.Settings.ActionSoundSettings))),
            ("Focus", "focus", state.Focus ? "On · outlined tiles" : "Off", state.Focus, () => { state.Focus = !state.Focus; state.FocusEndsAt = 0; Save(); RenderBody(); }
        ));
        Pair(("Grayscale", "photo", GrayscaleService.Global(this) ? "Whole display" : "Off", GrayscaleService.Global(this), () => { if (!GrayscaleService.Ready(this)) { Navigate("Grayscale setup"); return; } GrayscaleService.SetGlobal(this, !GrayscaleService.Global(this)); RenderBody(); }
        ),
            ("Dark theme", "photo", state.Theme == "Dark" ? "On" : "Off", state.Theme == "Dark", () => { ManualTheme(state.Theme == "Dark" ? "Light" : "Dark"); }
        ));
        body.AddView(grid, Match());
        Gap(body, 12);
        if (!connections.CanReadBluetooth || !connections.CanReadWifiName)
        {
            body.AddView(Button("Show connected names", RequestConnectionDetails), Match());
            Gap(body, 8);
        }
        var heading = Text("Notifications", 20, true);
        heading.SetPadding(0, Dp(8), 0, Dp(14));
        body.AddView(heading, Match());
        if (!ConversationService.Enabled(this))
        {
            Paragraph("Connect notification access to show your current notifications here.");
            body.AddView(Button("Notification access", () => Navigate("Permissions")), Match());
            return;
        }
        if (((KeyguardManager?)GetSystemService(KeyguardService))?.IsDeviceLocked == true)
        {
            Paragraph("Unlock your phone to see notifications.");
            return;
        }
        var notifications = ConversationService.NotificationSnapshot();
        if (notifications.Count == 0)
            Paragraph("No current notifications.");
        AddNotificationGroups(body, notifications, swipe: true);
    }

    private void RequestConnectionDetails() => Confirm("Connection details", "Allow nearby-device access to display connected Bluetooth devices, and precise location access to display your Wi-Fi network name. Launcher does not scan for your location or upload these details.", "Continue", () =>
    {
        var permissions = new List<string>();
        if (OperatingSystem.IsAndroidVersionAtLeast(31) && !connections.CanReadBluetooth)
            permissions.Add(Manifest.Permission.BluetoothConnect);
        if (!connections.CanReadWifiName)
        {
            permissions.Add(Manifest.Permission.AccessCoarseLocation);
            permissions.Add(Manifest.Permission.AccessFineLocation);
        }
        if (permissions.Count > 0)
            RequestPermissions(permissions.ToArray(), 901);
    });

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == FileSearchStoragePermission)
            UpdateFileSearchAccess();
        if (requestCode == 901)
        {
            connections.Start();
            if (page == "Quick panel")
                RenderBody();
        }
        if (requestCode == 916)
        {
            RefreshAgenda(true);
            if (page == "Root")
                RenderBody();
        }
    }
}
