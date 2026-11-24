# Launcher Roll

Launcher Roll is a Watch Face Format 2 face. The rolling hour and minute rows respect the watch's time format. Weather comes from Wear OS. Launcher for Wear OS supplies the divider color through the paired phone's Launcher accent.

## Package and release

Build the phone and Wear OS projects from the solution. The two app APKs use the same application ID and must use the same signing identity for the Wear OS Data Layer. Distribute the Wear OS companion through a Wear OS install channel before offering phone-driven face installs.

Increment the watch face version code and name in its manifest. From the Launcher repository root, run `.\tools\Build-LauncherWatchFace.ps1`. The signing workstation uses its installed SDK and JDK, Google's Watch Face Push validator, and the existing dedicated watch face key. The script accepts explicit tool and keystore paths for other workstations. Supply the password for a custom keystore through `LAUNCHER_WATCHFACE_STOREPASS`.

The script puts the signed APK and `watch-face-feed-entry.json` in `artifacts/watch-face`. Run `python tools/publish_launcher_wear.py --face-only artifacts/watch-face` from the repository root to publish both to Kevlog. You can also pass the signed APK directly when the feed JSON is beside it. The publisher updates the live feed and verifies the public download.

On Wear OS 6 and later, open Launcher for Wear OS on the watch. On the phone, open **Settings → Wear OS → Install Launcher Roll**. After approval, Launcher verifies the APK and sends it with the validation token. Select Launcher Roll in the watch face picker.

Launcher checks for updates to an installed face on resume. To check immediately, select **Check for watch face updates** on the Wear OS settings page.

The weather row follows the watch's built-in weather feed. If the feed is unavailable, open the face editor and select your watch's weather provider for the **Weather** complication.

To see which archived Launcher APKs can be removed from Kevlog, run `python tools/prune_launcher_apks.py`. Add `--apply` to remove those public downloads and retain a private server backup. APKs referenced by the update feed, including the legacy Android migration build, are protected.
