using Android.App;
using Android.Content;
using Android.Gms.Wearable;
using AndroidX.Wear.WatchFace.Complications.Datasource;

namespace LauncherWear;

[Service(Exported = true)]
[IntentFilter(["com.google.android.gms.wearable.DATA_CHANGED"],
    DataScheme = "wear", DataHost = "*", DataPath = AccentData.Path)]
public sealed class AccentListenerService : WearableListenerService
{
    public override void OnDataChanged(DataEventBuffer events)
    {
        foreach (var change in events)
        {
            if (change.Type != DataEvent.TypeChanged || change.DataItem?.Uri?.Path != AccentData.Path)
                continue;

            using var item = DataMapItem.FromDataItem(change.DataItem);
            using var map = item?.DataMap;
            if (map?.ContainsKey(AccentData.ColorKey) != true)
                continue;

            int color = map.GetInt(AccentData.ColorKey);
            using var preferences = GetSharedPreferences("launcher-theme", FileCreationMode.Private);
            if (preferences!.GetInt(AccentData.ColorKey, AccentData.DefaultColor) == color)
                continue;
            using var editor = preferences.Edit();
            editor!.PutInt(AccentData.ColorKey, color)!.Apply();

            using var component = new ComponentName(PackageName!, AccentComplicationService.ComponentName);
            ComplicationDataSourceUpdateRequester.Create(this, component).RequestUpdateAll();
        }
    }
}
