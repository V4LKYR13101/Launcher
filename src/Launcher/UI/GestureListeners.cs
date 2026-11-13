using Android.App;
using Android.Views;
using Android.Views.Animations;

namespace LauncherApp.UI;

public sealed class SheetDragListener(View sheet,Dialog dialog,float threshold) : Java.Lang.Object,View.IOnTouchListener
{
    private float startY;
    private long started;
    public bool OnTouch(View? view,MotionEvent? e)
    {
        if(e is null)return false;
        switch(e.ActionMasked)
        {
            case MotionEventActions.Down:
                startY=e.RawY;started=System.Environment.TickCount64;sheet.Animate()?.Cancel();
                view?.Parent?.RequestDisallowInterceptTouchEvent(true);return true;
            case MotionEventActions.Move:
                sheet.TranslationY=Math.Max(0,e.RawY-startY);return true;
            case MotionEventActions.Up:
                float distance=Math.Max(0,e.RawY-startY);
                float velocity=distance/Math.Max(.001f,(System.Environment.TickCount64-started)/1000f);
                if(distance>threshold || distance>threshold*.3f && velocity>threshold*5)
                {
                    sheet.Animate()!.TranslationY(sheet.Height).SetDuration(180).WithEndAction(new Java.Lang.Runnable(dialog.Dismiss)).Start();
                }
                else sheet.Animate()!.TranslationY(0).SetDuration(180).SetInterpolator(new DecelerateInterpolator()).Start();
                return true;
            case MotionEventActions.Cancel:
                sheet.Animate()!.TranslationY(0).SetDuration(180).Start();return true;
        }
        return true;
    }
}

public sealed class NotificationSwipeListener(float threshold,bool dismissible,Action dismiss,Action persistent) : Java.Lang.Object,View.IOnTouchListener
{
    private float startX,startY;
    private bool swiping;
    public bool OnTouch(View? view,MotionEvent? e)
    {
        if(view is null||e is null)return false;
        switch(e.ActionMasked)
        {
            case MotionEventActions.Down:startX=e.RawX;startY=e.RawY;swiping=false;return false;
            case MotionEventActions.Move:
                float dx=e.RawX-startX,dy=e.RawY-startY;
                if(Math.Abs(dx)>threshold*.15f && Math.Abs(dx)>Math.Abs(dy)*1.5f)
                {
                    swiping=true;view.CancelLongPress();view.Pressed=false;view.Parent?.RequestDisallowInterceptTouchEvent(true);
                    view.TranslationX=dx;view.Alpha=Math.Max(.3f,1-Math.Abs(dx)/view.Width);return true;
                }
                return swiping;
            case MotionEventActions.Up:
                if(!swiping)return false;
                if(Math.Abs(e.RawX-startX)>threshold && dismissible)
                    view.Animate()!.TranslationX((e.RawX>startX?1:-1)*view.Width).Alpha(0).SetDuration(150)
                        .WithEndAction(new Java.Lang.Runnable(dismiss)).Start();
                else
                {
                    view.Animate()!.TranslationX(0).Alpha(1).SetDuration(160).Start();
                    if(!dismissible)persistent();
                }
                return true;
            case MotionEventActions.Cancel:view.Animate()!.TranslationX(0).Alpha(1).SetDuration(160).Start();return swiping;
        }
        return false;
    }
}
