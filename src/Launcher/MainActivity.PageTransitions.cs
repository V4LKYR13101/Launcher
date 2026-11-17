using Android.Animation;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
namespace LauncherApp;

public partial class MainActivity
{
    private ValueAnimator? homePageAnimation;
    private View? outgoingHomePage,incomingHomePage;
    private readonly Dictionary<string,int> homeScrollPositions=[];
    private ScrollView? renderedHomeScroll;
    private string? renderedHomePage;
    private void RememberHomeScroll()
    {
        if(renderedHomePage is {} name&&renderedHomeScroll?.IsAttachedToWindow==true)
            homeScrollPositions[name]=renderedHomeScroll.ScrollY;
    }
    private void RestoreHomeScroll()
    {
        if(!IsHomePage||searching){renderedHomeScroll=null;renderedHomePage=null;return;}
        renderedHomeScroll=scroll;renderedHomePage=page;var target=scroll;string name=page;
        int position=homeScrollPositions.GetValueOrDefault(name);
        if(position>0)target.Post(()=>{if(ReferenceEquals(scroll,target)&&page==name&&!searching)target.ScrollTo(0,position);});
    }
    private static int HomePageIndex(string value)=>value switch {"Root"=>0,"Tiles"=>1,"Conversations"=>2,_=>-1};
    private void StopHomePageAnimation()
    {
        var animation=homePageAnimation;homePageAnimation=null;animation?.Cancel();
        if(incomingHomePage is {} incoming){incoming.TranslationX=0;incoming.Alpha=1;incoming.ScaleX=incoming.ScaleY=1;incoming.RotationY=0;if(incoming is ViewGroup group&&group.GetChildAt(0) is {} content)content.TranslationX=0;}
        if(outgoingHomePage is {} outgoing)root.RemoveView(outgoing);
        incomingHomePage=null;outgoingHomePage=null;
    }
    private void Navigate(string target)
    {
        RememberHomeScroll();ResetDeviceDepth();StopHomePageAnimation();
        int from=HomePageIndex(page),to=HomePageIndex(target);
        bool animate=LayerMotionEnabled&&state.PageTransitions&&resumed&&!searching&&from>=0&&to>=0&&from!=to&&scroll.Width>0&&scroll.Height>0
            &&Android.Provider.Settings.Global.GetFloat(ContentResolver,Android.Provider.Settings.Global.AnimatorDurationScale,1f)>0;
        var previous=animate?scroll:null;
        var layout=animate?new FrameLayout.LayoutParams(scroll.Width,scroll.Height)
        {LeftMargin=shell.Left+scroll.Left-root.PaddingLeft,TopMargin=shell.Top+scroll.Top-root.PaddingTop}:null;
        if(previous is not null)shell.RemoveView(previous);
        HideKeyboard();searching=false;query="";page=target;Render();
        if(previous is null||layout is null)return;
        float width=layout.Width,direction=to>from?1:-1;
        outgoingHomePage=previous;incomingHomePage=scroll;
        previous.ImportantForAccessibility=ImportantForAccessibility.NoHideDescendants;
        root.AddView(previous,layout);
        var incoming=scroll;incoming.TranslationX=direction*width;incoming.Alpha=.88f;
        incoming.SetCameraDistance(Dp(2400));previous.SetCameraDistance(Dp(2400));
        incoming.PivotX=direction>0?0:width;previous.PivotX=direction>0?width:0;
        incoming.PivotY=previous.PivotY=layout.Height/2f;
        incoming.ScaleX=incoming.ScaleY=.975f;incoming.RotationY=direction*8;
        var nextContent=incoming.GetChildAt(0);var oldContent=previous.GetChildAt(0);
        if(nextContent is not null)nextContent.TranslationX=-direction*Dp(12);
        var animation=ValueAnimator.OfFloat(0,1)!;homePageAnimation=animation;
        animation.SetDuration(260);animation.SetInterpolator(new DecelerateInterpolator(1.6f));
        animation.Update+=(_,e)=>
        {
            if(!ReferenceEquals(homePageAnimation,animation))return;
            float fraction=((Java.Lang.Float)e.Animation!.AnimatedValue!).FloatValue();
            incoming.TranslationX=direction*width*(1-fraction);incoming.Alpha=.88f+.12f*fraction;
            previous.TranslationX=-direction*width*fraction;previous.Alpha=1-.12f*fraction;
            incoming.ScaleX=incoming.ScaleY=.975f+.025f*fraction;previous.ScaleX=previous.ScaleY=1-.025f*fraction;
            incoming.RotationY=direction*8*(1-fraction);previous.RotationY=-direction*8*fraction;
            if(nextContent is not null)nextContent.TranslationX=-direction*Dp(12)*(1-fraction);
            if(oldContent is not null)oldContent.TranslationX=direction*Dp(12)*fraction;
        };
        animation.AnimationEnd+=(_,_)=>
        {
            if(!ReferenceEquals(homePageAnimation,animation))return;
            homePageAnimation=null;incoming.TranslationX=0;incoming.Alpha=1;incoming.ScaleX=incoming.ScaleY=1;incoming.RotationY=0;
            if(nextContent is not null)nextContent.TranslationX=0;
            root.RemoveView(previous);outgoingHomePage=null;incomingHomePage=null;
        };
        incoming.Post(()=>{if(ReferenceEquals(homePageAnimation,animation))animation.Start();});
    }
}
