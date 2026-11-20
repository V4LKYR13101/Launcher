using Android.OS;
using Android.Views;
namespace LauncherApp.UI;

public sealed class DrawerPullListener(float threshold, Func<bool> expanded, Action begin, Action<float> move, Action<float, bool> end, Action<bool> active) : Java.Lang.Object, View.IOnTouchListener
{
    private float x, y; private bool dragging, held; private long started;
    private readonly Handler handler = new(Looper.MainLooper!); private Action? hold;
    public bool OnTouch(View? view, MotionEvent? ev)
    {
        if (view is null || ev is null)
            return false;
        if (ev.ActionMasked == MotionEventActions.Down)
        {
            x = ev.RawX;
            y = ev.RawY;
            dragging = false;
            held = false;
            started = ev.EventTime;
            active(true);
            view.Parent?.RequestDisallowInterceptTouchEvent(true);
            hold = () => { held = true; view.PerformLongClick(); };
            handler.PostDelayed(hold, ViewConfiguration.LongPressTimeout);
            return true;
        }
        float dy = ev.RawY - y, dx = ev.RawX - x;
        if (ev.ActionMasked == MotionEventActions.Move)
        {
            if (Math.Abs(dy) > threshold || Math.Abs(dx) > threshold)
            {
                if (hold is not null)
                    handler.RemoveCallbacks(hold);
            }
            if (!dragging && !held && Math.Abs(dx) > threshold && Math.Abs(dx) > Math.Abs(dy) * 1.3f)
            {
                held = true;
                active(false);
                view.Parent?.RequestDisallowInterceptTouchEvent(false);
                return true;
            }
            if (!dragging && !held && Math.Abs(dy) > threshold && Math.Abs(dy) > Math.Abs(dx) * 1.3f)
            {
                if (expanded() ? dy < 0 : dy > 0)
                {
                    dragging = true;
                    begin();
                }
                else
                {
                    active(false);
                    view.Parent?.RequestDisallowInterceptTouchEvent(false);
                    held = true;
                }
            }
            if (dragging)
                move(dy);
            return true;
        }
        if (ev.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel)
        {
            if (hold is not null)
                handler.RemoveCallbacks(hold);
            if (dragging)
                end(dy, ev.ActionMasked == MotionEventActions.Cancel);
            else if (!held && ev.ActionMasked == MotionEventActions.Up && Math.Abs(dx) < threshold && Math.Abs(dy) < threshold && ev.EventTime - started < ViewConfiguration.LongPressTimeout)
                view.PerformClick();
            active(false);
            view.Parent?.RequestDisallowInterceptTouchEvent(false);
            return true;
        }
        return true;
    }
}
