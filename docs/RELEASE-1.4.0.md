# Launcher 1.4.0 (24)

- Optional global accessibility-overlay panel, disabled by default, with separate access from grayscale.
- Small edge activation handle, touch-free optional status strip, preserved native indicators and direct access to the native shade.
- Current notifications grouped by app, monochrome source icons, source-app opening, expansion, replies and clearable swipe dismissal.
- Artwork media card, transport controls, seeking, media volume, permission-gated brightness and native control shortcuts.
- Master disable, light/dark override, opacity, animation, status placement, handle side and control-order presets.
- Locked-app entry points return through Launcher authentication. Overlay window events do not replace the underlying app's grayscale rule; opening the panel temporarily restores colour.
- Media monitoring observes candidate sessions before their first playback state arrives, fixing missed cold-start players in both Launcher and the global panel.
- Window ownership covers registered windows before the first frame, with explicit cleanup on configuration changes and disable.
- Larger panel created on demand; first notification batch bounded to 20 with Show more. No extra process, blur, camera or global gesture interception. Media monitoring runs only while the global panel is open.

Validation is recorded in artifacts/system-panel-1.4.0. Samsung A26 / One UI 9 behaviour and battery impact still require physical-device verification. The implementation does not re-theme Samsung SystemUI.
