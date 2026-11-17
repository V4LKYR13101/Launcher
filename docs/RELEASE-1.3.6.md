# Launcher 1.3.6, build 16

14 November 2026. Phone resources and page transitions.

- Add card includes Phone resources: battery remaining, memory used and internal data-volume storage used as three accent donut charts.
- Battery state, battery temperature where reported, memory used and storage available appear beneath/alongside the charts. Tap a metric for exact totals and explanations.
- Switch the graph between memory used and battery remaining. The fixed 0-100% scale uses real timestamps, retains up to 15 minutes/91 session samples and leaves gaps when sampling pauses.
- Readings update every 10 seconds while the expanded card is on screen, with no background service or new permissions. Unavailable readings are labelled rather than shown as zero.
- The card is opt-in through Add card or Root settings; pin, arrange, collapse, hide and restore use existing card controls. The resources/device command also enables it.
- Root, Apps and Conversations use a 260 ms direction-aware slide with a restrained fade. The actual outgoing content remains visible during the handover; the status row and bottom bar stay fixed.
- New gestures, rendering, backgrounding and destruction cancel transitions and restore clean view positions. Back/Home navigation uses the same transition where applicable.
- Appearance includes a Page transitions switch, enabled initially. Android's animation duration setting is respected.
- Resource colours follow the selected accent, including light-mode contrast adjustment.

No device or functional tests were run, at the owner's request. Design previews use labelled sample readings rather than device captures.

Release compiled with zero warnings and zero errors after both additions. Package/version/signing identity, public updater and APK, previous APK removal, release notes, article, gallery preview and retained companion download were verified. The chart illustration was inspected separately from native device testing.

APK SHA-256: de7a788742d9888271c834a1aa07337c7b21ea551891a1751b8421c7504b5c92
