# Launcher 1.2.1, build 6

Published on 13 November 2026. Android-only update; Windows companion stays on 1.2.0.

- Root section labels are smaller, spaced and muted; note titles remain distinct.
- Calendar opens from the date. Secondary actions use compact rows instead of stacked full-width pills.
- Weather separates its location, accent temperature and conditions. Forecast rows are compact.
- Notes, timers and tickets have smaller add actions. Timers display their titles.
- Tile time sits one dp below its label, with eight dp of space below the footer.
- Dashboard has one app heading, grouped install/storage/network metadata and labelled chart scales.
- Usage sessions keep the union of active activity classes. Overlapping in-app activity handovers no longer prematurely end the app session.
- Short usage appears in seconds, and the dashboard includes seconds in longer totals. Hourly bars and today's total use the same event timeline.
- Storage does not add cache twice to Android's data byte count.
- Horizontal drawer gestures release folding ownership for page navigation. A fresh touch resets stale gesture ownership.
- Icon-pack removal falls back without an uncaught package-not-found exception.
- Cached weather without forecast arrays refreshes on the next allowed foreground attempt; manual-only settings remain respected.

Release compiled with zero errors and zero warnings. APK version and signing
identity were checked. No functional or device tests were run at the owner's
request. Android event retention and OEM usage reporting remain platform limits;
this is not a claim that missing historical events can be reconstructed.

Published APK SHA-256:
ba3fb617f4a4a570502b42e47f750c0dc3f98262210801f8ac74e4142e5cb200

Gallery: thirteen labelled interface illustrations and device/desktop mockups,
plus archived 1.0.1 assets. The owner chose archived screenshots for this release.
