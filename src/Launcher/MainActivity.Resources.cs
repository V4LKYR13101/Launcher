using Android.Views;
using Android.Widget;
using LauncherApp.Services;
using LauncherApp.UI;
namespace LauncherApp;

public partial class MainActivity
{
    private LinearLayout? resourcesPanel;
    private ResourceDonutView? memoryDonut,storageDonut,powerDonut;
    private TextView? memoryAmount,storageAmount,powerStatus,resourcesFootnote;
    private ResourceSnapshot? resourceSnapshot;
    private void AddResourcesCard()
    {
        state.ResourcesVisible=true;state.RootHidden.Remove("resources");state.CollapsedCards["resources"]=false;
        if(!state.RootOrder.Contains("resources"))state.RootOrder.Add("resources");Save();Navigate("Root");
    }
    private void BuildResources(LinearLayout panel)
    {
        resourcesPanel=panel;var row=Stack(true);row.SetPadding(0,Dp(8),0,Dp(10));
        (ResourceDonutView Donut,TextView Detail) Metric(string label,string unit,Action detail)
        {
            var column=Stack();column.SetGravity(GravityFlags.CenterHorizontal);TouchFeedback(column,12);
            var donut=new ResourceDonutView(this,palette.Accent,palette.Divider,palette.Text,palette.Muted,bold,unit);
            column.AddView(donut,new LinearLayout.LayoutParams(-1,Dp(104)));
            var name=Text(label,12,true);name.Gravity=GravityFlags.Center;name.SetPadding(0,Dp(3),0,Dp(5));column.AddView(name,Match());
            var amount=Text("",10,color:palette.Muted);amount.Gravity=GravityFlags.Center;amount.SetMaxLines(2);column.AddView(amount,Match());
            column.Click+=(_,_)=>detail();row.AddView(column,new LinearLayout.LayoutParams(0,-2,1));return(donut,amount);
        }
        (powerDonut,powerStatus)=Metric("Battery","left",()=>ResourceDetails("Battery"));
        (memoryDonut,memoryAmount)=Metric("Memory","used",()=>ResourceDetails("Memory"));
        (storageDonut,storageAmount)=Metric("Storage","used",()=>ResourceDetails("Storage"));panel.AddView(row,Match());
        resourcesFootnote=Text("",10,color:palette.Muted);resourcesFootnote.SetLineSpacing(Dp(2),1);resourcesFootnote.SetPadding(0,Dp(6),0,Dp(8));panel.AddView(resourcesFootnote,Match());
        UpdateResources(true);
    }
    private void UpdateResources(bool force=false)
    {
        if(!force&&(!resumed||page!="Root"||searching||resourcesPanel?.IsAttachedToWindow!=true||openSheetCount>0))return;
        using var visible=new Android.Graphics.Rect();
        if(!force&&resourcesPanel?.GetGlobalVisibleRect(visible)!=true)return;
        if(resourceSnapshot is null||Now-resourceSnapshot.At>=10_000||Now<resourceSnapshot.At)
        {
            resourceSnapshot=PhoneResources.Read(this);
        }
        else if(!force)return;
        PaintResources();
    }
    private void PaintResources()
    {
        if(resourceSnapshot is not {} value)return;
        powerDonut?.Update(value.Battery);memoryDonut?.Update(value.Memory);storageDonut?.Update(value.Storage);
        if(powerStatus is not null)powerStatus.Text=value.BatteryState;
        if(memoryAmount is not null)memoryAmount.Text=value.MemoryTotal>0?PhoneResources.Size(value.MemoryTotal-value.MemoryAvailable)+" used":"Unavailable";
        if(storageAmount is not null)storageAmount.Text=value.StorageTotal>0?PhoneResources.Size(value.StorageAvailable)+" available":"Unavailable";
        if(memoryDonut is not null)memoryDonut.ContentDescription="Memory used: "+(value.Memory is {} memory?$"{memory:0}%":"unavailable");
        if(storageDonut is not null)storageDonut.ContentDescription="Storage used: "+(value.Storage is {} storage?$"{storage:0}%":"unavailable");
        if(powerDonut is not null)powerDonut.ContentDescription="Battery remaining: "+(value.Battery is {} power?$"{power:0}%":"unavailable");
        if(resourcesFootnote is not null)resourcesFootnote.Text=(value.Temperature is {} temperature?$"Battery {temperature:0.#}°C · ":"")+"Updates every 10s while open.";
    }
    private void ResourceDetails(string metric)=>Sheet(metric,(panel,sheet)=>
    {
        if(resourceSnapshot is not {} value)return;
        void Detail(string label,string text){SectionCaption(panel,label);Paragraph(text,panel);}
        if(metric=="Memory")
        {
            if(value.MemoryTotal>0){Detail("Used",PhoneResources.Size(value.MemoryTotal-value.MemoryAvailable));Detail("Available",PhoneResources.Size(value.MemoryAvailable));Detail("System total",PhoneResources.Size(value.MemoryTotal));}
            else Paragraph("Memory information is unavailable on this device.",panel);
            Paragraph("Android can reclaim cached memory when apps need it. A high used percentage does not by itself mean the phone is struggling. The total excludes memory reserved by the device.",panel);
        }
        else if(metric=="Storage")
        {
            if(value.StorageTotal>0){Detail("Used",PhoneResources.Size(value.StorageTotal-value.StorageAvailable));Detail("Available to apps",PhoneResources.Size(value.StorageAvailable));Detail("Data volume total",PhoneResources.Size(value.StorageTotal));}
            else Paragraph("Storage information is unavailable on this device.",panel);
            Paragraph("This is the internal data volume used by apps, rather than the device's advertised capacity. Space reserved by Android is included in the unavailable portion.",panel);
        }
        else
        {
            Detail("Remaining",value.Battery is {} battery?$"{battery:0}%":"Unavailable");Detail("State",value.BatteryState);
            if(value.Temperature is {} temperature)Detail("Battery temperature",$"{temperature:0.#}°C");
            Paragraph("Temperature is reported by the battery sensor. It is not a CPU temperature reading.",panel);
        }
    });
}
