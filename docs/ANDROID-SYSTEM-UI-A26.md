# Android system customisation for Launcher

Reviewed 14 November 2026. Target reported by the user: Samsung Galaxy A26, One UI 9. Android SDK, build number and regional/carrier firmware have not been read from that phone. No physical device is currently connected. Good Lock is excluded as a dependency.

This document concerns the real status bar, notification shade, quick settings and other Android surfaces outside Launcher. It does not describe changes to Launcher's existing in-app quick panel as changes to Samsung SystemUI.

Launcher 1.4.0 now implements the optional custom panel described here. Setup and current boundaries are documented in [SYSTEM-PANEL.md](SYSTEM-PANEL.md). Samsung A26 / One UI 9 compatibility still needs physical verification.

## The practical answer

A coherent appearance across other apps is possible through a custom, optional system-panel component plus supported native settings. Completely re-skinning Samsung's original notification shade is a different level of access.

One-time ADB grants and Shizuku unlock useful settings and operations. They do not grant Samsung's platform signing identity. Permission to write secure settings is not permission to replace every SystemUI colour, icon, font or layout.

The strongest portable route without Good Lock is to build our own lightweight status/panel presentation, with explicit user enablement, notification access and an appropriate overlay/accessibility service. A compatibility audit is still needed to determine where the native bar can be hidden or visually covered and how the native shade remains accessible on the actual firmware.

## What the different access levels mean

| Access level | Useful capability | Boundary |
|---|---|---|
| Ordinary Launcher | Complete styling of its own pages and own-window bars; wallpaper selection with appropriate user flow | Does not re-style another app's window or Samsung's shade |
| Notification listener | Available notification data, dismiss actions and supported replies; active media access | Does not control the native card layout or provide complete private chat history |
| Overlay permission | Draw our own panel over ordinary apps | Normal application overlays are below critical system windows such as the status bar and keyboard |
| Explicit accessibility service | A separate accessibility-overlay path and supported global navigation actions | Greater setup/access implications; OEM behaviour and protected screens need testing; not an unrestricted native theme API |
| ADB secure-settings grant | Write settings the platform permits and the firmware actually consumes | A key that can be written may be ignored; cannot grant arbitrary signature permissions |
| Shizuku started through ADB | Execute supported operations as the shell user, where Android permits them | Shell is not root; permission and SELinux boundaries remain; startup commonly needs repeating after reboot |
| Root/compatible privileged modifications | Potentially overlay or hook actual SystemUI resources and behaviour | Firmware-specific maintenance; not all resources are overlayable; device-specific risks and bootloader availability |
| OEM-signed integration/custom system build | Deep native integration | Not obtainable by adding a permission or running an ordinary grant command |

## Surface-by-surface inventory

| Surface | What we can aim for without Good Lock | Native/terminal boundary |
|---|---|---|
| Status clock and battery appearance across apps | Own live status presentation, selected accent, font and truthful battery state | Native clock font/position and battery artwork need specific firmware controls or privileged modifications |
| Unwanted status icons | Optional native icon-visibility controls where supported | icon_blacklist and similar keys are private/OEM-dependent; not verified on this A26 |
| Native battery percentage | Open the native option, or use an applicable supported setting | Keys vary; do not assume one Samsung/AOSP key works everywhere |
| Status bar visibility | Own-window immersive mode; investigate supported per-app/system settings | Global policy_control-style tricks are not a universal modern Android solution |
| Notification shade background and typography | Our own panel using Launcher colours, spacing and fonts | Arbitrary re-skinning of Samsung's real panel is not enabled just by WRITE_SECURE_SETTINGS |
| Notification rows | Own view of notifications with real app icons, replies, grouping, dismiss and media controls | Their presentation in Samsung's real shade is owned by SystemUI |
| Quick settings arrangement | User-managed stock arrangement; inspect firmware-provided tile settings | Column counts, spacing and header dimensions are not a stable cross-device settings API |
| Launcher quick-settings tile | Add a supported native TileService entry for a Launcher action, subject to user addition | It occupies a native tile; it does not re-theme the rest of the shade |
| Wi-Fi and Bluetooth actions | Show states/names with permissions and use supported settings panels; shell-assisted controls require separate verification | Normal apps cannot freely toggle all radios on recent Android |
| Brightness and sound | Supported controls with the relevant access; consistent own-panel sliders | Whole-system brightness settings and own-window brightness are different; volume UI appearance remains OS-owned |
| System colour palette | Supported apps may follow Android's dynamic colours; user-selected wallpaper/palette can coordinate the phone | One UI's own palette path differs from generic Material You; cannot promise programmatic Samsung shade recolouring |
| System fonts and emoji | User-supported device settings; custom-font routes must be verified per firmware | Launcher font selection alone does not change other apps or SystemUI |
| Navigation buttons/gesture indicator | Own-window appearance where allowed; expose supported system navigation settings | Exact shapes, global colours and system gesture behaviour need OEM/privileged support |
| Recents view and app-launch transitions | Keep Launcher's own transitions consistent | System Recents is normally a privileged launcher/SystemUI integration, not replaced by setting the Home role |
| Lock screen and always-on display | Own standard notifications with selected privacy and actions; coordinate wallpaper through supported flows | Cannot claim a normal launcher replaces Samsung keyguard/AOD |
| Volume panel | A custom optional presentation is a separate overlay feature | Native panel layout/theming is SystemUI-owned; global key interception needs its own access/compatibility work |
| Do Not Disturb | An explicit user-authorised mode/rule integration | Recent Android versions use app-owned rules and retain user overrides; not arbitrary global policy ownership |
| Other apps' icons | Installed icon packs inside Launcher and supported launcher shortcuts | Does not replace icons in every app, Settings or native notifications |

