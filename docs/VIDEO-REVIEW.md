# Video comparison: Launcher 1.0.1

Reviewed 13 November 2026 against the installed source and release.

The current launcher has the basic drawers, app launching, weather strip,
playback controls, Root cards, notification feed and customisation. It does not
yet reproduce several substantial parts of the reference. The largest gaps are
per-app controls and Root's command-driven card system.

## What was reviewed

[TechAltar's video](https://www.youtube.com/watch?v=VVnmrUmSvqo), published
4 April 2020, runs 11:21. It demonstrates a prerelease launcher, older Blloc
hardware and a separate desktop application. Those are three different scopes.
This is a comparison with that video, not a claim about a current commercial app.

I retrieved the original video and English automatic captions. I reviewed the
caption sequence, 341 timeline samples across the full duration, twenty named
detail captures and four denser motion studies. The motion studies cover drawer
folding, biometric launch, card pinning and the Focus/Sun menu. Timeline contact
sheet times are nominal two-second samples; named details use exact requested
seek times. Sampling supports the visual comparison but is not a frame-perfect
measurement of animation duration or touch latency.

All research images are in
[the video reference index](E:/Repository/launcher/featureImages/video-VVnmrUmSvqo/INDEX.md).
These images are separate from the original Launcher screenshots and mockups
published on Kevlog.

## Feature comparison

| Video evidence | Feature | Launcher 1.0.1 | What remains |
|---|---|---|---|
| [01:40–02:12](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=100s) | App drawers and folding gestures | Partial | Named drawers, tap-to-collapse and reordering exist. The demonstrated pull/fold gesture family and its animated intermediate states do not. |
| [01:54–02:00](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=114s) | Usage totals beside drawer headings and under tiles | Present | Optional usage permission and today's totals are implemented. |
| [02:14–02:28](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=134s) | Rich app information and usage history | Partial | Today's total and a daily goal exist. The hourly chart, weekly comparison, app version/install-source panel, storage/data panel and QR affordance shown in the editor do not. |
| [02:20–02:39](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=140s) | Per-app tile palettes | Partial | Highlight uses the selected accent. The reference describes four tile appearance choices and shows more than a single highlight state. |
| [02:30–02:35](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=150s) | Replace an individual app's icon | Missing | Our geometric symbol picker customises drawers. App icons use each app's own themed/grayscale/original icon; there is no per-app replacement or icon-pack browser. |
| [02:38–03:01](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=158s) | Choose whether each opened application runs in grayscale | Missing | This affects the contents of the running application, not its launcher icon. Our Icon style setting only changes icons. There is no app-display grayscale policy. |
| [03:14–03:25](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=194s) | System grayscale shortcut | Missing | Requires additional Android display-setting access. It is separate from icon theming. |
| [03:26–03:35](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=206s) | Authenticate before launching a chosen app | Missing | A biometric gate can be added to Launcher entry points. It would not secure every other way of opening that application. |
| [03:34–03:56](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=214s) | Per-app notification badge mute | Missing | Global badge visibility and Conversations app filters exist. Neither is a per-app tile badge mute control. |
| [04:00–04:30](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=240s) | Root command bar, suggestions and web results | Missing | Search currently finds installed apps, or notifications on Conversations. Root does not parse commands, create cards or return Qwant/web results. |
| [04:20–04:39](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=260s) | Expanded weather card with graph and week view | Partial | The editable city and accent weather strip exist on the drawers page. There is no built-in Root weather chart, hourly curve or week view. Android widget hosting is an alternative, not the same card. |
| [04:32–04:42](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=272s) | Compact and expanded Root media cards | Partial | Native playback works on the drawers page. Root has no pinned/collapsible built-in player. Our artwork-backed player follows the later screenshot you supplied. |
| [04:43–04:50](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=283s) | Independent timer with pause/reset and typed duration | Partial | A focus-session timer exists with presets. It is not an independent general timer created from a command. |
| [04:48–05:00](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=288s) | Multiple titled notes and expandable note previews | Partial | One local note field exists. Multiple titled notes, a note collection and per-note cards do not. |
| [05:00–05:07](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=300s) | Tickets, RSS and YouTube result cards | Missing | The video describes these integrations and shows Tickets/YouTube content. None is implemented. The mentioned lock-screen ticket behaviour was not independently verified from these frames. |
| [05:08–05:18](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=308s) | Pin, remove and arrange Root cards with compact summaries | Partial | Built-in cards can hide/collapse and Android widgets can reorder. Built-in cards have fixed order and no pinned summary stack with inline actions. |
| [05:42–05:55](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=342s) | Focus and Sun modes | Partial | Both concepts exist. Our Focus filters to Essentials/pinned apps; the reference visibly changes tile presentation. Our Sun theme also lacks the reference's wireframe tile treatment. |
| [06:00–06:52](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=360s) | A complete multi-service chat interface | Different scope | The demonstration is on older Blloc hardware. The presenter explicitly says this Tree is not yet available in the reviewed Ratio preview. Our notification-derived Conversations is not full chat-history sync or an inline messenger. |
| [06:54–07:41](https://www.youtube.com/watch?v=VVnmrUmSvqo&t=414s) | Desktop pairing, photos/files and live note sync | Missing | This belongs to the separate desktop companion. Launcher is currently a standalone Android application with no paired desktop service. |

## The visual differences that matter

The icon treatment is the most obvious gap on the app grid. The reference uses
a consistent thin-outline icon pack, including chosen replacements. Our actual
app icons preserve identity, but grayscale fallback icons can contain filled
circles, varied stroke weights and different silhouettes. A selectable icon pack
with individual overrides would close that gap much more directly than replacing
apps with unrelated generic symbols.

The reference's Root page is flatter and denser. Thin separators and compact
summary rows carry information without a large rounded card around every piece.
An expanded weather curve, a small player summary and a pinned note list can
share the screen. Our Root uses substantial rounded containers with a full
header, padding and one fixed type of content inside. Collapsing currently keeps
the header rather than a useful weather/media/note summary.

The app editor is also less complete visually. The video combines a large app
heading, stats, chart, appearance preview and a compact icon action row. Ours is
a list of labelled settings. A richer editor could retain the bottom-sheet
behaviour you asked for while adding this information hierarchy and a palette
preview. It does not need to return to centred dialogs.

The reference reserves the accent for selected tiles, graph points, temperature,
playback and small active controls. We have accent-aware weather, badges, battery
and controls, but lack the graph and per-app palette details. New work should
continue using the selected accent and contrast-aware text.

The outlined Focus tiles and almost white Sun layout have their own identity in
the video. A white background alone does not recreate that treatment. Drawer
folding and the quick corner menu also have visible transitions; our drawers
currently rebuild their contents immediately.

Several differences are intentional results of your later instructions: the
weather strip and player belong on the app-folders page, the player uses artwork
behind its controls, Like and Shuffle are removed, page indicators are removed,
and sheets must be bottom anchored and dismissible. Those choices should remain.

## Per-app grayscale: the required distinction

A per-app setting must mean that the running application is grayscale, with
exceptions such as Camera or Photos staying in colour. It must not merely swap
the tile icon to gray. Launcher does not currently implement this.

The ordinary Home role grants no permission to recolour other application
windows. Android documents secure system settings as unavailable to normal
third-party applications. A practical implementation needs an explicit extra
setup path, a reliable way to follow the foreground app, and verification on
the target Samsung firmware. An ADB-granted secure-settings route is a candidate,
not a feature that should be promised without a device test. Leaving the chosen
app must restore the previous display setting, and existing accessibility colour
correction must be respected. Multi-window and service shutdown need explicit
behaviour too.

[Android secure-settings permission documentation](https://developer.android.com/reference/android/Manifest.permission#WRITE_SECURE_SETTINGS)
supports the platform restriction. [Android's biometric API](https://developer.android.com/identity/sign-in/biometric-auth)
supports a launcher-local authentication prompt; it does not make Launcher a
system-wide application lock.

## Recommended order

1. Per-app editor: real icon overrides, tile palettes, per-app badge muting and
   a clearly scoped biometric launch gate.
2. A separate opt-in setup for true per-app grayscale, tested on the Galaxy A26
   before advertising support. Keep icon colour and opened-app colour separate.
3. Root card model: reorder/pin/remove, useful collapsed summaries and multiple
   named notes. Then add independent timers and the command bar.
4. Forecast charts and usage history, followed by drawer folding animations and
   a more faithful compact/outline visual preset.
5. Tickets, feeds and desktop sync as separate integrations with their own data
   and permission requirements.

This review identifies missing work. It does not describe those missing features
as already shipped in the published APK.

## Evidence in the current code

- [App editor and icon rendering](E:/Repository/launcher/src/Launcher/MainActivity.Tiles.cs:279)
- [Root cards and Conversations](E:/Repository/launcher/src/Launcher/MainActivity.Root.cs:18)
- [Appearance options](E:/Repository/launcher/src/Launcher/MainActivity.Settings.cs:120)
- [Today's usage aggregation](E:/Repository/launcher/src/Launcher/Services/AppCatalog.cs:34)
- [Current media card](E:/Repository/launcher/src/Launcher/MainActivity.PlaybackCard.cs:25)
- [Current application state](E:/Repository/launcher/src/Launcher.Core/LauncherState.cs:38)
