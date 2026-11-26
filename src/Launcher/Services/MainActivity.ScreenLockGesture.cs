
using Android.OS;
using Android.Views;
using Android.Widget;
using LauncherApp.Services;

namespace LauncherApp;

public partial class MainActivity
{
    private const long DoubleTapWindowMs = 350;

    private long lastEmptyTapAt;
    private float lastEmptyTapX;
    private float lastEmptyTapY;

    private float currentEmptyTapX;
    private float currentEmptyTapY;

    private void AttachScreenLockGesture(LinearLayout surface)
    {
        // Observe the coordinates without consuming touch events.
        // Existing scrolling, swiping and long presses remain intact.
        surface.Touch += (_, args) =>
        {
            if (args.Event?.ActionMasked == MotionEventActions.Up)
            {
                currentEmptyTapX = args.Event.RawX;
                currentEmptyTapY = args.Event.RawY;
            }

            args.Handled = false;
        };

        // Only taps handled by the background reach this listener.
        surface.Click += (_, _) => HandleBackgroundTap();
    }

    private void HandleBackgroundTap()
    {
        if (!state.DoubleTapToLock ||
            searching ||
            openSheetCount > 0 ||
            appLayer is not null ||
            page is not ("Root" or "Tiles"))
        {
            lastEmptyTapAt = 0;
            return;
        }

        // Exclude visible content regions, including non-clickable
        // labels, cards and widget surfaces.
        for (int i = 0; i < body.ChildCount; i++)
        {
            var child = body.GetChildAt(i);

            if (child is null ||
                child.Visibility != ViewStates.Visible)
                continue;

            using var bounds = new Android.Graphics.Rect();

            if (child.GetGlobalVisibleRect(bounds) &&
                bounds.Contains(
                    (int)currentEmptyTapX,
                    (int)currentEmptyTapY))
            {
                lastEmptyTapAt = 0;
                return;
            }
        }

        long now = SystemClock.UptimeMillis();

        bool withinTime =
            lastEmptyTapAt > 0 &&
            now - lastEmptyTapAt <= DoubleTapWindowMs;

        bool withinDistance =
            Math.Abs(currentEmptyTapX - lastEmptyTapX) <= Dp(48) &&
            Math.Abs(currentEmptyTapY - lastEmptyTapY) <= Dp(48);

        if (withinTime && withinDistance)
        {
            lastEmptyTapAt = 0;
            LockScreenFromGesture();
            return;
        }

        lastEmptyTapAt = now;
        lastEmptyTapX = currentEmptyTapX;
        lastEmptyTapY = currentEmptyTapY;
    }

    private void LockScreenFromGesture()
    {
        if (!SystemPanelService.IsEnabled(this))
        {
            Toast(
                Localize(Resource.String.ui_enable_launcher_system_panel_in_android) +
                Localize(Resource.String.ui_accessibility_settings_to_lock_the_screen));
            return;
        }

        Haptic(root, HapticCue.Commit);

        if (!SystemPanelService.TryLockScreen())
        {
            Toast(
                Localize(Resource.String.ui_android_could_not_lock_the_screen) +
                Localize(Resource.String.ui_check_that_launcher_accessibility_is_connected));
        }
    }
}
