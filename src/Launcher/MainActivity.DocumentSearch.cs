using Android.Widget;
using LauncherApp.Services;
using LauncherApp.Services.DocumentSearch;

namespace LauncherApp;

public partial class MainActivity
{
    private const string SearchSettingsPage = "Search";
    private const string DocumentSearchPage = "Document search";
    private DocumentSearchService DocumentSearch => DocumentSearchService.For(this);
    private TextView? documentSearchStatus;
    private ProgressBar? documentSearchBar;
    private DocumentSearchService.Phase displayedDocumentPhase;
    private bool displayedDocumentReady, displayedDocumentAccess, displayedSearchAccess;
    private CancellationTokenSource? documentQueryLoad;
    private string? documentQuery;
    private int documentRevisionSeen;
    private SearchFile[] documentMatches = [];
    private bool documentQueryFailed, documentEncoderUsed;

    private void AddSearchSettingsLink(LinearLayout parent) => SettingRow(parent, SearchSettingsPage,
        "Files, image search and document search", () => Navigate(SearchSettingsPage), "search");

    private void RenderSearchSettings()
    {
        var sources = AddSettingsCard();
        AddImageSearchSetting(sources);
        AddDocumentSearchSetting(sources);
        displayedSearchAccess = SharedStorage.CanRead(this);
        if (!displayedSearchAccess)
            SettingRow(sources, "Allow file access", "Search shared storage on this device", RequestFileSearchAccess, "lock");
    }

    private void AddDocumentSearchSetting(LinearLayout parent) => SettingRow(parent, DocumentSearchPage,
        DocumentSearch.Enabled ? "On · model, indexing and storage" : "Off · find files by what they say",
        () => Navigate(DocumentSearchPage), "file", "documents semantic embeddings EmbeddingGemma files PDF DOCX TXT model");

    private void RenderDocumentSearchSettings()
    {
        var service = DocumentSearch;
        var status = service.Status;
        displayedDocumentPhase = status.Phase;
        displayedDocumentReady = service.Ready;
        displayedDocumentAccess = SharedStorage.CanRead(this);
        var panel = AddSettingsCard();
        if (status.Phase != DocumentSearchService.Phase.Removing)
        {
            ToggleRow(panel, "Search documents by meaning", service.Enabled, enabled =>
            {
                if (enabled && !service.Ready)
                    Confirm("Download the document search model?",
                        "EmbeddingGemma 2 needs about 165 MB. Launcher reads and indexes passages on this device.",
                        "Download & enable", () => { service.SetEnabled(true); RenderBody(); });
                else
                    service.SetEnabled(enabled);
                RenderBody();
            });
        }
        Paragraph("Search in Files using a description such as “the lease clause about pets”. Supports PDF files with selectable text, DOCX, TXT, Markdown and CSV.", panel);
        var index = AddSettingsCard("Model and index");
        documentSearchStatus = Text(DocumentSearchDetail(status), 13, color: palette.Muted);
        documentSearchStatus.SetPadding(0, Dp(8), 0, Dp(8));
        index.AddView(documentSearchStatus, Match());
        documentSearchBar = status.Phase is DocumentSearchService.Phase.Downloading or DocumentSearchService.Phase.Scanning or
            DocumentSearchService.Phase.Indexing ? ModelProgressBar(index) : null;
        if (service.Enabled && !displayedDocumentAccess)
            SettingRow(index, "Allow file access", "Use the existing Files permission", RequestFileSearchAccess, "lock");
        if (status.Phase == DocumentSearchService.Phase.Downloading)
            SettingRow(index, "Cancel download", "Stop downloading the model", service.Cancel, "close");
        if (service.Enabled && status.Phase == DocumentSearchService.Phase.Idle)
        {
            if (!service.Ready)
                SettingRow(index, "Download model", "EmbeddingGemma 2 · about 165 MB", () => { _ = service.Download(); RenderBody(); }, "down");
            else if (displayedDocumentAccess)
                SettingRow(index, "Index documents now", "Process new and changed files · automatic indexing while charging",
                    () => { _ = service.Index(); RenderBody(); }, "refresh");
        }
        if ((service.StorageBytes > 0 || status.Phase == DocumentSearchService.Phase.Downloading) &&
            status.Phase != DocumentSearchService.Phase.Removing)
        {
            SettingRow(index, "Remove model and index", "Free document search storage", () =>
                Confirm("Remove document search data?", "This turns off document search and removes its downloaded model and passage index.",
                    "Remove data", () => _ = RemoveDocumentSearch()), "close");
        }
        UpdateDocumentSearchUi();
    }

    private async Task RemoveDocumentSearch()
    {
        var removal = DocumentSearch.Remove();
        UpdateDocumentSearchUi();
        await removal;
        if (!IsDestroyed && page == DocumentSearchPage)
            RenderBody();
    }

