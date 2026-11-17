# UI review and native emulator checks

Reviewed Apps/drawers, Root/cards, Conversations, notification sources, quick panel, weather, playback, feed settings, resources, shared sheets, forms, app actions/dashboard, icon selection, Settings/Appearance, journeys/replies and companion screens.

The previous release was published first. This follow-up uses actual emulator captures rather than relying solely on illustrations. Images are stored in artifacts/ui-polish-1.3.11 and mirrored under featureImages/releases/1.3.11.

## Findings corrected
Transparent icon margins made themed glyphs much smaller than raster icons. The first pass also found a clipped media seek handle, duplicate settings titles, inconsistent card spacing, non-anchored journey/reply actions, and refreshes that could replace content during scrolling. The source changes address these issues without changing gesture recognition thresholds.

## Verification
The UI scripts explicitly target emulator-5554. They assert visible native controls and use Android media-session state to check playback. Continuous playback prevents UIAutomator reaching idle, so transport checks reuse observed button bounds and pause playback before inspecting the hierarchy. A sheet test also needed to wait for navigation, and a feed test needed to account for deliberate scroll restoration. These harness corrections are recorded instead of misreporting them as app defects.

A final pass checked 360 x 640 dp layouts, 150% text on that smaller display, a 640 dp sheet width on a larger display, the journey save action with the keyboard open, and the reply footer. The emulator display and font overrides were reset afterwards.

See the native screenshots and checks.json, media-checks.json and feed-checks.json for evidence. Feed failure tests use an isolated sample cache and an invalid test host; no production source or user phone was modified.

## Limits
An emulator does not establish behaviour on every OEM, sensor, biometric implementation, widget provider or Android version. Those integrations remain device-dependent. No claim of universal pixel-perfect rendering is made.
