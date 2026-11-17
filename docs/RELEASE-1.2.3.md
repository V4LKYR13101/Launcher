# Launcher 1.2.3, build 8

## Interface refinement

Shared secondary buttons use bounded, lightly outlined controls. Primary actions
retain the selected accent. Sheets use consistent side insets, smaller headings,
separated choice rows and side-by-side confirmation actions. Dark surfaces are quieter, and wallpaper dimming can be adjusted for readable light and dark themes. Settings and app
controls use aligned labels, short descriptions and section captions. Touch
feedback is provided on controls, cards and tiles.

Add card is a two-column picker with icons, names and descriptions. It is also
used in command search. The app dashboard groups its metrics, has Today/Last 7
days tabs and a compact QR preview. Tile appearance choices show live previews.
Conversations and the quick panel share notification card typography and spacing.

Drawer headings are smaller. Icon rendering rejects blank/solid monochrome masks
and falls back to usable native artwork or app initials. Tile footer contrast is
based on its surface. Locked tiles use Google's Apache-2.0 Material fingerprint
artwork, converted to a native path and scaled to fill each tile. Its accent is
quieter and its background matches the other tile surfaces.

## Notification opening

A notification tap first opens its source app, preserving Launcher biometric
checks. Notifications without a launchable source activity use their native
intent sender as a fallback. Missing or expired actions no longer fail silently.
Reply and History remain separate actions. This does not promise opening a
specific chat when the source app has no available notification deep link.

## News and feeds

RSS feeds are visible under Settings, News & RSS feeds; Online cards; and Add card.
The presets are Google News South Africa, MyBroadband, BBC World, The Verge and
Ars Technica. Google News South Africa starts enabled. Each source can be enabled,
disabled, edited or removed, and custom HTTPS RSS/Atom feeds can be added.

All sources share one News & feeds Root card. Items sort by publication time,
newest first, with undated items last. Refresh and Sources actions remain compact.
Feeds load concurrently with a limit of three; disabled sources are not fetched.
Successful articles remain available in memory if a later refresh fails. The
reader follows bounded HTTPS redirects and supports published feed links on HTML
pages. It shows specific server and format errors. No provider API key is needed.
Existing separate RSS cards migrate to the source list.

## Drawer gestures

Settings > Drawers > Drag headings to fold enables/disables vertical header
folding. It defaults off, including for older backups that lack the setting.
Tapping to open/close and horizontal page navigation remain available.

## Validation

No functional or device tests are run, at the owner's request. Release compilation,
package/signing metadata and public publication checks are recorded separately.
The five preset feed endpoints returned valid RSS/Atom documents when inspected
outside the app. This is not a claim of device acceptance or exact visual parity.

The Windows companion remains 1.2.0. Native Android package and signing identity
are unchanged. Third-party artwork licences are included in Assets/licenses.
