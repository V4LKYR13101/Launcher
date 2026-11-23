using Android.Content;
using Android.Views;
using Android.Widget;
using LauncherApp.Services;
using LauncherApp.UI;

namespace LauncherApp;

public partial class MainActivity
{
    private enum AppSearchMode { Apps, Files, Download }
    private const string DownloadsSettingsPage = "Downloads";
    internal const string DownloadHomeExtra = "open_home_after_download";
    private AppSearchMode searchMode;
    private System.Uri? pendingDownloadUrl;
    private string searchBeforeDownload = "";

    private bool SearchingDownloads => searching && IsAppsSurface && searchMode == AppSearchMode.Download;

    private void AddSearchModeToggle(LinearLayout pill)
    {
        var next = !searching ? AppSearchMode.Files : searchMode switch
        {
            AppSearchMode.Apps => AppSearchMode.Files,
            AppSearchMode.Files when DownloadRequest.Enabled(this) => AppSearchMode.Download,
            _ => AppSearchMode.Apps
        };
        string glyph = SearchingDownloads ? "download" : !searching || SearchingFiles ? "file" : "search";
        var toggle = new FrameLayout(this)
        {
            ContentDescription = next switch
            {
                AppSearchMode.Files => searching ? "Switch to file search" : "Search files",
                AppSearchMode.Download => "Switch to download",
                _ => "Switch to app search"
            }
        };
        var icon = new GlyphView(this, glyph, SearchingDownloads || SearchingFiles ? palette.Accent : palette.Text)
        {
            ImportantForAccessibility = ImportantForAccessibility.No
        };
        toggle.AddView(icon, new FrameLayout.LayoutParams(Dp(22), Dp(22), GravityFlags.Center));
        TouchFeedback(toggle, 24);
        toggle.Click += (_, _) =>
        {
            if (next == AppSearchMode.Download)
            {
                searchBeforeDownload = query;
                BeginSearch(next, keepQuery: false);
                return;
            }
            if (SearchingDownloads)
                query = searchBeforeDownload;
            BeginSearch(next, keepQuery: searching);
        };
        if (!searching)
        {
            BindHoldHaptics(toggle);
            toggle.LongClick += (_, _) => QuickControls();
        }
        pill.AddView(toggle, new LinearLayout.LayoutParams(Dp(48), Dp(48)) { LeftMargin = Dp(2), RightMargin = Dp(4) });
    }

    private void UpdateDownloadButton(View button) =>
        button.Visibility = DirectFileTransfer.TryGetUrl(query, out _) ? ViewStates.Visible : ViewStates.Gone;

    private void DownloadSearchUrl()
    {
        if (!SearchingDownloads || !DirectFileTransfer.TryGetUrl(query, out var url))
            return;
        if (pendingDownloadUrl is not null)
            return;
        pendingDownloadUrl = url;
        if (!DownloadRequest.RequestPermission(this))
            ContinueSearchDownload();
    }

    private void ContinueSearchDownload()
    {
        var url = pendingDownloadUrl;
        pendingDownloadUrl = null;
        if (url is null || IsDestroyed)
            return;
        if (DownloadRequest.Start(this, url) is { } error)
            Toast(error);
        else
        {
            HideKeyboard();
            Navigate("Tiles");
        }
    }

    private void AddDownloadsSettingsLink(LinearLayout parent) => SettingRow(parent, DownloadsSettingsPage,
        DownloadRequest.Enabled(this) ? "On · download files and view history" : "Off · view download history",
        () => Navigate(DownloadsSettingsPage), "download", "share links transfer file history");

    private void RenderDownloadsSettings()
    {
        var options = AddSettingsCard();
        ToggleRow(options, "Enable downloads", DownloadRequest.Enabled(this), enabled => DownloadRequest.SetEnabled(this, enabled));

        var history = AddSettingsCard("Download history");
        var entries = DownloadHistory.Read(this);
        if (entries.Length == 0)
            Paragraph("Files downloaded with Launcher will appear here.", history);
        foreach (var entry in entries)
            SettingRow(history, entry.Name, entry.CompletedAt.ToLocalTime().ToString("g"), () => OpenDownload(entry), "file");
    }

    private void RefreshDownloadHistory()
    {
        if (page == DownloadsSettingsPage && resumed && !IsDestroyed)
            RefreshBodyInPlace();
    }

    private void OpenDownload(DownloadHistory.Entry entry)
    {
        using var uri = entry.IsLegacyPath ? SharedFileProvider.UriFor(entry.Location) : Android.Net.Uri.Parse(entry.Location)!;
        try
        {
            using var file = ContentResolver!.OpenFileDescriptor(uri, "r");
            if (file is null)
            {
                Toast("This downloaded file is no longer available.");
                return;
            }
        }
        catch (Exception error) when (error is Java.IO.FileNotFoundException or Java.Lang.SecurityException)
        {
            Toast("This downloaded file is no longer available.");
            return;
        }
        using var intent = new Intent(Intent.ActionView);
        intent.SetDataAndType(uri, entry.MimeType);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        OpenIntent(intent);
    }
}
