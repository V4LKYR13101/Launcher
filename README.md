# Launcher

A native Android home screen written in C# and .NET 11 for Android. The app is
named **Launcher**, package `com.launcher.home`. It uses native Android views
and requires no Launcher account. Optional features use the network for weather,
feeds, speech-model downloads and app updates.

Android version in this source tree is **1.5.14 (build 42)**. The Windows
companion is **1.2.0**. Published APKs are listed in
[GitHub Releases](https://github.com/V4LKYR13101/Launcher/releases).
**Settings → App updates** checks
`https://kevlog.co.za/updates/launcher.json`, shows a newer version, downloads with
progress, verifies size/SHA-256, package/version and signing certificate, then
opens Android's installer. Installation needs the user's Android approval.
The updater supports Android 7+, including the per-source installation setting
on Android 8+. Existing layout and widget bindings are preserved on update.

Project, APK and gallery: https://kevlog.co.za/projects/launcher/
Article: https://kevlog.co.za/blog/building-launcher-for-android/

## Run

Open `Launcher.sln` in Visual Studio with the Android workload, or:

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

A local Release build produces the development-signed APK at
`src/Launcher/bin/Release/net11.0-android/com.launcher.home-Signed.apk`.
To build an update for an existing production installation, run
`tools/Build-LauncherRelease.ps1` on the signing workstation. It uses the
project version and the production signing lineage, verifies the APK signature
and alignment, and writes the APK and SHA-256 metadata to
`artifacts/release-<version>-build-<number>/`.

```powershell
adb devices -l
adb -s YOUR_SERIAL install -r artifacts/release-1.5.14-build-42/Launcher-1.5.14.apk
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
  labels. Use its files toggle to search shared storage on the device after
  granting Android file access. Hidden apps remain excluded from app search.
- Swipe up from Home to open the app drawer, swipe left to open **Notifications**,
  or swipe right to open **Today** (the Root page). **Settings → Swipe right on
  Home** can open Google Discover through the installed Google app. Today remains
  available in the Launcher menu.
- Hold the search icon or tap the three dots for quick theme and focus controls.
- Pull down within Tiles when scrolled to the top, or tap the small status row,
  to open **Launcher's quick panel**. Controls and notifications scroll together.
- Settings offers Classic/Studio presets, 3–6 columns, tile heights and corners,
  custom accent colors, heading alignment, labels, actual monochrome/grayscale/
  original app icons, system wallpaper and a minimal status row.
- **Settings → Appearance → Liquid glass cards** controls the glass appearance
  of Today cards, notification groups, app tiles, dock icons and search bars on
  Android 13 and later. It defaults to on when no preference has been saved.
- Notifications are grouped by app and ordered by the newest item in each group.
  Groups with multiple items show a count and stack beneath the newest preview.
  Tap the app header to expand or collapse the stack, or tap a notification to
  open it. Hold a notification on the Notifications page for speech and other
  available actions.
- Root includes a calendar, local notes, arithmetic calculator, focus timer and
  native Android widget hosting. Hold widget headers for resizing and order.
- Each built-in Root card, including Focus, can be hidden in Root settings.
- Drawer headings have customizable geometric icons. Hold a heading and choose
  **Drawer icon** for the symbol grid.
- **Settings → Cards, feeds & weather** opens weather configuration for the city,
  °C/°F and refresh interval, including
  Manual only. Cached weather refreshes while Launcher is visible, with no
  background polling service. A matching strip appears above drawers.
- An active media session appears below weather with artwork, metadata, seek/time
  display and playback controls. Repeat depends on the player's exposed actions;
  unavailable actions are disabled.
- **Settings → Appearance → Navigation & status** selects the status row's top-corner or below-cutout
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
no location coordinates are requested. Close the quick panel with its Back
button or an upward swipe starting on the header. The content scrolls down to
the Android navigation area.
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
not app blocking. Focus gives app tiles an outlined appearance; it does
not silence Android notifications or enforce a system app restriction.

Notification access is optional. The Notifications page supports app-source
filters, a messages-only view and opening or dismissing active notifications.
Replies require an active notification with an Android reply action. Optional
message history stores notification-derived messages locally. Android widgets
are supplied by installed apps;
their internal appearance remains under their provider's control. Existing weather
widgets can be hosted instead of the custom network-backed weather strip.

Work profiles and Private Space depend on Android's profile visibility and
launcher access. Compatible icon packs and biometric app locks are configured
through the app editor. Use **Edit home layout** or the long-press controls to
arrange drawers, tiles and cards.

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


## Launcher 1.2.0 and Windows companion

Root now supports titled notes, independent timers, card pinning and arrangement,
weather charts, RSS, tickets and online search cards. Hold a card header for its
options. Web and YouTube inline results need the optional provider API keys in
Settings. Browser search remains available without keys.

Compatible icon packs, individual icon choices, per-app tile palettes and badge
muting are in the app editor. The app dashboard includes usage history and QR
sharing. Message history is opt-in, notification-derived and limited to 30 days
or 5,000 messages. Replies depend on a live notification reply action.

Download `LauncherCompanion.exe` from Kevlog and run it on Windows x64. It includes
the .NET runtime. Pair from Launcher Settings > Desktop companion on the same
private Wi-Fi network; compare the certificate fingerprint before entering the
code. The connection runs while Launcher is visible. Select one folder and
separately allow uploads if wanted. Transfers are limited to 16 MB and there is
no desktop file-delete operation. Restarting the desktop process requires a new
pairing. Windows may ask to allow private-network access. The executable is not
code signed.

Build the companion with `dotnet publish src/Launcher.Companion/Launcher.Companion.csproj
-c Release -r win-x64 --self-contained true`. The Android project remains the
startup project in Visual Studio.

Release 1.2.0 was compiled without running functional or device tests, as requested.
See [the release notes](docs/RELEASE-1.2.0.md) for scope and platform limits.


### Android 1.2.1

Root uses compact action rows and smaller section labels. Weather separates the
location, temperature and conditions, and the date itself opens the calendar.
The app dashboard has one title, grouped metadata, chart scales and precise
short-session durations. Usage tracks the union of an app's resumed activities,
so an older screen pausing does not end another active screen's session.
Horizontal swipes on drawer headings return to page navigation. Tile usage text
sits closer to the app label with eight dp of space below it.

The latest project page has thirteen new labelled mockups plus archived captures:
https://kevlog.co.za/projects/launcher/


### Android 1.2.2

Timer permissions are under Settings > Timer alerts. The Root page footer keeps
Add card, Arrange and Add widget on one compact row. The Windows companion
remains 1.2.0.


### Android 1.2.4

Shared controls, settings, sheets, notification cards and the app dashboard have
refined spacing and hierarchy. Add card uses a labelled icon grid. Notification
taps open the source app while retaining the Launcher biometric gate. Locked
tiles use fine fingerprint contours, and blank icon masks fall back to usable
native artwork or app initials.

Settings > News & RSS feeds manages sources. Google News South Africa is enabled
initially; optional presets include MyBroadband, BBC World, The Verge and Ars
Technica. Custom RSS/Atom sources can be added, edited, disabled or removed.
Enabled feeds merge into one Root card, newest first, with undated items last.
RSS requires no API key. Source requests are bounded and limited to three at a
time. The card loads on opening or manual refresh, with a 15-minute freshness
check when it is rebuilt; it does not run a background polling service.

Settings > Drawers > Drag headings to fold is optional and defaults off. Tap-to-
fold remains available. Appearance also includes wallpaper dimming for readability.

No functional or device tests were run for this release, at the owner's request.
Release builds and publication metadata were verified. Exact visual parity and
production acceptance still require the owner's phone review.


### Android 1.3.0

Google Material, Phosphor and Bootstrap libraries are bundled offline. Choose a
global library in Appearance, or hold an app tile and choose App icon for an
individual selection. Drawer icons offer Browse icon libraries as well as the
existing geometric symbols. Installed compatible icon packs remain supported.
Global matching preserves native icons when a recognised match is unavailable.

Settings > Automatic theme optionally follows the ambient-light sensor only.
It defaults off, with Light at 1000 lux, Dark at 300 lux and a five-second delay.
Thresholds and delay are configurable. Manual theme choices disable automatic
switching. The sensor runs only while Launcher is visible; no camera is used.

The Root weather card no longer shows Refresh or City & units. Weather settings
retain those controls. See docs/ICON-LIBRARIES.json for pinned asset provenance and
docs/RELEASE-1.3.0.md for implementation and validation scope.


### Android 1.3.1

The icon picker pins search and its result count above the grid. It uses compact
34 dp previews, two-line labels and selected-icon outlines. Common symbols and
recognised app matches appear first; specialised numbered symbols remain in the
full searchable catalogue. Bundled fonts and app-tile rendering are unchanged.


### Android 1.3.11

Optical icon sizing, consistent card spacing, clearer notification hierarchy, anchored form actions and more stable refreshes. Native emulator checks and screenshots cover themes, larger text, small screens, sheets, playback, notifications and feeds. See [release notes](docs/RELEASE-1.3.11.md) and [UI review](docs/UI-REVIEW-1.3.11.md).

### Android 1.3.12

Single-word app labels shorten cleanly at larger text sizes. Multiword labels can use two lines. See docs/RELEASE-1.3.12.md.

### Android 1.3.13

Choose Off, Subtle or Crisp interaction feedback in Settings. Effects follow Android touch-vibration policy. See docs/RELEASE-1.3.13.md and docs/ANDROID-SYSTEM-UI-A26.md.


### 1.4.0: optional system panel

Settings > System panel adds a separate master switch and access setup for a matching notification/quick panel across apps. The status strip is separately optional. Native Android controls remain available. See [setup and boundaries](docs/SYSTEM-PANEL.md).

Controls include notification opening, grouping, inline replies, swipe dismissal, media playback, media volume, permission-gated brightness and native settings links. Appearance supports a light/dark override, opacity, animation, handle placement and control-order presets. The service reads foreground window changes without retrieving screen text. Screen off hides its windows; Disable removes them and stops its listeners. Biometric app controls and grayscale rules remain separate.


### 1.4.1: tile icon size

Appearance > Tile icon size provides a 60 to 150 percent slider, live preview, Cancel, Save and Reset to default. Icons fit the available tile space. Tile dimensions and grid spacing stay unchanged. The preference is saved in layout backups.
