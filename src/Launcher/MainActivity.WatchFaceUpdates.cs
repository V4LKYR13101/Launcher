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
        Paragraph("Pair a Wear OS 6 or later watch with this phone. Install and open Launcher for Wear OS on the watch, then send the face from here.");
        body.AddView(Button("Install Launcher Roll", () => _ = CheckWatchFaceUpdateAsync(WatchFaceAction.FirstInstall), true), Match());
        Gap(body, 12);
        body.AddView(Button("Check for watch face updates", () => _ = CheckWatchFaceUpdateAsync(WatchFaceAction.CheckUpdates)), Match());
        Gap(body, 12);
        body.AddView(Button("Get Launcher for Wear OS",
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
            if (manual) WearMessage("Checking your watch. Try again in a moment.");
            return;
        }
        if (!manual && Android.OS.SystemClock.ElapsedRealtime() < nextWatchUpdateCheck)
            return;

        watchUpdateBusy = true;
        if (manual) WearMessage("Connecting to your watch…");
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
                if (manual) WearMessage("Checking Kevlog for Launcher Roll…");
                var update = await AppUpdates.CheckWatchFaceAsync(updateLifetime.Token);
                if (IsDestroyed || !resumed)
                    return;
                if (update is null || update.Release.Build <= installedBuild)
                {
                    if (manual) WearMessage(update is null ? "No Launcher Roll release is available yet." : "Launcher Roll is up to date.");
                    return;
                }
                string nodeId = node.Id!;
                if (!manual && !OfferedWatchUpdates.Add((nodeId, update.Release.Build)))
                    continue;

                if (manual) WearMessage($"Launcher Roll {update.Release.Version} is available for {node.DisplayName}.");
                Confirm(firstInstall ? "Install Launcher Roll" : "Update Launcher Roll",
                    $"{(firstInstall ? "Install" : "Update to")} version {update.Release.Version} on {node.DisplayName}?",
                    firstInstall ? "Download and install" : "Download and update",
                    () => _ = InstallWatchFaceAsync(nodeId, update));
                return;
            }
            if (manual) WearMessage((firstInstall, foundInstalledFace, foundEmptyWatch, unsupported) switch
            {
                (true, true, _, _) => "Launcher Roll is already installed. Select it in your watch face picker.",
                (false, _, true, _) => "Launcher Roll is available for first-time setup. Tap Install Launcher Roll.",
                (_, _, _, true) => "Installing from Launcher requires Wear OS 6 or later.",
                _ when connected.Count == 0 => "No connected watch found. Pair your watch with this phone and check that it's connected.",
                _ => "Launcher couldn't reach its watch companion. Install or update Launcher for Wear OS, open it on your watch, then try again."
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            Log.Debug("LauncherWear", $"Watch face check failed: {error.Message}");
            if (manual && !IsDestroyed) WearMessage("Could not connect to the watch or check Kevlog. Try again.");
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
        if (page == WearSettingsPage) WearMessage("Downloading Launcher Roll…");
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
            if (page == WearSettingsPage) WearMessage("Sending Launcher Roll to your watch…");
            using var messages = WearableClass.GetMessageClient(this);
            string result = await RequestWatchAsync(messages, nodeId, "install", request);
            if (!IsDestroyed) WearMessage(result switch
            {
                "added" => "Launcher Roll installed. Select it in your watch face picker.",
                "updated" => "Launcher Roll updated on your watch.",
                "current" => "Launcher Roll is already up to date.",
                "no-slot" => "Your watch has no available Launcher watch face slot.",
                _ => "Your watch could not install Launcher Roll."
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            Log.Debug("LauncherWear", $"Watch face install failed: {error.Message}");
            if (!IsDestroyed) WearMessage("Launcher couldn't send the watch face. Reconnect your watch and try again.");
        }
        finally
        {
            File.Delete(apkPath);
            watchUpdateBusy = false;
        }
    }
}
