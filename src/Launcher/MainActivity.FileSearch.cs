using Android.Content;
using Android.Provider;
using Android.Views;
using Android.Widget;
using LauncherApp.Services;
using LauncherApp.Services.ImageSearch;
using LauncherApp.UI;
using Color = Android.Graphics.Color;
using Uri = Android.Net.Uri;

namespace LauncherApp;

public partial class MainActivity
{
    private const int FileSearchStoragePermission = 917;
    private bool fileSearchAccess, fileSearchFailed;
    private FileSearchCatalog.Snapshot? fileSnapshot;
    private object? fileSearchLoad;
    private CancellationTokenSource? imageQueryLoad;
    private string? imageQuery;
    private SearchFile[] imageMatches = [];
    private bool imageQueryFailed, imageTextUsed;
    private (int Description, int Text, int People) imageIndexRevisionSeen;
    private HorizontalScrollView? fileTypeScroll;
    private LinearLayout? fileTypeRow;
    private FileSearchScope fileSearchScope;
    private string? selectedFileType;
    private FileSearchPill[] displayedFilePills = [];
    private TextView[] fileTypePills = [];
    private enum FileSearchScope { All, Images, Documents }
    private readonly record struct FileSearchPill(string Label, FileSearchScope? Scope, string? Extension);
    private static FileSearchPill[] ScopePills =>
    [
        new(Localize(Resource.String.ui_all), FileSearchScope.All, null),
        new(Localize(Resource.String.ui_images), FileSearchScope.Images, null),
        new(Localize(Resource.String.ui_documents), FileSearchScope.Documents, null)
    ];

    private (int Description, int Text, int People) PhotoSearchRevision =>
        (ImageSearch.IndexRevision, PhotoSearchPack.Text(this).Revision, PhotoSearchPack.Faces(this).Revision);

    private bool HasSearchablePhotos => ImageSearch.Enabled && ImageSearch.HasIndex ||
        PhotoSearchPack.Text(this).Searchable || PhotoSearchPack.Faces(this).Searchable;
    private bool SearchingFiles => searching && IsAppsSurface && searchMode == AppSearchMode.Files;

    private void RequestFileSearchAccess()
    {
        HideKeyboard();
        if (LauncherDistribution.IsPlay)
        {
            Toast(Localize(Resource.String.ui_play_storage_scope));
            return;
        }
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
        {
            if (access)
                _ = LoadSearchFilesAsync();
            return;
        }

        fileSearchAccess = access;
        ResetFileSearch(keepScope: true);
        RenderBody();
        if (!fileSearchAccess)
            return;

        _ = LoadSearchFilesAsync();
        FocusSearchInput();
    }

    private void ResetFileSearch(bool keepScope = false)
    {
        fileSearchLoad = null;
        fileSnapshot = null;
        fileSearchFailed = false;
        selectedFileType = null;
        if (!keepScope)
            fileSearchScope = FileSearchScope.All;
        ResetImageQuery();
        ResetDocumentQuery();
        if (documentEncoderUsed)
        {
            documentEncoderUsed = false;
            _ = Task.Run(DocumentSearch.ReleaseEncoder);
        }
        if (imageTextUsed)
        {
            imageTextUsed = false;
            _ = ImageSearch.ReleaseText();
        }
    }

    private async Task LoadSearchFilesAsync()
    {
        if (fileSearchLoad is not null || !fileSearchAccess)
            return;

        var catalog = FileSearchCatalog.For(this);
        var load = new object();
        fileSearchLoad = load;
        if (fileSnapshot is null)
            RefreshAppSearch();
        try
        {
            var saved = await catalog.Cached();
            if (CanApplyFileCatalog(load) && saved is not null)
                ApplyFileSnapshot(saved);

            var updated = await catalog.Refresh();
            if (CanApplyFileCatalog(load))
                ApplyFileSnapshot(updated);
        }
        catch (Exception error) when (error is Java.Lang.RuntimeException or IOException or UnauthorizedAccessException)
        {
            Android.Util.Log.Warn("FileSearch", error.ToString());
            if (ReferenceEquals(fileSearchLoad, load))
                fileSearchFailed = true;
        }
        finally
        {
            if (ReferenceEquals(fileSearchLoad, load))
            {
                fileSearchLoad = null;
                RenderFileMatches(resetScroll: false);
            }
        }
    }

    private bool CanApplyFileCatalog(object load) => ReferenceEquals(fileSearchLoad, load) &&
        !IsDestroyed && SearchingFiles && fileSearchAccess && SharedStorage.CanRead(this);

    private void ApplyFileSnapshot(FileSearchCatalog.Snapshot snapshot)
    {
        fileSearchFailed = false;
        if (ReferenceEquals(fileSnapshot, snapshot))
            return;
        fileSnapshot = snapshot;
        ResetImageQuery();
        ResetDocumentQuery();
        RefreshFileSearch(resetScroll: false);
    }

