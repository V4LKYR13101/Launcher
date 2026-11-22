using Android.Content;
using Android.Provider;
using Android.Views;
using Android.Widget;
using LauncherApp.Services;
using LauncherApp.UI;
using Uri = Android.Net.Uri;

namespace LauncherApp;

public partial class MainActivity
{
    private const int FileSearchStoragePermission = 917;
    private bool fileSearchMode, fileSearchAccess, fileSearchFailed;
    private SearchFile[] searchFiles = [];
    private CancellationTokenSource? fileSearchLoad;
    private bool SearchingFiles => searching && IsAppsSurface && fileSearchMode;
    private sealed record SearchFile(string Path, string Name, string Folder);

    private void AddFileSearchToggle(LinearLayout pill)
    {
        bool active = SearchingFiles;
        var toggle = new FrameLayout(this)
        {
            ContentDescription = active ? "Turn off file search" : "Turn on file search",
            Selected = active
        };
        var icon = new GlyphView(this, "file", active ? palette.Accent : palette.Text)
        {
            ImportantForAccessibility = ImportantForAccessibility.No
        };
        toggle.AddView(icon, new FrameLayout.LayoutParams(Dp(22), Dp(22), GravityFlags.Center));
        TouchFeedback(toggle, 24);
        toggle.Click += (_, _) => BeginSearch(files: !SearchingFiles, keepQuery: searching);
        if (!searching)
        {
            BindHoldHaptics(toggle);
            toggle.LongClick += (_, _) => QuickControls();
        }
        pill.AddView(toggle, new LinearLayout.LayoutParams(Dp(48), Dp(48)) { LeftMargin = Dp(2), RightMargin = Dp(4) });
    }

    private void RequestFileSearchAccess()
    {
        HideKeyboard();
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            RequestPermissions([Android.Manifest.Permission.ReadExternalStorage], FileSearchStoragePermission);
            return;
        }

        using var package = Uri.FromParts("package", PackageName, null);
        using var settings = new Intent(Settings.ActionManageAppAllFilesAccessPermission, package);
        try { StartActivity(settings); }
        catch (ActivityNotFoundException)
        {
            using var fallback = new Intent(Settings.ActionManageAllFilesAccessPermission);
            OpenIntent(fallback);
        }
    }

    private void UpdateFileSearchAccess()
    {
        if (!SearchingFiles)
            return;

        bool access = SharedStorage.CanRead(this);
        if (access == fileSearchAccess)
            return;

        fileSearchAccess = access;
        ResetFileSearch();
        RenderBody();
        if (!fileSearchAccess)
            return;

        _ = LoadSearchFilesAsync();
        FocusSearchInput();
    }

    private void ResetFileSearch()
    {
        var load = fileSearchLoad;
        fileSearchLoad = null;
        load?.Cancel();
        searchFiles = [];
        fileSearchFailed = false;
    }

    private async Task LoadSearchFilesAsync()
    {
        using var load = new CancellationTokenSource();
        fileSearchLoad = load;
        RefreshAppSearch();
        try
        {
            var files = await Task.Run(() => ReadSearchFiles(load.Token), load.Token);
            if (ReferenceEquals(fileSearchLoad, load))
                searchFiles = files;
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception error) when (error is Java.Lang.RuntimeException or IOException or UnauthorizedAccessException)
        {
            if (ReferenceEquals(fileSearchLoad, load))
                fileSearchFailed = true;
        }
        finally
        {
            if (ReferenceEquals(fileSearchLoad, load))
            {
                fileSearchLoad = null;
                RefreshAppSearch();
            }
        }
    }

    private SearchFile[] ReadSearchFiles(CancellationToken token)
    {
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        var results = new List<SearchFile>();
        foreach (var volume in SharedStorage.MountedVolumes(this))
        {
            var directories = new Stack<DirectoryInfo>();
            directories.Push(new DirectoryInfo(volume.Root));
            while (directories.TryPop(out var directory))
            {
                token.ThrowIfCancellationRequested();
                string folder = directory.FullName == volume.Root ? volume.Label : volume.Label + directory.FullName[volume.Root.Length..];
                try
                {
                    foreach (var entry in directory.EnumerateFileSystemInfos("*", options))
                    {
                        token.ThrowIfCancellationRequested();
                        if (entry is DirectoryInfo child)
                        {
                            if (SharedStorage.Contains(volume.Root, child.FullName))
                                directories.Push(child);
                        }
                        else
                            results.Add(new(entry.FullName, entry.Name, folder));
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
        }
        return results.OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(file => file.Folder, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private IEnumerable<SearchFile> FileSearchMatches()
    {
        string search = query.Trim();
        if (search.Length == 0)
            return [];
        var terms = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return searchFiles.Where(file => terms.All(term => file.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            .OrderByDescending(file => file.Name.StartsWith(search, StringComparison.CurrentCultureIgnoreCase));
    }

    private void RefreshFileSearch()
    {
        var matches = FileSearchMatches().Select(file => new SearchHit(null, null, file)).ToArray();
        appSearchAdapter?.Replace(matches);
        appSearchList?.SetSelection(0);
        if (appSearchTitle is not null)
            appSearchTitle.Text = "Files";
        if (appSearchCount is not null)
            appSearchCount.Text = !fileSearchAccess ? "Allow access to search your device’s shared storage."
                : fileSearchLoad is not null ? "Reading files…"
                : fileSearchFailed ? "Couldn’t read shared storage. Try reopening file search."
                : searchFiles.Length == 0 ? "No files found in shared storage."
                : string.IsNullOrWhiteSpace(query) ? "Type a filename to search."
                : matches.Length == 0 ? "No matching files." : Counted(matches.Length, "file");
    }

    private void OpenSearchFile(SearchFile file)
    {
        HideKeyboard();
        if (!SharedStorage.CanRead(this) || !File.Exists(file.Path))
        {
            Toast("This file is no longer available.");
            return;
        }
        using var uri = SharedFileProvider.UriFor(file.Path);
        using var intent = new Intent(Intent.ActionView);
        intent.SetDataAndType(uri, SharedFileProvider.MimeType(file.Path));
        intent.ClipData = ClipData.NewRawUri(file.Name, uri);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        OpenIntent(intent);
    }
}
