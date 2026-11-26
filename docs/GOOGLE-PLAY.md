# Launcher on Google Play

The default phone build produces an Android App Bundle for `com.kevlog.launcher`. The separately distributed Kevlog APK has package `com.launcher.home`; build it with `-p:LauncherDistribution=Kevlog` or use the existing Kevlog release script. A package rename is a new install, not an upgrade. Test exporting a layout from Kevlog and restoring it in the Play app before advertising that migration.

## Internal testing

1. Build the Release bundle, signed with the Play upload key. Keep signing material and passwords outside the repository.
2. Verify the bundle identity and manifest. The Play edition does not request package installs, all-files access or ADB-granted secure settings. The two optional AccessibilityService features and Launcher Roll setup/sync are not part of this phone test. Existing Kevlog builds retain them.
3. Upload the signed bundle to **Internal testing** for this app in Play Console. Use the tester opt-in link to test the Play-installed build. A new personal account also needs a separate eligible closed test before applying for production.
4. Check home role, app drawers, backup/restore, English/Spanish, the Android file picker, direct downloads, notification onboarding, and native Google Discover plus fallback. Do not test the last item on a Google-free emulator alone.

## Listing draft

- Short description: A clean Android home screen with app drawers, search, notes and timers.
- Initial release notes: First Google Play test of Launcher. Try a custom home layout, app drawers, quick search, Today cards, notes and timers. English and Spanish are included.
- Use real app screenshots and the Launcher icon. Review the complete data flows before publishing the privacy policy and submitting Data Safety; the in-app copy is a starting point, not a completed declaration.
