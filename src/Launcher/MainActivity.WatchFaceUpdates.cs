using Android.Gms.Extensions;
using Android.Gms.Wearable;
using Android.Runtime;
using Android.Util;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
using LauncherWear;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace LauncherApp;

public partial class MainActivity
{
    private const string WearSettingsPage = "Wear OS";
    private const string WatchFaceRpc = "/launcher/watchface/v1/";
    private static readonly HashSet<(string Node, long Build)> OfferedWatchUpdates = [];
    private bool watchUpdateBusy;
    private long nextWatchUpdateCheck;
    private TextView? wearStatusView;
    private string wearStatus = "";

    private enum WatchFaceAction { AutomaticUpdate, CheckUpdates, FirstInstall }

    private void RenderWearSettings()
    {
        Title(WearSettingsPage);
        Paragraph(Localize(Resource.String.ui_pair_a_wear_os_6_or_later_watch_with));
        body.AddView(Button(Localize(Resource.String.ui_install_launcher_roll), () => _ = CheckWatchFaceUpdateAsync(WatchFaceAction.FirstInstall), true), Match());
        Gap(body, 12);
        body.AddView(Button(Localize(Resource.String.ui_check_for_watch_face_updates), () => _ = CheckWatchFaceUpdateAsync(WatchFaceAction.CheckUpdates)), Match());
        Gap(body, 12);
        body.AddView(Button(Localize(Resource.String.ui_get_launcher_for_wear_os),
            () => OpenUrl("https://kevlog.co.za/projects/launcher/")), Match());
        Gap(body, 12);
        wearStatusView = Text(wearStatus, 15);
        wearStatusView.AccessibilityLiveRegion = Android.Views.AccessibilityLiveRegion.Polite;
        body.AddView(wearStatusView, Match());
        Gap(body, 16);
        RenderRollControls();
    }

    private void RenderRollControls()
    {
        Title("Watch face settings");
        Paragraph("Your choices sync to Launcher Roll on a paired watch with the latest Launcher for Wear OS. Follow watch leaves that choice to the watch.");
        var prefs = WearRollPublisher.Settings(this);
        void Changed()
        {
            WearRollPublisher.Start(this, state.Timers);
            WearAccentPublisher.Publish(this, WearRollPublisher.AccentFor(this, state.Accent));
            RenderBody();
        }
        void Choice(string key, string title, params (string Value, string Label)[] options)
        {
            string? current = prefs.GetString(key, "watch");
            string label = options.FirstOrDefault(o => o.Value == current).Label ?? options[0].Label;
            SettingRow(body, title, label, () => Sheet(title, (panel, dialog) =>
            {
                foreach (var option in options)
                    SettingRow(panel, option.Label, option.Value == current ? "Selected" : "", () =>
                    {
                        prefs.Edit()!.PutString(key, option.Value)!.Apply();
                        dialog.Dismiss();
                        Changed();
                    });
            }));
        }
        Choice(RollSync.Clock, "Time format", ("watch", "Follow watch"), ("12", "12-hour"), ("24", "24-hour"));
        Choice(RollSync.Layout, "Layout", ("watch", "Follow watch"), ("daily", "Daily"), ("pure", "Pure"));
        Choice(RollSync.Date, "Show date", ("watch", "Follow watch"), ("on", "On"), ("off", "Off"));
        Choice(RollSync.AmbientDate, "Show date in always-on", ("watch", "Follow watch"), ("on", "On"), ("off", "Off"));
        ToggleRow(body, "Match Launcher accent", prefs.GetBoolean(RollSync.Accent, true), enabled =>
        {
            prefs.Edit()!.PutBoolean(RollSync.Accent, enabled)!.Apply();
            Changed();
        });
        ToggleRow(body, "Show phone downloads and timers", prefs.GetBoolean(RollSync.Activity, false), enabled =>
        {
            prefs.Edit()!.PutBoolean(RollSync.Activity, enabled)!.Apply();
            Changed();
        });
        body.AddView(Button("Sync watch face settings", () =>
        {
            WearRollPublisher.SyncNow(this, state.Timers);
            WearAccentPublisher.Publish(this, WearRollPublisher.AccentFor(this, state.Accent));
            Toast("Watch face settings queued for sync.");
        }), Match());
        Paragraph("If your watch kept a previous utility choice, choose Launcher Activity in the watch’s complication picker. Weather and other complication providers are chosen on the watch.");
    }

