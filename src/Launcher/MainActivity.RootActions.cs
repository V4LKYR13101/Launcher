using Android.Graphics;
using Android.Views;
using Android.Widget;
namespace LauncherApp;
public partial class MainActivity
{
    private TextView RootAction(string title,Action action,bool prominent=false)
    {
        var view=Text(title,13,true,prominent?palette.Action:palette.Text);view.Gravity=GravityFlags.Center;view.SetMinimumHeight(Dp(48));view.SetMaxLines(2);view.Ellipsize=Android.Text.TextUtils.TruncateAt.End;view.SetPadding(Dp(10),Dp(6),Dp(10),Dp(6));
        if(prominent)view.Background=Background(new Color(palette.Accent.R,palette.Accent.G,palette.Accent.B,(byte)18),10);
        TouchFeedback(view,10);view.Click+=(_,_)=>action();return view;
    }
    private void RootActions(LinearLayout panel,params (string Title,Action Action)[] actions)
    {
        bool stacked=FontScale>=1.4f;
        var row=Stack(!stacked);row.BaselineAligned=false;row.SetGravity(GravityFlags.FillVertical);
        foreach(var item in actions)
        {
            var view=RootAction(item.Title,item.Action);
            var lp=stacked?new LinearLayout.LayoutParams(-1,-2):actions.Length>=2?new LinearLayout.LayoutParams(0,-1,1):new LinearLayout.LayoutParams(-2,-2);lp.SetMargins(0,Dp(4),Dp(4),0);row.AddView(view,lp);
        }
        panel.AddView(row,Match());
    }
}
