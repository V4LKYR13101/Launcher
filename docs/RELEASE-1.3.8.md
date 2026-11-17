# Launcher 1.3.8, build 18

14 November 2026. Everyday layers, optional tilt and compact app actions.

- Layered interface is the master switch for app expansion, page perspective, drawer/card reveals, scroll-edge masking and optional tilt. Off restores immediate page/card changes, flat drawer toggles and the standard app editor. Turning it off stops sensors and active layer transforms.
- Existing horizontal swipe thresholds, vertical scrolling, tap routing and optional drawer-heading drag recognition are retained. No new navigation gesture or interception rule is introduced. The touch dispatcher only records contact state to freeze visual tilt until release/cancel.
- Tilt depth is a separate opt-in setting, off initially, using game rotation vector or rotation vector sensors. Phones without a sensor retain other layers. No camera or microphone is used and no new permission is needed.
- Tilt uses a smoothed, bounded response and calibrates at activation and display rotation. Content and headings occupy slightly different visual planes while status/search remain fixed. It stops outside visible home pages, during dialogs, app inspection and when backgrounded. It pauses during touches and layer animations.
- Root cards crossfade and fold content beneath fixed headings, with clipped reveal, restrained perspective and animated height. Repeated toggles cancel old transitions; cleanup restores natural height and final transforms.
- Drawer apps reveal with row/column offsets, a small lift and scale, and reset fully at rest. Existing heading gestures retain their existing routing and threshold behaviour.
- Page transitions use edge-based perspective and separate content parallax after the existing swipe is recognised. They do not follow or capture new finger movement. Status and bottom controls remain fixed.
- App actions are smaller, edge-anchored and use a tile-like surface, inline usage, up to three native shortcuts and compact Open/Edit/Details actions. The large usage block, bright full-width launch button and Close footer are removed; an accessible close icon remains.
- App authentication, exact source-tile restoration and deferred background refreshes are retained. Native shortcuts still depend on Android/app support.

No functional or device tests were run, at the owner's request. Source review verifies preserved gesture recognition; physical-device feel and sensor behaviour require owner acceptance testing. Previews are labelled illustrations.

Release compiled with zero warnings and zero errors. Source review confirmed retained gesture-routing thresholds and no new depth touch listener. Package/version/signing identity, public updater/APK, previous APK removal, release notes, article, gallery and retained companion were verified. The illustration was inspected separately from device testing.

APK SHA-256: c78c64e93a620bf66e52ff0d98c7308bb65dd682b9200d2c5673c36717f1f748
