using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Widget;
using StorageEnvironment = Android.OS.Environment;

namespace LauncherApp.Services;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public sealed class DirectDownloadService : Service
{
    internal const string UrlExtra = "download_url";
    private const string Channel = "file_downloads";
    private const string CancelAction = "com.launcher.home.CANCEL_FILE_DOWNLOAD";
    private static int lastNotificationId = 9200;
    private sealed record RunningDownload(int Id, CancellationTokenSource Cancellation, ActiveDownloads.Transfer Transfer);
    private readonly Dictionary<int, RunningDownload> active = new();
    private int foregroundId, lastStartId;

    private NotificationManager Notifications => (NotificationManager)GetSystemService(NotificationService)!;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        lastStartId = startId;
        if (intent?.Action == CancelAction)
        {
            if (active.TryGetValue(intent.GetIntExtra("id", 0), out var job))
                job.Cancellation.Cancel();
            if (active.Count == 0)
                StopSelf(startId);
            return StartCommandResult.NotSticky;
        }
        if (!DirectFileTransfer.TryGetUrl(intent?.GetStringExtra(UrlExtra) ?? "", out var url))
        {
            if (active.Count == 0)
                StopSelf(startId);
            return StartCommandResult.NotSticky;
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            using var channel = new NotificationChannel(Channel, "File downloads", NotificationImportance.Low);
            Notifications.CreateNotificationChannel(channel);
        }
        int id = Interlocked.Increment(ref lastNotificationId);
        var download = new RunningDownload(id, new(), ActiveDownloads.Start(DirectFileTransfer.SuggestedName(url)));
        active.Add(id, download);
        using (var notification = Progress(download))
        {
            if (foregroundId == 0)
            {
                foregroundId = id;
                StartForeground(id, notification);
            }
            else
                Notifications.Notify(id, notification);
        }
        ShowToast("Download started.");
        _ = RunDownload(url, download);
        return StartCommandResult.NotSticky;
    }

    private async Task RunDownload(System.Uri url, RunningDownload download)
    {
        string title;
        string message;
        DownloadHistory.Entry? completed = null;
        string path = Path.Combine(CacheDir!.AbsolutePath, $"direct-download-{Guid.NewGuid():N}.part");
        long lastUpdate = 0;
        var token = download.Cancellation.Token;
        try
        {
            completed = await Task.Run(async () =>
            {
                var file = await DirectFileTransfer.Download(url, path, (bytes, size) =>
                {
                    download.Transfer.Report(bytes, size);
                    long now = System.Environment.TickCount64;
                    long last = Interlocked.Read(ref lastUpdate);
                    if (now - last < 1000 || Interlocked.CompareExchange(ref lastUpdate, now, last) != last)
                        return;
                    using var notification = Progress(download);
                    Notifications.Notify(download.Id, notification);
                }, token, download.Transfer.Name);
                return await Publish(path, file, token);
            }, token);
            title = "Saved to Downloads";
            message = completed.Name;
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested)
        {
            title = "Download canceled";
            message = "";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or HttpRequestException or
            TimeoutException or Java.Lang.Exception || DirectFileTransfer.IsWebPage(error))
        {
            title = "Download failed";
            message = DirectFileTransfer.IsWebPage(error) ? "Use a direct file link instead of a web page." :
                error is HttpRequestException { StatusCode: { } status } ? $"The server returned HTTP {(int)status}." :
                error is IOException or UnauthorizedAccessException or Java.Lang.Exception ? "Check available storage and try again." :
                "Check your connection and the link, then try again.";
        }
        finally
        {
            download.Transfer.Dispose();
            try { File.Delete(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        using var main = new Handler(Looper.MainLooper!);
        main.Post(() =>
        {
            download.Cancellation.Dispose();
            if (completed is not null)
                DownloadHistory.Record(this, completed);
            if (!active.Remove(download.Id))
                return;
            if (foregroundId == download.Id)
            {
                var next = active.Values.FirstOrDefault();
                foregroundId = next?.Id ?? 0;
                if (next is null)
                    StopForeground(StopForegroundFlags.Remove);
                else
                {
                    using var foreground = Progress(next);
                    StartForeground(next.Id, foreground);
                }
            }
            Notifications.Cancel(download.Id);
            if (message.Length > 0)
            {
                using var builder = Builder();
                using var notification = builder.SetContentTitle(title)!.SetContentText(message)!.SetAutoCancel(true)!.Build();
                Notifications.Notify(download.Id, notification);
            }
            ShowToast(message.Length > 0 ? $"{title}. {message}" : title);
            if (active.Count == 0)
                StopSelf(lastStartId);
        });
    }

    private Notification Progress(RunningDownload download)
    {
        var status = download.Transfer.Current;
        using var cancel = new Intent(this, typeof(DirectDownloadService)).SetAction(CancelAction)!.PutExtra("id", download.Id);
        using var pending = PendingIntent.GetService(this, download.Id, cancel, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        using var builder = Builder();
        using var icon = Android.Graphics.Drawables.Icon.CreateWithResource(this, Android.Resource.Drawable.IcMenuCloseClearCancel);
        using var actionBuilder = new Notification.Action.Builder(icon, "Cancel", pending);
        using var action = actionBuilder.Build();
        return builder.SetContentTitle(status.Name)!
            .SetContentText(status.Percent is { } percent ? $"{percent}%" : "Downloading to Downloads")!
            .SetProgress(100, status.Percent ?? 0, status.Percent is null)!
            .SetOngoing(true)!.SetOnlyAlertOnce(true)!
            .AddAction(action)!.Build();
    }

    private Notification.Builder Builder()
    {
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26) ? new Notification.Builder(this, Channel) : new Notification.Builder(this);
        return builder.SetSmallIcon(Android.Resource.Drawable.StatSysDownload)!.SetVisibility(NotificationVisibility.Private)!;
    }

    private void ShowToast(string message) => Toast.MakeText(this, message, ToastLength.Short)?.Show();

    private async Task<DownloadHistory.Entry> Publish(string path, DirectFileTransfer.FileDetails file, CancellationToken token)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            string target = await PublishLegacy(path, file.Name, token);
            return new(Path.GetFileName(target), file.MimeType, target, true, DateTimeOffset.UtcNow);
        }

        var resolver = ContentResolver!;
        using var values = new ContentValues();
        values.Put("_display_name", file.Name);
        values.Put("mime_type", file.MimeType);
        values.Put("relative_path", StorageEnvironment.DirectoryDownloads);
        values.Put("is_pending", 1);
        using var uri = resolver.Insert(MediaStore.Downloads.ExternalContentUri!, values)
            ?? throw new IOException("Could not create a file in Downloads.");
        bool published = false;
        try
        {
            await using (var output = resolver.OpenOutputStream(uri) ?? throw new IOException("Could not open the Downloads file."))
            await using (var input = File.OpenRead(path))
                await input.CopyToAsync(output, token);
            token.ThrowIfCancellationRequested();
            values.Clear();
            values.Put("is_pending", 0);
            published = resolver.Update(uri, values, null, null) == 1;
            if (!published)
                throw new IOException("Could not publish the Downloads file.");
            using var cursor = resolver.Query(uri, ["_display_name"], null, null, null);
            string name = cursor?.MoveToFirst() == true ? cursor.GetString(0) ?? file.Name : file.Name;
            return new(name, file.MimeType, uri.ToString()!, false, DateTimeOffset.UtcNow);
        }
        finally
        {
            if (!published)
                resolver.Delete(uri, null, null);
        }
    }

    private static async Task<string> PublishLegacy(string path, string name, CancellationToken token)
    {
        string directory = StorageEnvironment.GetExternalStoragePublicDirectory(StorageEnvironment.DirectoryDownloads)!.AbsolutePath;
        Directory.CreateDirectory(directory);
        string staged = Path.Combine(directory, $".launcher-{Guid.NewGuid():N}.part");
        try
        {
            await using (var output = File.Open(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var input = File.OpenRead(path))
                await input.CopyToAsync(output, token);
            string stem = Path.GetFileNameWithoutExtension(name);
            string extension = Path.GetExtension(name);
            for (int suffix = 0; ; suffix++)
            {
                token.ThrowIfCancellationRequested();
                string candidate = suffix == 0 ? name : $"{stem} ({suffix}){extension}";
                string target = Path.Combine(directory, candidate);
                try
                {
                    File.Move(staged, target);
                    return target;
                }
                catch (IOException) when (File.Exists(target) || Directory.Exists(target)) { }
            }
        }
        finally { File.Delete(staged); }
    }

    public override void OnTimeout(int startId, ForegroundService serviceType)
    {
        StopSelf();
    }

    public override void OnDestroy()
    {
        foreach (var download in active.Values)
        {
            download.Cancellation.Cancel();
            download.Transfer.Dispose();
            Notifications.Cancel(download.Id);
        }
        active.Clear();
        foregroundId = 0;
        base.OnDestroy();
    }
}
