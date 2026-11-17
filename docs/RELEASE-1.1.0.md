# Launcher 1.1.0, build 3

13 November 2026.

- Biometric launch and app-editor authentication, with screen-lock fallback.
- Accent fingerprint backgrounds fill the tile at any aspect ratio. A cached,
  continuous ridge path replaces the overlapping curves.
- Per-application display grayscale choices and an optional foreground-window
  service, with setup status and restore controls. Icon colour stays separate.
- Larger app icons: 52 dp in the four-column layout, 44 dp in five columns and
  38 dp in six columns. Wide tiles use a 44 dp icon.
- Flatter Root cards, outlined Focus/Sun tiles and animated drawer folding.
- A stable wallpaper window surface, preference-only wallpaper flag changes and
  transparent scrolling without overscroll stretch.
- Classic Visual Studio solution with Android deployment enabled.

Debug solution and Release app builds completed without errors or warnings.
No unit tests, emulator tests or phone functional tests were run, at the user's
request. A local geometry preview was used to inspect the fingerprint drawing.
Phone installation and publication are delivery operations, separate from device
acceptance testing.

Grayscale needs accessibility enablement and a one-time secure-settings grant.
The owner will test firmware behaviour on the Samsung phone. The implementation
does not claim native per-window colour isolation in split screen.

The public release publisher replaces the download button and update manifest,
then removes the old APK from live storage. The previous database, manifest and
APK remain only in a private server rollback backup.
