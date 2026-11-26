# Launcher

A native Android home screen written in C# and .NET 11 for Android. The app is
named **Launcher**, package `com.launcher.home`. It uses native Android views
and requires no Launcher account. Optional features use the network for weather,
feeds, speech-model downloads and app updates.

Android version in this source tree is **1.6.0 (build 43)**. The Windows
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
adb -s YOUR_SERIAL install -r artifacts/release-1.6.0-build-43/Launcher-1.6.0.apk
adb -s YOUR_SERIAL shell am start -n com.launcher.home/com.launcher.home.MainActivity
```

Choose **Launcher → Settings → Permissions → Default home app** to make it the
home screen. Android's chooser requires the user's selection. Switch back through
Android **Settings → Apps → Default apps → Home app**.

## Language

Launcher follows the first supported language in Android’s preferred language
list and falls back to English. Open **Settings → Language** to choose
**System default**, **English**, or **Español**.

To add a translation, copy the identifiers from
[src/Launcher/Resources/values/ui_strings.xml](src/Launcher/Resources/values/ui_strings.xml)
into the new Android `values-<language>` resource folder, along with the
service descriptions from `values/strings.xml`. Keep numbered
placeholders such as `{0}` and translate the plural forms for that language.
Add the language tag to
[src/Launcher/Resources/xml/launcher_locales.xml](src/Launcher/Resources/xml/launcher_locales.xml)
to expose it in the language picker. English supplies any missing strings.

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
- Hold the file-search icon on Apps or the search icon on Today or Notifications
  for quick theme and focus controls.
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

