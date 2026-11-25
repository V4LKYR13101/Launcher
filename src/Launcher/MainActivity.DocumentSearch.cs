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

    private void AddSearchSettingsLink(LinearLayout parent) => SettingRow(parent, PageLabel(SearchSettingsPage),
        Localize(Resource.String.ui_files_image_search_and_document_search), () => Navigate(SearchSettingsPage), "search");

    private void RenderSearchSettings()
    {
        var sources = AddSettingsCard();
        AddImageSearchSetting(sources);
        AddDocumentSearchSetting(sources);
        displayedSearchAccess = SharedStorage.CanRead(this);
        if (!displayedSearchAccess)
            SettingRow(sources, Localize(Resource.String.ui_allow_file_access), Localize(Resource.String.ui_search_shared_storage_on_this_device), RequestFileSearchAccess, "lock");
    }

    private void AddDocumentSearchSetting(LinearLayout parent) => SettingRow(parent, PageLabel(DocumentSearchPage),
        DocumentSearch.Enabled ? Localize(Resource.String.ui_on_model_indexing_and_storage) : Localize(Resource.String.ui_off_find_files_by_what_they_say),
        () => Navigate(DocumentSearchPage), "file", Localize(Resource.String.ui_documents_semantic_embeddings_embeddinggemma_files_pdf_docx_txt_model));

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
            ToggleRow(panel, Localize(Resource.String.ui_search_documents_by_meaning), service.Enabled, enabled =>
            {
                if (enabled && !service.Ready)
                    Confirm(Localize(Resource.String.ui_download_the_document_search_model),
                        Localize(Resource.String.ui_embeddinggemma_2_needs_about_165_mb_launcher_reads_and),
                        Localize(Resource.String.ui_download_enable), () => { service.SetEnabled(true); RenderBody(); });
                else
                    service.SetEnabled(enabled);
                RenderBody();
            });
        }
        Paragraph(Localize(Resource.String.ui_search_in_files_using_a_description_such_as_the), panel);
        var index = AddSettingsCard(Localize(Resource.String.ui_model_and_index));
        documentSearchStatus = Text(DocumentSearchDetail(status), 13, color: palette.Muted);
        documentSearchStatus.SetPadding(0, Dp(8), 0, Dp(8));
        index.AddView(documentSearchStatus, Match());
        documentSearchBar = status.Phase is DocumentSearchService.Phase.Downloading or DocumentSearchService.Phase.Scanning or
            DocumentSearchService.Phase.Indexing ? ModelProgressBar(index) : null;
        if (service.Enabled && !displayedDocumentAccess)
            SettingRow(index, Localize(Resource.String.ui_allow_file_access), Localize(Resource.String.ui_use_the_existing_files_permission), RequestFileSearchAccess, "lock");
        if (status.Phase == DocumentSearchService.Phase.Downloading)
            SettingRow(index, Localize(Resource.String.ui_cancel_download), Localize(Resource.String.ui_stop_downloading_the_model), service.Cancel, "close");
        if (service.Enabled && status.Phase == DocumentSearchService.Phase.Idle)
        {
            if (!service.Ready)
                SettingRow(index, Localize(Resource.String.ui_download_model), Localize(Resource.String.ui_embeddinggemma_2_about_165_mb), () => { _ = service.Download(); RenderBody(); }, "down");
            else if (displayedDocumentAccess)
                SettingRow(index, Localize(Resource.String.ui_index_documents_now), Localize(Resource.String.ui_process_new_and_changed_files_automatic_indexing_while_charging),
                    () => { _ = service.Index(); RenderBody(); }, "refresh");
        }
        if ((service.StorageBytes > 0 || status.Phase == DocumentSearchService.Phase.Downloading) &&
            status.Phase != DocumentSearchService.Phase.Removing)
        {
            SettingRow(index, Localize(Resource.String.ui_remove_model_and_index), Localize(Resource.String.ui_free_document_search_storage), () =>
                Confirm(Localize(Resource.String.ui_remove_document_search_data), Localize(Resource.String.ui_this_turns_off_document_search_and_removes_its_downloaded),
                    Localize(Resource.String.ui_remove_data), () => _ = RemoveDocumentSearch()), "close");
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
            DocumentSearchService.Phase.Downloading => Localize(Resource.String.ui_downloading_embeddinggemma_2_value, status.Downloaded * 100 / DocumentSearchService.DownloadBytes),
            DocumentSearchService.Phase.Scanning => Localize(Resource.String.ui_looking_for_new_and_changed_documents),
            DocumentSearchService.Phase.Indexing => Localize(Resource.String.ui_indexing_documents_value_of_value, status.Done, status.Total),
            DocumentSearchService.Phase.Removing => Localize(Resource.String.ui_removing_model_and_index),
            _ when !DocumentSearch.Ready => Localize(Resource.String.ui_model_download_required_about_165_mb),
            _ when status.Counts.Indexed == 0 && status.Counts.Skipped == 0 => Localize(Resource.String.ui_model_ready_waiting_to_index_documents),
            _ => Localize(Resource.String.ui_value_indexed_value, Counted(status.Counts.Indexed, Resource.Plurals.document), Counted(status.Counts.Passages, Resource.Plurals.passage)) +
                (status.Counts.Skipped > 0 ? Localize(Resource.String.ui_value_without_readable_text, status.Counts.Skipped) : "")
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
