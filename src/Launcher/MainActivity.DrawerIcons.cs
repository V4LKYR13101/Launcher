using Android.App;
using Android.Views;
using Android.Widget;
using Launcher.Core;
using LauncherApp.UI;

namespace LauncherApp;

public partial class MainActivity
{
    private void PickDrawerIcon(Drawer drawer)
    {
        string selected=DrawerIcons.All.Contains(drawer.Icon)?drawer.Icon:DrawerIcons.ForDrawer(drawer.Id);
        Sheet(drawer.Name,(panel,dialog)=>
        {
            Paragraph("Choose a symbol for this drawer.",panel);
            var grid=Stack();panel.AddView(grid,Match());
            void Rebuild()
            {
                grid.RemoveAllViews();
                for(int row=0;row<5;row++)
                {
                    var line=Stack(true);grid.AddView(line,Match());
                    foreach(string icon in DrawerIcons.All.Skip(row*4).Take(4))
                    {
                        bool active=icon==selected;
                        var cell=new FrameLayout(this){ContentDescription="Drawer symbol "+icon};
                        cell.Background=Background(palette.Panel,12,active?palette.Accent:null);
                        cell.AddView(new GlyphView(this,icon,active?palette.Accent:palette.Text),new FrameLayout.LayoutParams(Dp(25),Dp(25),GravityFlags.Center));
                        cell.Click+=(_,_)=>{selected=icon;Rebuild();};
                        var lp=new LinearLayout.LayoutParams(0,Dp(66),1);lp.SetMargins(Dp(4),Dp(4),Dp(4),Dp(4));line.AddView(cell,lp);
                    }
                }
            }
            Rebuild();
        },(footer,dialog)=>footer.AddView(Button("Save changes",()=>{drawer.Icon=selected;Save();dialog.Dismiss();Render();},true),Match()));
    }
}
