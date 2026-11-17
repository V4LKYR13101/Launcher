# Deploy from Visual Studio

1. Open `E:\Repository\launcher\Launcher.sln` in Visual Studio 2026.
2. Set **Launcher** under `src` as the startup project.
3. Select **Debug** and your connected Samsung phone in the device dropdown.
4. Press **F5** to deploy and debug, **Ctrl+F5** to deploy without debugging, or
   right-click Launcher and choose **Deploy** to install it.

The Android project is marked for deployment in both solution configurations.
The core library and console check project are build-only. The device fixture
used for earlier emulator work is not included in this solution.

Install the Android tooling in Visual Studio Installer if the phone dropdown is
missing. Microsoft bundles it with the .NET multi-platform app UI workload. The
project itself uses native .NET for Android views and has no MAUI dependency.

This workspace requires .NET SDK 11.0.100, Android SDK platform 37 and JDK 21.
The installed JDK is:

`C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot`

Choose that JDK under Visual Studio's Android settings if its default JDK is
older. USB debugging must be enabled and the phone must authorise this computer.
Deploying over the installed app preserves its data when signing keys match.

## Grayscale setup

Hold an app tile and choose **Open app in grayscale**. Icon colour is a separate
setting. For display grayscale:

1. Open Launcher Settings → **App grayscale**.
2. Enable **Launcher app grayscale** in Android accessibility settings. It only
   receives foreground-window change events, without reading screen content.
3. Grant display-setting access once from a connected computer:

```powershell
adb -s R5GYB2QB0NR shell pm grant com.launcher.home android.permission.WRITE_SECURE_SETTINGS
```

Both setup statuses appear in Launcher. If Android blocks the sideloaded
accessibility service, its App info menu may require **Allow restricted
settings** before you can enable the service yourself.

The implementation uses Android's display colour-correction setting while the
chosen app is in front. Other apps in split screen share that display state.
Leaving the app restores the previous setting. The service also attempts to
restore on shutdown and on reconnection after a process restart. The setup page
has a **Turn off service and restore colours** action. Samsung firmware behaviour
requires your acceptance testing; no device test was run for this release.

Biometric locking uses Android's biometric/screen-lock prompt. It guards app
launches and app editing through Launcher. It does not replace an app's own
authentication or block Android's other entry points.
