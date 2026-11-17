# Launcher 1.3.11 (21)

## Interface
- Optical app-icon sizing uses visible artwork and caches its measurements. Narrow grids keep icons inside the tile.
- Tighter drawer composition, stable drawer sections, touch feedback and consistent 240 ms fold timing.
- Consistent Root header/content/divider spacing, outlined Focus choices and quieter secondary actions.
- Conversations keeps chronological order with date labels, closer consecutive cards, clear application names and smaller reply controls.
- Shared sheets have anchored footer actions, keyboard clearance, consistent entrance motion and a maximum width of 640 dp. Motion honours the layering switch and Android animation setting.
- Journey and reply forms use labelled, accent-aware fields and fixed actions. Settings pages have one title rather than two.
- The playback seek handle no longer clips at either end of its track.
- Singular/plural reading counts use natural wording.

## Continuity
- Body refreshes wait until touch, flings, folds and sheets settle.
- Root and Apps preserve the visible card or drawer when refreshed content above changes height.
- Unchanged feed rows retain their loaded thumbnails. Feed refreshes preserve the visible reading anchor, and failed updates retain saved stories.

## Validation
Release build, APK identity and signing checks. Native Android 15 emulator checks cover dark/light themes, 150% text, drawer folding, page swipes from headings, keyboard clearance, quick-panel dismissal, media controls and card opening, notification opening/swipe dismissal, feed refresh and image preferences. Controlled sample notifications and a MediaSession come from a separate QA fixture, excluded from the Launcher APK. No physical phone used. Windows companion remains 1.2.0.