    private void UpdateDocumentSearchUi()
    {
        if (SearchingFiles && fileSearchAccess && documentRevisionSeen != DocumentSearch.Revision)
            RefreshAppSearch();
        if (page == SearchSettingsPage && !searching && displayedSearchAccess != SharedStorage.CanRead(this))
        {
            RenderBody();
            return;
        }
        if (page != DocumentSearchPage || searching || documentSearchStatus is null)
            return;
        var status = DocumentSearch.Status;
        if (displayedDocumentPhase != status.Phase || displayedDocumentReady != DocumentSearch.Ready ||
            displayedDocumentAccess != SharedStorage.CanRead(this))
        {
            RenderBody();
            return;
        }
        string detail = DocumentSearchDetail(status);
        if (documentSearchStatus.Text != detail)
            documentSearchStatus.Text = detail;
        if (documentSearchBar is not null)
            UpdateModelProgress(documentSearchBar, status.Phase switch
            {
                DocumentSearchService.Phase.Downloading => (int)(status.Downloaded * 100 / DocumentSearchService.DownloadBytes),
                DocumentSearchService.Phase.Indexing when status.Total > 0 => (int)(status.Done * 100L / status.Total),
                _ => null
            });
    }

    private string DocumentSearchDetail(DocumentSearchService.Progress status)
    {
        string detail = status.Error ?? (status.Phase switch
        {
            DocumentSearchService.Phase.Downloading => $"Downloading EmbeddingGemma 2 · {status.Downloaded * 100 / DocumentSearchService.DownloadBytes}%",
            DocumentSearchService.Phase.Scanning => "Looking for new and changed documents…",
            DocumentSearchService.Phase.Indexing => $"Indexing documents · {status.Done:N0} of {status.Total:N0}",
            DocumentSearchService.Phase.Removing => "Removing model and index…",
            _ when !DocumentSearch.Ready => "Model download required · about 165 MB",
            _ when status.Counts.Indexed == 0 && status.Counts.Skipped == 0 => "Model ready · waiting to index documents",
            _ => $"{Counted(status.Counts.Indexed, "document")} indexed · {Counted(status.Counts.Passages, "passage")}" +
                (status.Counts.Skipped > 0 ? $" · {status.Counts.Skipped:N0} without readable text" : "")
        });
        string storage = $"{StorageSize(DocumentSearch.StorageBytes)} stored on device";
        if (status.Counts.Passages > 0)
            storage += $" · {StorageSize(status.Counts.Passages * DocumentIndex.Dimensions * 2L)} in passage vectors";
        return $"{detail}\n{storage}";
    }

    private void RefreshDocumentMatches(string text, FileSearchCatalog.Snapshot? snapshot)
    {
        int revision = DocumentSearch.Revision;
        bool canSearch = fileSearchScope != FileSearchScope.Images && fileSearchAccess && snapshot is not null &&
            DocumentSearch.Searchable && text.Length >= 2 &&
            !(text.Length > 2 && text[0] == '"' && text[^1] == '"');
        if (documentRevisionSeen == revision && (canSearch ? text == documentQuery : documentQuery is null))
            return;

        documentRevisionSeen = revision;
        ResetDocumentQuery();
        if (canSearch)
        {
            documentQuery = text;
            documentEncoderUsed = true;
            _ = FindDocuments(text, snapshot!);
        }
    }

    private void ResetDocumentQuery()
    {
        documentQueryLoad?.Cancel();
        documentQueryLoad = null;
        documentQuery = null;
        documentMatches = [];
        documentQueryFailed = false;
    }

    private async Task FindDocuments(string text, FileSearchCatalog.Snapshot snapshot)
    {
        using var load = new CancellationTokenSource();
        documentQueryLoad = load;
        try
        {
            await Task.Delay(450, load.Token);
            var results = await Task.Run(async () =>
            {
                var candidates = snapshot.Documents;
                if (candidates.Count == 0)
                    return Array.Empty<SearchFile>();
                var matches = await DocumentSearch.Search(text, candidates.ContainsKey, load.Token);
                return matches.Select(path => candidates[path]).ToArray();
            }, load.Token);
            if (ReferenceEquals(documentQueryLoad, load))
                documentMatches = results;
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception error) when (DocumentSearchService.IsSearchError(error))
        {
            Android.Util.Log.Warn("DocumentSearch", error.ToString());
            if (ReferenceEquals(documentQueryLoad, load))
                documentQueryFailed = true;
        }
        finally
        {
            if (ReferenceEquals(documentQueryLoad, load))
            {
                documentQueryLoad = null;
                if (SearchingFiles && !IsDestroyed && query.Trim() == text)
                    RenderFileMatches(resetScroll: false);
            }
        }
    }
}
