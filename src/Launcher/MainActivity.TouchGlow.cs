using Android.App;
using Android.Views;
using LauncherApp.UI;

namespace LauncherApp;

public partial class MainActivity
{
    private TouchGlowDrawable? touchGlow;

    private void TrackTouchGlow(MotionEvent touch, View? decor, ref TouchGlowDrawable? glow)
    {
        if (!state.TouchGlow || decor is null)
            return;

        if (glow is null && touch.ActionMasked == MotionEventActions.Down)
        {
            glow = new TouchGlowDrawable(Dp(42));
            decor.Overlay?.Add(glow);
        }

        glow?.Track(touch, palette.Accent);
    }

    private sealed class TouchGlowDialog(MainActivity host) : Dialog(host, Android.Resource.Style.ThemeMaterialNoActionBar)
    {
        private TouchGlowDrawable? glow;

        public override bool DispatchTouchEvent(MotionEvent touch)
        {
            host.TrackTouchGlow(touch, Window?.DecorView, ref glow);
            return base.DispatchTouchEvent(touch);
        }

        protected override void OnStop()
        {
            glow?.Clear();
            base.OnStop();
        }
    }
}
