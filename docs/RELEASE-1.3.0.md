# Launcher 1.3.0, build 10

14 November 2026. Android release; Windows companion remains 1.2.0.

- Bundled Google Material (Outlined, Rounded, Filled, Sharp), Phosphor (Light, Thin, Regular, Bold, Fill) and Bootstrap icon libraries.
- Offline font-based vectors, name search and selections for app tiles and drawer symbols. Original app icons and compatible installed packs remain available.
- Global library matching uses recognised brands or exact utility names. Unmatched apps retain native icons. Per-app original-icon and global-selection choices are separate.
- Broader installed pack intent discovery and drawable-list browsing.
- Optional ambient-light-sensor theme switching in Settings and Appearance. Defaults off, Light at 1000 lux, Dark at 300 lux and five seconds of stable readings. Delay and thresholds are editable.
- Separate thresholds, a switch cooldown, and deferral while typing or using sheets prevent disruptive changes. Scroll position is retained. Manual theme selection disables the automatic mode.
- Monitoring runs only while Launcher is visible. No camera access or camera permission. Phones without a light sensor retain manual theme selection.
- Removed Refresh and City & units actions from the Root weather card. They remain in Weather settings.

The icon sources and pinned commit hashes are recorded in ICON-LIBRARIES.json.
Original licences are included in the APK under assets/licenses. Fonts are bundled,
with no CDN or network requirement at runtime.

No functional or device tests were run at the owner's request. Release compilation,
package/signing checks, bundled asset checks and public publication verification
are recorded separately. Device acceptance and real sensor thresholds remain with
the owner.

Release compilation passed with zero errors and zero warnings. Ten bundled styles and their catalogues/licences were verified in the APK. Package/version/signing identity, no camera permission, public update manifest, download size, article/release copy and removal of the previous live APK were verified.

APK SHA-256: 2276f667ef38dffe28f2ea2ccf07d5de023fa5602a7ec24e7e78439a3ad326fa
