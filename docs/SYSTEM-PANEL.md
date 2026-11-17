# Launcher system panel

The optional extension adds an accessibility-overlay status strip, a narrow left/right activation handle and a full-height notification/quick panel. It is disabled by default, including for existing installations. It uses a separate accessibility service from app grayscale.

## Setup

1. In Launcher Settings, open System panel and enable its master switch.
2. Open Accessibility access and enable Launcher system panel. Android may require Allow restricted settings in app information for browser-installed APKs.
3. Enable Launcher notification access for current notifications, replies and media.
4. Optionally allow brightness access and connection-name permissions.

No root, Good Lock, secure-settings grant or Shizuku is required for this implementation. Wi-Fi, Bluetooth and other protected controls open their native settings rather than claiming to toggle a restricted radio.

## Interaction and controls

Tap the visible 68 dp edge handle or swipe down within it. Its distance below the native status bar is adjustable so Android’s own top-edge gesture does not compete with it. Only that small region accepts activation touches. The status strip itself is not touchable. The panel includes Android and Close controls at the top, a dedicated upward-drag close handle, a scrolling content area and a Disable action at the bottom. Android opens the original notification shade.

The strip is separately opt-in. Its below-bar position can cover app headers because overlays do not make other apps reserve space. The full panel starts below Android's native status bar so native indicators remain visible. Top placement leaves the native right-hand privacy-indicator area uncovered. This overlay does not change Samsung SystemUI resources, fonts or icons. Device-specific cutouts and reserved indicators still need physical verification.

Notifications are grouped by source app with monochrome real app icons. Tap the body to open the notification destination, expand long text, reply when the source provides an inline reply, and dismiss clearable notifications by button or horizontal swipe. Persistent notifications remain until their source clears them. Replies are sent through the original app action.

Media metadata, artwork and transport controls come from Android's active media session. The panel offers brightness access, media volume, supported connection details and links to native controls. Appearance follows Launcher with optional light/dark override, opacity, animation and control-order preferences.

## Lifecycle

The master switch removes both overlay windows and unregisters notification, connection, media and broadcast listeners. The accessibility binding remains authorised until explicitly disconnected, but disabled callbacks perform no foreground processing. Turning the screen off removes overlays and stops radio/media monitoring. Locked devices and recognised SystemUI, permission-controller and installer windows hide the overlay. The service does not retrieve screen content or capture global gestures.

The larger panel and media-session monitoring run only while open. Notifications initially show a batch of 20, with Show more for further items. Adaptive brightness stays under Android control; its native settings remain available. The status clock updates on Android minute/time broadcasts. Media position updates once per second only while the panel is visible. There is no blur, camera use, sensor loop or extra process. No system settings are changed simply by enabling or disabling the extension. Brightness changes occur only after a user adjusts its slider with the required access.

## Verification

Core preference checks and native emulator checks are recorded with the release. Samsung A26 / One UI 9 overlay behaviour, real cutout placement and battery/memory impact require physical-device measurements. This implementation does not assert that every protected OEM screen can be identified from window-change events.
