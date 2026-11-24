# Launcher Roll

Launcher Roll is a Watch Face Format 2 face. The rolling hour and minute rows respect the watch's time format. Weather comes from Wear OS. Launcher for Wear OS supplies the divider color through the paired phone's Launcher accent.

## Package and release

Build the phone and Wear OS projects from the solution. The two app APKs use the same application ID and must use the same signing identity for the Wear OS Data Layer. Distribute the Wear OS companion through a Wear OS install channel before offering phone-driven face installs.

Increment the watch face version code and name in its manifest. Run `tools/Build-LauncherWatchFace.ps1` with the Android SDK, JDK, Google's Watch Face Push CLI validator, and the dedicated watch-face signing key. Supply its password through `LAUNCHER_WATCHFACE_STOREPASS`. Use the same dedicated face key for subsequent releases.

The script produces the signed APK and `watch-face-feed-entry.json`. Publish the APK through the Kevlog Launcher project download route specified in the entry. Merge the generated `watchFace` object into the site's existing `updates/launcher.json`. Verify the public route returns the exact signed bytes before exposing the feed entry.

On Wear OS 6 and later, open Launcher for Wear OS on the watch. On the phone, open **Settings → Wear OS → Install Launcher Roll**. After approval, Launcher verifies the APK and sends it with the validation token. Select Launcher Roll in the watch face picker.

Launcher checks for updates to an installed face on resume. To check immediately, select **Check for watch face updates** on the Wear OS settings page.

The weather row follows the watch's built-in weather feed. If the feed is unavailable, open the face editor and select your watch's weather provider for the **Weather** complication.
