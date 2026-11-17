# Launcher 1.3.7, build 17

14 November 2026. Optional layered interface.

- Appearance includes Layered interface, enabled initially. Disable it for the standard long-press editor and flatter drawer/page motion.
- Hold a tile or pinned app to expand a compact app dashboard from its position. The surrounding content recedes; the status and search bar stay fixed.
- The dashboard contains the app identity, drawer, today's usage where permitted, up to four available native shortcuts, Open app, App controls and Usage & details.
- Shortcuts require Android 7.1+ and shortcut-host access. Unsupported apps/platforms omit the section.
- Outside tap, Close and Back animate the dashboard back to the source tile. Launching or navigating closes the layer first.
- Biometric-protected apps authenticate before dashboard details and shortcuts are shown. Credential fallback can resume the pending inspection after returning. Layers are discarded when backgrounded.
- New animations respect Android's duration setting. Interrupted animation, rendering, pause and destruction restore source visibility and reset the recessed page's transforms.
- Background body refreshes are deferred until the layer closes. Horizontal page gestures are not routed through an open layer.
- Page changes gain a restrained 2.5% depth movement; drawer unfolding uses depth and opacity only while the setting is enabled. Resting surfaces stay flat.
- The implementation adds no camera, microphone, AI runtime or model download.

No functional or device tests were run, at the owner's request. Design previews use sample content and are not device screenshots.

Release compiled with zero warnings and zero errors. Package/version/signing identity, public updater and APK, previous APK removal, release notes, article, gallery preview and retained companion download were verified. The illustration was inspected separately from native device testing.

APK SHA-256: 2713fafd94f4dfab7146d94de93fd925e2bdc985622202ae838615c08a1550dc
