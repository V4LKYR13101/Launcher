Launcher started with a fairly simple brief: make the home screen calmer, give apps a sensible place to live, and leave enough room to make it your own.

I built it in C# with native Android views and .NET for Android. There is no account to create and no feed waiting behind the home screen. It opens to your apps, grouped into drawers you can name and arrange yourself.

![Launcher on Android](/media/launcher-cover-v1.webp)

## Apps with a place to go

A drawer can be as broad as Essentials or as specific as the handful of tools you use for work. Tap its heading to show the apps inside. Hold it to rename it, change its symbol, choose its apps or move it in the list.

The drawer symbols are deliberately simple. The apps keep their own identities. Launcher uses an app's monochrome icon where Android provides one and falls back to its real icon in grayscale. Original colour icons are an option too.

You can change the column count, tile height and corners, hide labels, centre headings or pin a few apps. Dark, Light and Sun themes give you a starting point. The accent can be one of the supplied colours or a hex value of your own.

![Drawer symbols and selection](/media/launcher-icons-v1.webp)

## The useful things stay close

The weather strip sits above the app drawers. Set the city in Settings and choose Celsius or Fahrenheit. The temperature and conditions use your accent colour.

Weather comes from Open-Meteo. It is separate from the weather service supplied with a phone. Refreshing happens while Launcher is open, with a choice of intervals or manual updates. You can also turn the strip off and add your existing Android weather widget on the Root page.

When an app exposes an active Android media session, a compact player appears below the weather. Artwork sits behind the title, progress and playback controls. Tap the card to open the playing app. The transport buttons control playback without opening it. Available controls depend on what that app supports.

![Weather and the compact player](/media/launcher-home-v1.webp)

## A separate space for cards

Swipe across to Root for the date, notes, a focus timer and a calculator. Each card can collapse. You can hide the cards you do not want, including Focus, and add Android widgets from installed apps.

Focus mode keeps Essentials and pinned apps visible. It helps narrow the home screen, but it does not block another app or silence the phone. Notes and layout settings stay on the device. A JSON backup lets you save and restore the layout; widget bindings stay tied to their own device.

![The Root page](/media/launcher-root-v1.webp)

## Notifications you can choose

Conversations brings notifications together with a small icon identifying the source app. Choose which apps appear and whether to show all notification types or messages only. Count badges use the selected accent and reflect current dismissible notifications, rather than claiming to know an app's internal unread count.

Pull down on the home screen to open Launcher's quick panel. It combines connection shortcuts, brightness and a scrollable notification list. Swipe a dismissible notification sideways to clear it, or swipe up from the panel header or bottom handle to close the panel.

This panel belongs to Launcher. Android's system notification shade still belongs to the phone. A regular launcher cannot replace Samsung's system interface across other apps.

![Notification sources and filters](/media/launcher-conversations-v1.webp)

## The details took the work

Much of the testing happened on a Galaxy A26. The small interactions mattered: a sheet needed to follow a finger down, a notification list needed to scroll without closing its panel, and the music card needed to open the right app without its buttons doing the same thing.

The bottom search bar leaves more space for the drawers and keeps app search within reach. Time and battery placement are adjustable for screens with camera cutouts. The battery icon follows the reported level and uses the accent colour, with an animation while charging.

## Install Launcher

[Download the Android APK from the project page](/projects/launcher/). It requires Android 7 or later. Open the downloaded file and approve installation. Android may first ask you to allow installs from your browser or file manager.

To use it as your home screen, choose Launcher under Android's default Home app setting. You can change that choice later in the same place.

Notification access enables Conversations, badges and media-session access. Usage access is optional for time totals. Connection permissions let the quick panel show the network or device name. Launcher does not need those optional permissions just to open apps.

Settings includes App updates. It checks Kevlog for a newer release, downloads the APK and verifies it before opening Android's installer. Updating keeps the existing layout and settings.

The gallery pairs actual app screenshots with device mockups. Weather, notes and notifications shown there are sample content. The studio cover is a generated product scene using an app capture.