    private IEnumerable<SearchFile> FileSearchMatches()
    {
        string search = query.Trim();
        if (search.Length == 0 || !fileSearchAccess || fileSnapshot is null)
            return [];
        var terms = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return fileSnapshot.Files.Where(file => terms.All(term => file.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)) &&
            MatchesFileScope(file))
            .OrderByDescending(file => file.Name.StartsWith(search, StringComparison.CurrentCultureIgnoreCase));
    }

    private bool MatchesFileScope(SearchFile file) => fileSearchScope switch
    {
        FileSearchScope.Images => file.IsImage,
        FileSearchScope.Documents => file.IsDocument,
        _ => true
    };

    private void RefreshFileSearch(bool resetScroll = true)
    {
        string text = query.Trim();
        var snapshot = fileSnapshot;
        bool canSearchImages = fileSearchScope != FileSearchScope.Documents && fileSearchAccess &&
            snapshot is not null && HasSearchablePhotos;
        var revision = PhotoSearchRevision;
        if (text != imageQuery || !canSearchImages || revision != imageIndexRevisionSeen)
        {
            imageIndexRevisionSeen = revision;
            ResetImageQuery();
            if (text.Length > 0 && canSearchImages)
            {
                imageQuery = text;
                imageTextUsed = true;
                _ = FindImages(text, snapshot!);
            }
        }
        RefreshDocumentMatches(text, snapshot);
        RenderFileMatches(resetScroll);
    }

    private void RenderFileMatches(bool resetScroll)
    {
        var all = FileSearchMatches().Concat(documentMatches).Concat(imageMatches).DistinctBy(file => file.Path)
            .Select(file => (File: file, Type: FileType(file.Name))).ToArray();
        var types = all.Select(match => match.Type).Distinct(StringComparer.Ordinal)
            .OrderBy(type => type.Length == 0).ThenBy(type => type, StringComparer.Ordinal).ToArray();
        string? activeType = selectedFileType is { } selected && types.Contains(selected, StringComparer.Ordinal) ? selected : null;
        if (activeType is null && imageQueryLoad is null && documentQueryLoad is null)
            selectedFileType = null;
        UpdateFileTypeFilters(types, activeType);
        var matches = all.Where(match => activeType is null || match.Type == activeType)
            .Select(match => new SearchHit(null, null, match.File)).ToArray();
        appSearchAdapter?.Replace(matches);
        if (resetScroll)
            appSearchList?.SetSelection(0);
        if (appSearchTitle is not null)
            appSearchTitle.Text = Localize(Resource.String.ui_files);
        if (appSearchCount is not null)
            appSearchCount.Text = !fileSearchAccess ? Localize(Resource.String.ui_allow_access_to_search_your_devices_shared_storage)
                : fileSnapshot is null ? fileSearchFailed ? Localize(Resource.String.ui_couldnt_read_shared_storage_try_reopening_file_search) : Localize(Resource.String.ui_preparing_file_catalog)
                : fileSnapshot.Files.Length == 0 && fileSearchLoad is null ? Localize(Resource.String.ui_no_files_found_in_shared_storage)
                : string.IsNullOrWhiteSpace(query) ? FileSearchHint()
                : documentQueryLoad is not null ? Localize(Resource.String.ui_value_searching_documents, Counted(matches.Length, Resource.Plurals.file))
                : imageQueryFailed ? Localize(Resource.String.ui_value_image_search_unavailable, Counted(matches.Length, Resource.Plurals.file))
                : documentQueryFailed ? Localize(Resource.String.ui_value_document_search_unavailable, Counted(matches.Length, Resource.Plurals.file))
                : imageQueryLoad is not null ? Localize(Resource.String.ui_value_searching_images, Counted(matches.Length, Resource.Plurals.file))
                : matches.Length == 0 && fileSearchScope != FileSearchScope.Documents && ImageSearch.Enabled && !ImageSearch.HasIndex ? Localize(Resource.String.ui_no_matching_filenames_image_index_pending)
                : matches.Length == 0 && fileSearchLoad is not null ? Localize(Resource.String.ui_no_matches_yet_checking_for_changes)
                : matches.Length == 0 ? Localize(Resource.String.ui_no_matching_files) : Counted(matches.Length, Resource.Plurals.file);
    }

    private string FileSearchHint() => fileSearchScope switch
    {
        FileSearchScope.Images => HasSearchablePhotos ? Localize(Resource.String.ui_search_image_filenames_or_indexed_photos) : Localize(Resource.String.ui_type_an_image_filename_to_search),
        FileSearchScope.Documents => DocumentSearch.Searchable ? Localize(Resource.String.ui_search_document_filenames_or_indexed_passages) : Localize(Resource.String.ui_type_a_document_filename_to_search),
        _ => HasSearchablePhotos || DocumentSearch.Searchable ? Localize(Resource.String.ui_search_filenames_or_indexed_content) : Localize(Resource.String.ui_type_a_filename_to_search)
    };

    private static string FileType(string name)
    {
        string extension = Path.GetExtension(name);
        return extension.Length > 1 && extension.Length < name.Length ? extension[1..].ToUpperInvariant() : "";
    }

    private void AddFileTypeFilters()
    {
        fileTypeRow = Stack(true);
        fileTypeRow.SetPaddingRelative(0, 0, 0, Dp(8));
        fileTypeScroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false, Visibility = ViewStates.Gone };
        fileTypeScroll.AddView(fileTypeRow);
        body.AddView(fileTypeScroll, Match());
        displayedFilePills = [];
        fileTypePills = [];
    }

    private void UpdateFileTypeFilters(string[] types, string? activeType)
    {
        if (fileTypeScroll is null || fileTypeRow is null)
            return;
        fileTypeScroll.Visibility = fileSearchAccess ? ViewStates.Visible : ViewStates.Gone;
        FileSearchPill[] pills = types.Length == 0 ? ScopePills :
            [new(Localize(Resource.String.ui_all), null, null), .. types.Select(type => new FileSearchPill(type.Length == 0 ? Localize(Resource.String.ui_no_extension) : type, null, type))];
        if (!pills.AsSpan().SequenceEqual(displayedFilePills))
        {
            bool changedKind = displayedFilePills.Length == 0 || displayedFilePills[0].Scope.HasValue != pills[0].Scope.HasValue;
            displayedFilePills = pills;
            fileTypeRow.RemoveAllViews();
            fileTypePills = pills.Select(pill =>
            {
                var chip = RootAction(pill.Label, () => SelectFileSearchPill(pill));
                chip.SetSingleLine(true);
                chip.SetPaddingRelative(Dp(16), Dp(6), Dp(16), Dp(6));
                fileTypeRow.AddView(chip, new LinearLayout.LayoutParams(-2, -2) { MarginEnd = Dp(8) });
                return chip;
            }).ToArray();
            if (changedKind)
                fileTypeScroll.ScrollTo(0, 0);
        }
        for (int i = 0; i < fileTypePills.Length; i++)
        {
            var chip = fileTypePills[i];
            var pill = pills[i];
            bool selected = pill.Scope is { } scope ? fileSearchScope == scope : pill.Extension == activeType;
            if (chip.Background is not null && chip.Selected == selected)
                continue;
            chip.Selected = selected;
            chip.SetTextColor(selected ? palette.OnAccent : palette.Text);
            chip.Background = Background(selected ? palette.Accent : Color.Transparent, 24, selected ? null : palette.Divider);
        }
    }

    private void SelectFileSearchPill(FileSearchPill pill)
    {
        if (pill.Scope is { } scope)
        {
            fileSearchScope = scope;
            selectedFileType = null;
            RefreshFileSearch();
        }
        else
        {
            selectedFileType = pill.Extension;
            RenderFileMatches(resetScroll: true);
        }
    }

    private void ResetImageQuery()
    {
        imageQueryLoad?.Cancel();
        imageQueryLoad = null;
        imageQuery = null;
        imageQueryFailed = false;
        imageMatches = [];
    }

    private async Task FindImages(string text, FileSearchCatalog.Snapshot snapshot)
    {
        using var load = new CancellationTokenSource();
        imageQueryLoad = load;
        try
        {
            await Task.Delay(175, load.Token);
            var results = await Task.Run(async () =>
            {
                var candidates = snapshot.Images;
                if (candidates.Count == 0)
                    return Array.Empty<SearchFile>();
                bool phrase = text.Length > 2 && text[0] == '"' && text[^1] == '"';
                var people = phrase ? [] : PhotoSearchPack.Faces(this).Match(text, candidates.ContainsKey, load.Token);
                var ocr = PhotoSearchPack.Text(this).Match(text, candidates.ContainsKey, load.Token);
                string[] visual;
                try { visual = !phrase && text.Length >= 2 ? await ImageSearch.Search(text, candidates.ContainsKey, load.Token) : []; }
                catch (Exception error) when (people.Length + ocr.Length > 0 && IsImageSearchError(error))
                {
                    Android.Util.Log.Warn("ImageSearch", error.ToString());
                    visual = [];
                }
                var paths = people.Concat(ocr).Concat(visual).Distinct(StringComparer.Ordinal);
                return paths.Select(path => candidates[path]).Where(file => File.Exists(file.Path)).ToArray();
            }, load.Token);
            if (ReferenceEquals(imageQueryLoad, load))
                imageMatches = results;
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception error) when (IsImageSearchError(error))
        {
            Android.Util.Log.Warn("ImageSearch", error.ToString());
            if (ReferenceEquals(imageQueryLoad, load))
                imageQueryFailed = true;
        }
        finally
        {
            if (ReferenceEquals(imageQueryLoad, load))
            {
                imageQueryLoad = null;
                if (SearchingFiles && !IsDestroyed && query.Trim() == text)
                    RenderFileMatches(resetScroll: false);
            }
        }
    }

    private static bool IsImageSearchError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or
        Java.Lang.RuntimeException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException or TypeInitializationException or KeyNotFoundException or System.Text.Json.JsonException;

    private void OpenSearchFile(SearchFile file)
    {
        HideKeyboard();
        if (!SharedStorage.CanRead(this) || !File.Exists(file.Path))
        {
            Toast(Localize(Resource.String.ui_this_file_is_no_longer_available));
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
