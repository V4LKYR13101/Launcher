using Android.App;
using Android.Views;
using Android.Widget;
using LauncherApp.Services;

namespace LauncherApp;

public partial class MainActivity
{
    private void PickConversationSources()
    {
        var options=apps.Select(a=>(Package:a.PackageName,Name:a.Label))
            .Concat(ConversationService.NotificationSnapshot().Select(n=>(Package:n.Package,Name:apps.FirstOrDefault(a=>a.PackageName==n.Package)?.Label??n.Package)))
            .DistinctBy(p=>p.Package).OrderBy(p=>p.Name).ToArray();
        var selected=options.Where(p=>!state.ConversationExcludedApps.Contains(p.Package)).Select(p=>p.Package).ToHashSet();
        bool messagesOnly=state.ConversationMessagesOnly;
        Sheet("Notification sources",(panel,dialog)=>
        {
            Paragraph("Choose which apps appear in Conversations. This doesn’t change Android’s notification settings.",panel);
            ToggleRow(panel,"Messages only",messagesOnly,v=>messagesOnly=v);
            var controls=Stack(true);LinearLayout list=Stack();string query="";
            void Rebuild()
            {
                list.RemoveAllViews();
                foreach(var option in options.Where(o=>o.Name.Contains(query,StringComparison.CurrentCultureIgnoreCase)||o.Package.Contains(query,StringComparison.OrdinalIgnoreCase)))
                {
                    var check=new CheckBox(this){Text=option.Name,Checked=selected.Contains(option.Package),TextSize=15,Typeface=regular,ButtonTintList=AccentTint()};
                    check.SetTextColor(palette.Text);check.SetMinimumHeight(Dp(50));
                    check.CheckedChange+=(_,e)=>{if(e.IsChecked)selected.Add(option.Package);else selected.Remove(option.Package);};list.AddView(check,Match());
                }
            }
            controls.AddView(Button("Select all",()=>{selected=options.Select(o=>o.Package).ToHashSet();Rebuild();}),new LinearLayout.LayoutParams(0,-2,1));
            controls.AddView(Button("Clear",()=>{selected.Clear();Rebuild();}),new LinearLayout.LayoutParams(0,-2,1));panel.AddView(controls,Match());Gap(panel);
            var search=new EditText(this){Hint="Filter apps",TextSize=15,Typeface=regular,Background=Background(palette.Panel,14)};
            search.SetSingleLine(true);search.SetTextColor(palette.Text);search.SetHintTextColor(palette.Muted);search.SetPadding(Dp(14),Dp(12),Dp(14),Dp(12));
            search.TextChanged+=(_,_)=>{query=search.Text??"";Rebuild();};panel.AddView(search,Match());Gap(panel,8);panel.AddView(list,Match());Rebuild();
        },(footer,dialog)=>footer.AddView(Button("Apply filters",()=>
        {
            state.ConversationExcludedApps=options.Where(o=>!selected.Contains(o.Package)).Select(o=>o.Package).ToList();
            state.ConversationMessagesOnly=messagesOnly;conversationFilter="All";Save();dialog.Dismiss();Render();
        },true),Match()));
    }
}