    private void WearMessage(string message)
    {
        wearStatus = message;
        if (page == WearSettingsPage && wearStatusView is not null)
            wearStatusView.Text = message;
        else
            Toast(message);
    }

    private async Task CheckWatchFaceUpdateAsync(WatchFaceAction action = WatchFaceAction.AutomaticUpdate)
    {
        bool manual = action != WatchFaceAction.AutomaticUpdate;
        bool firstInstall = action == WatchFaceAction.FirstInstall;
        if (watchUpdateBusy)
        {
            if (manual) WearMessage(Localize(Resource.String.ui_checking_your_watch_try_again_in_a_moment));
            return;
        }
        if (!manual && Android.OS.SystemClock.ElapsedRealtime() < nextWatchUpdateCheck)
            return;

        watchUpdateBusy = true;
        if (manual) WearMessage(Localize(Resource.String.ui_connecting_to_your_watch));
        try
        {
            using var nodes = WearableClass.GetNodeClient(this);
            var connected = await nodes.GetConnectedNodesAsync()
                .WaitAsync(TimeSpan.FromSeconds(8), updateLifetime.Token);
            using var messages = WearableClass.GetMessageClient(this);
            bool unsupported = false;
            bool foundInstalledFace = false;
            bool foundEmptyWatch = false;
            foreach (var node in connected.OrderByDescending(node => node.IsNearby))
            {
                string? result;
                try { result = await RequestWatchAsync(messages, node.Id!, "version", [], 8); }
                catch (Exception e) when (e is TimeoutException or Android.Gms.Common.Apis.ApiException) { continue; }
                if (result == "unsupported")
                {
                    unsupported = true;
                    continue;
                }
                if (!long.TryParse(result, NumberStyles.None, CultureInfo.InvariantCulture, out long installedBuild))
                    continue;
                foundInstalledFace |= installedBuild > 0;
                foundEmptyWatch |= installedBuild == 0;
                if (firstInstall != (installedBuild == 0))
                    continue;

                if (!manual)
                    nextWatchUpdateCheck = Android.OS.SystemClock.ElapsedRealtime() + (long)TimeSpan.FromHours(1).TotalMilliseconds;
                if (manual) WearMessage(Localize(Resource.String.ui_checking_kevlog_for_launcher_roll));
                var update = await AppUpdates.CheckWatchFaceAsync(updateLifetime.Token);
                if (IsDestroyed || !resumed)
                    return;
                if (update is null || update.Release.Build <= installedBuild)
                {
                    if (manual) WearMessage(update is null ? Localize(Resource.String.ui_no_launcher_roll_release_is_available_yet) : Localize(Resource.String.ui_launcher_roll_is_up_to_date));
                    return;
                }
                string nodeId = node.Id!;
                if (!manual && !OfferedWatchUpdates.Add((nodeId, update.Release.Build)))
                    continue;

                if (manual) WearMessage(Localize(Resource.String.ui_launcher_roll_value_is_available_for_value, update.Release.Version, node.DisplayName));
                Confirm(firstInstall ? Localize(Resource.String.ui_install_launcher_roll) : Localize(Resource.String.ui_update_launcher_roll),
                    Localize(Resource.String.ui_value_version_value_on_value, Localize(firstInstall ? Resource.String.ui_watch_install : Resource.String.ui_watch_update_to), update.Release.Version, node.DisplayName),
                    firstInstall ? Localize(Resource.String.ui_download_and_install) : Localize(Resource.String.ui_download_and_update),
                    () => _ = InstallWatchFaceAsync(nodeId, update));
                return;
            }
            if (manual) WearMessage((firstInstall, foundInstalledFace, foundEmptyWatch, unsupported) switch
            {
                (true, true, _, _) => Localize(Resource.String.ui_launcher_roll_is_already_installed_select_it_in_your),
                (false, _, true, _) => Localize(Resource.String.ui_launcher_roll_is_available_for_first_time_setup_tap),
                (_, _, _, true) => Localize(Resource.String.ui_installing_from_launcher_requires_wear_os_6_or_later),
                _ when connected.Count == 0 => Localize(Resource.String.ui_no_connected_watch_found_pair_your_watch_with_this),
                _ => Localize(Resource.String.ui_launcher_couldn_t_reach_its_watch_companion_install_or)
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            Log.Debug("LauncherWear", $"Watch face check failed: {error.Message}");
            if (manual && !IsDestroyed) WearMessage(Localize(Resource.String.ui_could_not_connect_to_the_watch_or_check_kevlog));
        }
        finally
        {
            watchUpdateBusy = false;
            if (!manual && nextWatchUpdateCheck <= Android.OS.SystemClock.ElapsedRealtime())
                nextWatchUpdateCheck = Android.OS.SystemClock.ElapsedRealtime() + (long)TimeSpan.FromMinutes(2).TotalMilliseconds;
        }
    }

    private async Task<string> RequestWatchAsync(MessageClient client, string node, string path, byte[] data, int seconds = 60)
    {
        using var reply = await client.SendRequest(node, WatchFaceRpc + path, data)!
            .AsAsync<Java.Lang.Object>().WaitAsync(TimeSpan.FromSeconds(seconds), updateLifetime.Token);
        var bytes = JNIEnv.GetArray<byte>(reply.Handle)
            ?? throw new InvalidDataException("The watch returned an empty response.");
        return Encoding.UTF8.GetString(bytes);
    }

    private async Task InstallWatchFaceAsync(string nodeId, WatchFaceRelease update)
    {
        if (watchUpdateBusy)
            return;
        watchUpdateBusy = true;
        if (page == WearSettingsPage) WearMessage(Localize(Resource.String.ui_downloading_launcher_roll));
        string apkPath = Path.Combine(CacheDir!.AbsolutePath, "launcher-roll-" + Guid.NewGuid().ToString("N") + ".apk");
        try
        {
            await AppUpdates.DownloadAsync(update.Release, apkPath, _ => { }, updateLifetime.Token);
            byte[] apk = await File.ReadAllBytesAsync(apkPath, updateLifetime.Token);
            byte[] token = Encoding.UTF8.GetBytes(update.ValidationToken);
            byte[] request = new byte[10 + token.Length + apk.Length];
            BinaryPrimitives.WriteInt64BigEndian(request, update.Release.Build);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8), (ushort)token.Length);
            token.CopyTo(request, 10);
            apk.CopyTo(request, 10 + token.Length);
            if (page == WearSettingsPage) WearMessage(Localize(Resource.String.ui_sending_launcher_roll_to_your_watch));
            using var messages = WearableClass.GetMessageClient(this);
            string result = await RequestWatchAsync(messages, nodeId, "install", request);
            if (!IsDestroyed) WearMessage(result switch
            {
                "added" => Localize(Resource.String.ui_launcher_roll_installed_select_it_in_your_watch_face),
                "updated" => Localize(Resource.String.ui_launcher_roll_updated_on_your_watch),
                "current" => Localize(Resource.String.ui_launcher_roll_is_already_up_to_date),
                "no-slot" => Localize(Resource.String.ui_your_watch_has_no_available_launcher_watch_face_slot),
                _ => Localize(Resource.String.ui_your_watch_could_not_install_launcher_roll)
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            Log.Debug("LauncherWear", $"Watch face install failed: {error.Message}");
            if (!IsDestroyed) WearMessage(Localize(Resource.String.ui_launcher_couldn_t_send_the_watch_face_reconnect_your));
        }
        finally
        {
            File.Delete(apkPath);
            watchUpdateBusy = false;
        }
    }
}
