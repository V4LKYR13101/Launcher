using Android.Views;
using Android.Widget;
using LauncherApp.Services;
using LauncherApp.UI;
namespace LauncherApp;

public partial class MainActivity
{
    private LinearLayout? resourcesPanel;
    private ResourceDonutView? memoryDonut, storageDonut, powerDonut;
    private TextView? memoryAmount, storageAmount, powerStatus, resourcesFootnote;
    private ResourceSnapshot? resourceSnapshot;
    private void AddResourcesCard()
    {
        state.ResourcesVisible = true;
        state.RootHidden.Remove("resources");
        state.CollapsedCards["resources"] = false;
        if (!state.RootOrder.Contains("resources"))
            state.RootOrder.Add("resources");
        Save();
        Navigate("Root");
    }
    private void BuildResources(LinearLayout panel)
    {
        resourcesPanel = panel;
        var row = Stack(true);
        row.SetPadding(0, Dp(8), 0, Dp(10));
        (ResourceDonutView Donut, TextView Detail) Metric(string label, string unit, Action detail)
        {
            var column = Stack();
            column.SetGravity(GravityFlags.CenterHorizontal);
            TouchFeedback(column, 12);
            var donut = new ResourceDonutView(this, palette.Accent, palette.Divider, palette.Text, palette.Muted, bold, unit);
            column.AddView(donut, new LinearLayout.LayoutParams(-1, Dp(104)));
            var name = Text(label, 12, true);
            name.Gravity = GravityFlags.Center;
            name.SetPadding(0, Dp(3), 0, Dp(5));
            column.AddView(name, Match());
            var amount = Text("", 10, color: palette.Muted);
            amount.Gravity = GravityFlags.Center;
            amount.SetMaxLines(2);
            column.AddView(amount, Match());
            column.Click += (_, _) => detail();
            row.AddView(column, new LinearLayout.LayoutParams(0, -2, 1));
            return (donut, amount);
        }
        (powerDonut, powerStatus) = Metric(Localize(Resource.String.ui_battery_2), Localize(Resource.String.ui_left_2), () => ResourceDetails(Localize(Resource.String.ui_battery_2)));
        (memoryDonut, memoryAmount) = Metric(Localize(Resource.String.ui_memory), Localize(Resource.String.ui_used_2), () => ResourceDetails(Localize(Resource.String.ui_memory)));
        (storageDonut, storageAmount) = Metric(Localize(Resource.String.ui_storage), Localize(Resource.String.ui_used_2), () => ResourceDetails(Localize(Resource.String.ui_storage)));
        panel.AddView(row, Match());
        resourcesFootnote = Text("", 10, color: palette.Muted);
        resourcesFootnote.SetLineSpacing(Dp(2), 1);
        resourcesFootnote.SetPadding(0, Dp(6), 0, Dp(8));
        panel.AddView(resourcesFootnote, Match());
        UpdateResources(true);
    }
    private void UpdateResources(bool force = false)
    {
        if (!force && (!resumed || page != "Root" || searching || resourcesPanel?.IsAttachedToWindow != true || openSheetCount > 0))
            return;
        using var visible = new Android.Graphics.Rect();
        if (!force && resourcesPanel?.GetGlobalVisibleRect(visible) != true)
            return;
        if (resourceSnapshot is null || Now - resourceSnapshot.At >= 10_000 || Now < resourceSnapshot.At)
        {
            resourceSnapshot = PhoneResources.Read(this);
        }
        else if (!force)
            return;
        PaintResources();
    }
    private void PaintResources()
    {
        if (resourceSnapshot is not { } value)
            return;
        powerDonut?.Update(value.Battery);
        memoryDonut?.Update(value.Memory);
        storageDonut?.Update(value.Storage);
        if (powerStatus is not null)
            powerStatus.Text = value.BatteryState;
        if (memoryAmount is not null)
            memoryAmount.Text = value.MemoryTotal > 0 ? PhoneResources.Size(value.MemoryTotal - value.MemoryAvailable) + Localize(Resource.String.ui_used_3) : Localize(Resource.String.ui_unavailable);
        if (storageAmount is not null)
            storageAmount.Text = value.StorageTotal > 0 ? PhoneResources.Size(value.StorageAvailable) + Localize(Resource.String.ui_available_2) : Localize(Resource.String.ui_unavailable);
        if (memoryDonut is not null)
            memoryDonut.ContentDescription = Localize(Resource.String.ui_memory_used) + (value.Memory is { } memory ? $"{memory:0}%" : Localize(Resource.String.ui_unavailable_2));
        if (storageDonut is not null)
            storageDonut.ContentDescription = Localize(Resource.String.ui_storage_used) + (value.Storage is { } storage ? $"{storage:0}%" : Localize(Resource.String.ui_unavailable_2));
        if (powerDonut is not null)
            powerDonut.ContentDescription = Localize(Resource.String.ui_battery_remaining) + (value.Battery is { } power ? $"{power:0}%" : Localize(Resource.String.ui_unavailable_2));
        if (resourcesFootnote is not null)
            resourcesFootnote.Text = (value.Temperature is { } temperature ? Localize(Resource.String.ui_battery_valuec, temperature) : "") + Localize(Resource.String.ui_updates_every_10s_while_open);
    }
    private void ResourceDetails(string metric) => Sheet(metric, (panel, sheet) =>
    {
        if (resourceSnapshot is not { } value)
            return;
        void Detail(string label, string text)
        {
            SectionCaption(panel, label);
            Paragraph(text, panel);
        }
        if (metric == "Memory")
        {
            if (value.MemoryTotal > 0)
            {
                Detail(Localize(Resource.String.ui_used), PhoneResources.Size(value.MemoryTotal - value.MemoryAvailable));
                Detail(Localize(Resource.String.ui_available), PhoneResources.Size(value.MemoryAvailable));
                Detail(Localize(Resource.String.ui_system_total), PhoneResources.Size(value.MemoryTotal));
            }
            else
                Paragraph(Localize(Resource.String.ui_memory_information_is_unavailable_on_this_device), panel);
            Paragraph(Localize(Resource.String.ui_android_can_reclaim_cached_memory_when_apps_need_it), panel);
        }
        else if (metric == "Storage")
        {
            if (value.StorageTotal > 0)
            {
                Detail(Localize(Resource.String.ui_used), PhoneResources.Size(value.StorageTotal - value.StorageAvailable));
                Detail(Localize(Resource.String.ui_available_to_apps), PhoneResources.Size(value.StorageAvailable));
                Detail(Localize(Resource.String.ui_data_volume_total), PhoneResources.Size(value.StorageTotal));
            }
            else
                Paragraph(Localize(Resource.String.ui_storage_information_is_unavailable_on_this_device), panel);
            Paragraph(Localize(Resource.String.ui_this_is_the_internal_data_volume_used_by_apps), panel);
        }
        else
        {
            Detail(Localize(Resource.String.ui_remaining), value.Battery is { } battery ? $"{battery:0}%" : "Unavailable");
            Detail(Localize(Resource.String.ui_state), value.BatteryState);
            if (value.Temperature is { } temperature)
                Detail(Localize(Resource.String.ui_battery_temperature), $"{temperature:0.#}°C");
            Paragraph(Localize(Resource.String.ui_temperature_is_reported_by_the_battery_sensor_it_is), panel);
        }
    });
}
