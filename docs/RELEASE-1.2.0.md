# Launcher 1.2.0, build 5

Published Android APK and self-contained Windows x64 companion on 13 November 2026.

- Compatible icon packs, individual icon selection, tile palettes and per-app badge mute.
- App dashboard with hourly/seven-day usage, version/install source, available storage/data figures and a Play Store QR link.
- Finger-following drawer folding, outlined Focus tiles and flatter Root cards.
- Root command bar, card pinning/reordering/hiding, compact summaries and player/timer actions.
- Multiple titled notes, independent timers with pause/reset and Android alarm notifications.
- Hourly weather curve and seven-day forecast in the selected city's time zone.
- HTTPS RSS, optional Brave Search and YouTube API cards, browser fallback, manual ticket cards with selected attachments.
- Optional local notification message history and supported inline replies.
- Windows companion: pinned TLS pairing, notes with revision conflict protection, browsing and downloading a selected folder, separately enabled uploads, 16 MB transfer cap.
- Whole-display grayscale shortcut using the existing optional display permissions.

## Build and publication checks

Both Release builds compiled successfully with zero warnings and zero errors.
The APK declares com.launcher.home, version 1.2.0, version code 5 and minimum Android API 24.
Its signing certificate matches the previously published APK. Public updater metadata and both download sizes were checked; the previous APK download returns 404.
No functional or device tests were run. Device acceptance testing belongs to the owner.

## Limits and setup

Notification history cannot import private chat history from another app. Replies
require a current Android notification action; Launcher cannot confirm delivery.
History is excluded from layout backups. Tickets are manually entered, not inbox
imports or lock-screen passes. Weather remains Open-Meteo, not a private Google or
Samsung weather service. Inline web/YouTube results need the user's own API keys.
App locks protect Launcher entry points and do not provide systemwide app locking.

The Windows companion is unsigned. Its pairing code expires after ten minutes and
requires a matching certificate fingerprint. It runs only on private IPv4 network
addresses, and the browser UI is bound to loopback. Launcher must remain visible
for a phone connection. File access is scoped to a user-selected Android document
tree; uploads are disabled by default. Restarting the desktop process clears the
session token. Android and Windows firewall/provider behaviour still need the
owner's acceptance testing.

The new gallery images are labelled interface illustrations with sample content.
Archived device captures show 1.0.1. Fresh phone captures require the owner's phone
to be connected; no emulator or unrelated device was used.

QRCoder 1.8.1 supplies QR encoding under its MIT licence:
https://github.com/codebude/QRCoder
