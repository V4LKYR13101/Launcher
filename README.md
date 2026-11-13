# Launcher

A native Android home screen written in C# and .NET 11 for Android. The app is
named **Launcher**, package `com.launcher.home`. No accounts or ads. Network access
is used for optional Open-Meteo weather and manual update checks/downloads from
Kevlog. UI uses native Android views.

Current release: **1.0.1 (build 2)**. Settings → App updates checks
`https://kevlog.co.za/updates/launcher.json`, shows a newer version, downloads with
progress, verifies size/SHA-256, package/version and signing certificate, then
opens Android's installer. Installation needs the user's Android approval.
The updater supports Android 7+, including the per-source installation setting
on Android 8+. Existing layout and widget bindings are preserved on update.

Project, APK and gallery: https://kevlog.co.za/projects/launcher/
Article: https://kevlog.co.za/blog/building-launcher-for-android/

## Run

Open `Launcher.slnx` in Visual Studio with the Android workload, or:

```powershell
dotnet workload install android
dotnet build src/Launcher/Launcher.csproj
dotnet build src/Launcher/Launcher.csproj -c Release
dotnet run --project tests/Launcher.Core.Tests -c Release
```

This workspace pins SDK `11.0.100`. Builds need Android SDK platform 37 and JDK
21. If `JAVA_HOME` points to an older JDK, pass
`-p:JavaSdkDirectory="C:\path\to\jdk-21"`. The current machine uses
`C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot`.

An installable signed APK is produced under
`src/Launcher/bin/Release/net11.0-android/com.launcher.home-Signed.apk`.
The delivered copy is `artifacts/Launcher.apk`. This build uses the development
signing key; use a dedicated signing key for distribution.

```powershell
adb devices -l
adb -s YOUR_SERIAL install -r artifacts/Launcher.apk
adb -s YOUR_SERIAL shell am start -n com.launcher.home/com.launcher.home.MainActivity
```

Choose **Launcher → Settings → Permissions → Default home app** to make it the
home screen. Android's chooser requires the user's selection. Switch back through
Android **Settings → Apps → Default apps → Home app**.

## Controls

- Tap drawer headings to expand or collapse. Hold a heading to rename, select
  apps, reorder or remove the drawer. Removing a drawer rehomes apps to App box.
- Tap a tile to launch its real Android activity. Hold to rename, move, highlight,
  pin, expand, hide, reorder or set a daily usage goal.
- The bottom search pill searches installed apps, original names and custom
  labels. Hidden apps and apps excluded by focus do not appear in search.
- Swipe right for **Root** and left for **Conversations**, or use the search menu.
- Hold the search icon or tap the three dots for quick theme and focus controls.
- Pull down within Tiles when scrolled to the top, or tap the small status row,
  to open **Launcher’s quick panel**.
- Settings offers Classic/Studio presets, 3–6 columns, tile heights and corners,
  custom accent colors, heading alignment, labels, actual monochrome/grayscale/
  original app icons, system wallpaper and a minimal status row.
- Root includes a calendar, local notes, arithmetic calculator, focus timer and
  native Android widget hosting. Hold widget headers for resizing and order.
- Each built-in Root card, including Focus, can be hidden in Root settings.
- Drawer headings have customizable geometric icons. Hold a heading and choose
  **Drawer icon** for the symbol grid.
- **Settings → Weather** configures the city, °C/°F and refresh interval, including
  Manual only. Cached weather refreshes while Launcher is visible, with no
  background polling service. A matching strip appears above drawers.
- An active media session appears below weather with artwork, metadata, seek/time
  display and playback controls. Repeat depends on the player's exposed actions;
  unavailable actions are disabled.
- **Settings → Appearance** selects the status row's top-corner or below-cutout
  position. The accent battery icon follows the actual level and pulses while charging.
- App tiles and pinned apps show accent-colored notification counts when optional
  notification access is enabled. Counts reflect active clearable notifications,
  not private unread databases inside other apps.
- Back up or restore your settings and notes through Android's document picker.
  Widget bindings stay on their original device and are preserved on restore.

## Platform boundaries

This is a home-screen replacement. Android owns the system status bar,
notification shade, lock screen, recent-apps screen and navigation gestures.
Launcher can hide the status bar while showing its own minimal row; the Android
shade remains available at the screen edge. The Launcher quick panel complements
it and opens Android's connection, sound and display controls. It does not theme
or replace the OS shade. Active quick controls use your selected accent, with
black or white foregrounds selected by relative-luminance contrast. Wi-Fi and
Bluetooth names require optional precise-location and nearby-device permissions;
no location coordinates are requested. The quick panel follows upward drags to
close from the header or bottom handle. The notification feed scrolls separately.
Notification cards swipe left or right to dismiss when Android permits it.

In-app editors and pickers use bottom-anchored sheets that follow downward handle
drags. Short drags spring back; longer drags close the sheet. System-owned
permission prompts, app-widget setup screens and document pickers retain Android's UI.

Monochrome icons use each app's Android themed drawable if provided; otherwise
the app's original icon is desaturated. Generic category glyphs are used only for
Launcher's own controls, never as substitutes for installed app identities.

Usage access is optional and enables local event-based foreground usage since
midnight. These totals depend on Android's retained usage events and may differ
from the manufacturer's Digital Wellbeing figures. Goals are reminders at launch,
not app blocking. Focus filters Launcher to Essentials and pinned apps; it does
not silence Android notifications or enforce a system app restriction.

Notification access is optional. The quick panel shows current notifications;
Conversations supports a persistent app-source allowlist, all notifications or
messages only, and opening or dismissing current notifications. No historical
chats, contact merging or inline replies. Notification data is
held in memory, never uploaded. Android widgets are supplied by installed apps;
their internal appearance remains under their provider's control. Existing weather
widgets can be hosted instead of the custom network-backed weather strip.

This version targets the current Android user. Work profiles, private space,
third-party icon packs, app locking and drag-and-drop placement are not included.
Drawer and tile ordering is edited through long-press controls.

## Source and validation

- `src/Launcher`: Android activity, native UI, app catalog, notification listener,
  optional usage integration and preference storage.
- `src/Launcher.Core`: layout state, safe backup parsing, organization, search,
  focus filtering and arithmetic parser.
- `tests/Launcher.Core.Tests`: dependency-free behavioral checks.
- `tools/device_qa.py`: serial-specific adb UI inspection, screenshots and input.
- `featureImages`: downloaded research references, contact sheets and source index.
- `docs/VISUAL-RESEARCH.md`: version-aware visual observations and decisions.

Research images are not application assets. Typeface: Outfit, bundled under its
SIL Open Font License in `src/Launcher/Assets/Outfit-OFL.txt`.