## Samsung-specific evidence

ColorBlendr's current maintainer documentation explicitly distinguishes Material You changes from Samsung's own palette used by its apps and quick settings. It reports that those Samsung surfaces require Samsung-signed permissions. This is a concrete reason not to promise full One UI shade recolouring through Shizuku. It is maintainer evidence, not a successful test on this phone.

The same documentation reports additional One UI 8.5+ restrictions around Dual Messenger and colour changes. Do not delete profiles or disable the user's apps as part of this project. Any compatibility investigation should begin with read-only inspection and the exact firmware build.

## Proposed custom component for low-end devices

The custom overlay component is implemented in 1.4.0. Native Samsung theme modifications and firmware-specific terminal controls remain separate research.

1. **Shared palette and data models.** Reuse Launcher's accent and typography without coupling the service to MainActivity or retaining its entire view tree.
2. **Optional global status presentation.** Live time, battery and supported connection indicators. Treat cutouts, rotation and native reserved insets explicitly. Ordinary application overlays cannot simply replace the critical native status window.
3. **A custom notification/quick panel.** Reuse notification/media models with a dedicated native view. Actual operations use notification actions and supported system APIs, not simulated values.
4. **A documented activation area.** Restrict interception to an explicitly enabled handle/region. Do not consume app scrolling, Launcher page swipes or bottom navigation. Retain a clear route to the native panel and a quick disable action.
5. **Permission and firmware checks.** Enable only verified operations. Distinguish permission granted, setting written and visually verified behaviour.
6. **Screen and lifecycle handling.** Stop optional drawing/updates while the screen is off and yield appropriately to keyguard, authentication and sensitive system windows. Real Samsung testing is required.
7. **Low-cost rendering.** Flat native surfaces by default, no continuous blur, no camera processing and no duplicate .NET runtime/process merely for styling. Bound caches; create the larger panel only when needed; update the clock at minute boundaries unless seconds are explicitly enabled.
8. **Restoration.** Record original native values before supported changes, scope each modification and provide Restore Android defaults. Never disable or replace Samsung SystemUI as a workaround.

Memory, battery use and reliability have not been measured for this proposed component. Low-end suitability must be established through traces and device tests rather than promised from its design.

## Terminal setup

The supplied audit is read-only. It collects model/build information, a small list of relevant settings, overlay registrations and supported status-bar command help. It does not dump all user settings or notification content.

From this repository on a computer with ADB:

```powershell
adb devices
pwsh -File .\tools\Audit-AndroidSystemUi.ps1 -Serial YOUR_ADB_SERIAL
```

An emulator run validates the audit script itself. It does not validate Samsung's private controls. The report intentionally lists no verified visual changes.

Where needed, Launcher already declares the secure-settings permission used by its grayscale setup. The relevant grant is:

```text
adb -s YOUR_ADB_SERIAL shell pm grant com.launcher.home android.permission.WRITE_SECURE_SETTINGS
```

This command is not an instruction to apply a theme and is not required for haptic feedback. It does not grant Samsung-signed permissions. No physical-device grant was executed during this task. Existing grayscale users should not revoke it casually, because that would affect the feature they enabled.

For Shizuku, use the startup command shown by the installed Shizuku application rather than assuming a permanent service or a fixed command for every version. ADB-mode startup commonly has to be repeated after reboot.

Do not run bulk scripts that overwrite enabled accessibility services, replace all quick-setting tiles, force global immersive mode, disable SystemUI, remove user profiles or enable arbitrary overlays. A future setup tool should inspect, back up and modify only a specifically supported setting after the user selects it.

## Root route

If the requirement is to alter the actual Samsung status bar and shade rather than present a custom counterpart, root/privileged integration is the route to investigate next. RRO can change eligible resources; behaviour changes may need compatible hooks or a system build. Resource names and integration points can change between firmware releases.

Samsung documents that rooting/unapproved boot states can trip the Knox Warranty Bit, affecting Knox-backed features. Bootloader unlock availability must be checked for the exact device variant. Terminal willingness is not authorisation to unlock, flash or root the phone, and none of those operations were attempted here.

## Sources

- [Android application overlay ordering](https://developer.android.com/reference/android/view/WindowManager.LayoutParams)
- [Android accessibility services and overlays](https://developer.android.com/reference/android/accessibilityservice/AccessibilityService)
- [AOSP window layer policy](https://android.googlesource.com/platform/frameworks/base/+/bafc1f5a4e19842582a55ec1aae02e00d0b12f64/services/core/java/com/android/server/policy/WindowManagerPolicy.java)
- [Shizuku privilege model](https://github.com/RikkaApps/Shizuku-API#differents-of-the-privilege-betweent-adb-and-root)
- [Shizuku startup lifecycle](https://shizuku.rikka.app/guide/setup/)
- [AOSP resource overlay policies](https://source.android.com/docs/core/runtime/rros)
- [AOSP fabricated-overlay shell implementation](https://android.googlesource.com/platform/frameworks/base/+/master/services/core/java/com/android/server/om/OverlayManagerShellCommand.java)
- [ColorBlendr maintainer's One UI compatibility notes](https://github.com/Mahmud0808/ColorBlendr#faq-)
- [SystemUI Tuner's declared permissions and feature limitations](https://github.com/zacharee/Tweaker/blob/master/app/src/main/assets/terms.md)
- [Current Android notification-policy behaviour](https://developer.android.com/reference/android/app/NotificationManager)
- [Samsung Knox warranty-bit explanation](https://docs.samsungknox.com/admin/knox-platform-for-enterprise/faqs/faq-115013719548.htm/)
