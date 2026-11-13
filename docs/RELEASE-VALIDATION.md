# Launcher 1.0.1, build 2

13 November 2026.

- Release build: zero errors and warnings, .NET 11 / Android SDK 37 / JDK 21.
- Core behavior harness: 56 checks passed. Includes update origin and manifest
  validation, source-generated JSON, progress, download size limits, truncation,
  checksum failure and preservation of the previously verified file.
- Galaxy A26: updated Debug build opens YouTube Music from the now-playing
  title. User confirmed the action works. Playback controls remain separate
  clickable controls. The phone disconnected before the final ADB installation;
  the user subsequently confirmed installing the published update via browser.
- Dedicated Android 15 emulator: deliberately older build 1 with the updater
  checked the live manifest, found 1.0.1, downloaded the release, passed APK
  verification, opened per-source installation permission and Android's Update
  prompt. Approving Update installed build 2. Layout and sample weather survived.
  The installed build then reported that 1.0.1 was up to date.
- Public manifest and APK downloaded over HTTPS; byte count and SHA-256 matched
  the local release. Project, article, project listing and blog listing return 200.
  Both pages passed desktop/mobile capture and mobile overflow checks. All 22
  gallery images return 200 and the APK button renders.

APK: `artifacts/Launcher.apk`, 19,340,810 bytes.

SHA-256: `5ee1a2e0377c5b1ec6adec9a9463fa452921978287c9bca2ccb5dba9c8825d39`

Evidence: `artifacts/screenshots/qa-update-installed.png`,
`artifacts/showcase/published.json`, desktop/mobile live captures in the showcase
folder. Earlier smoke checks are recorded separately; they are not a claim of
a fresh complete UI regression pass for this release.

Publishing initially created root-owned render output that prevented the CMS
service from rebuilding on restart. Ownership was corrected in the application's
render-next/rendered folders, a build was run as kevlog, and the service restarted.
The publisher now corrects generated ownership before restarting. Final public
verification passed after this correction. The pre-import SQLite backup is
recorded in the publishing receipt.
