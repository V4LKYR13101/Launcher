using Android.Widget;
using LauncherApp.Services;
using LauncherApp.Services.ImageSearch;

namespace LauncherApp;

public partial class MainActivity
{
    private const string ImageSearchPage = "Image search";
    private ImageSearchService ImageSearch => ImageSearchService.For(this);
    private TextView? imageSearchSummary;
    private ProgressBar? imageSearchBar;
    private readonly List<(PhotoSearchPack Pack, TextView Text, ProgressBar? Bar, bool Busy, bool Ready, bool Downloading)> photoPackControls = [];
    private ImageSearchService.Phase displayedImagePhase;
    private bool displayedImageReady, displayedImageAccess;
    private string? displayedImageError;

    private void AddImageSearchSetting(LinearLayout parent) => SettingRow(parent, PageLabel(ImageSearchPage),
        Localize(Resource.String.ui_find_photos_by_description_text_or_people),
        () => Navigate(ImageSearchPage), "search", Localize(Resource.String.ui_ai_semantic_files_photos_ocr_faces_embedding_clip_model));

    private void ResumeImageSearch()
    {
        ImageSearch.Resume();
        PhotoSearchPack.Text(this).Resume();
        PhotoSearchPack.Faces(this).Resume();
        DocumentSearch.Resume();
        UpdateImageSearchUi();
    }

    private void UpdateImageSearchUi()
    {
        if (SearchingFiles && fileSearchAccess && imageIndexRevisionSeen != PhotoSearchRevision)
            RefreshAppSearch();
        if (page == "People" && !peopleLoading && displayedPeopleRevision != PhotoSearchPack.Faces(this).Revision)
            RenderBody();
        if (page != ImageSearchPage || searching || imageSearchSummary is null)
            return;
        var status = ImageSearch.Status;
        if (photoPackControls.Any(control => control.Busy != (control.Pack.Status.Activity is not null) ||
            control.Ready != control.Pack.Ready || control.Downloading != control.Pack.Downloading))
        {
            RenderBody();
            return;
        }
        if (IsWorking(displayedImagePhase) != IsWorking(status.Phase) ||
            status.Phase == ImageSearchService.Phase.Removing && displayedImagePhase != status.Phase ||
            displayedImageReady != ImageSearch.Ready || displayedImageAccess != SharedStorage.CanRead(this) ||
            displayedImageError != status.Error)
        {
            RenderBody();
            return;
        }
        displayedImagePhase = status.Phase;
        string detail = ImageSearchDetail(status);
        if (imageSearchSummary.Text != detail)
            imageSearchSummary.Text = detail;
        if (imageSearchBar is not null)
            UpdateModelProgress(imageSearchBar, status.Phase == ImageSearchService.Phase.Downloading
                ? (int)(status.Downloaded * 100 / ImageSearchModels.TotalBytes)
                : status.Counts.Total > 0 && status.Phase == ImageSearchService.Phase.Indexing
                    ? (int)(status.Counts.Processed * 100L / status.Counts.Total) : null);
        foreach (var control in photoPackControls)
        {
            string label = PhotoPackStatus(control.Pack);
            if (control.Text.Text != label)
                control.Text.Text = label;
            if (control.Bar is not null)
                UpdateModelProgress(control.Bar, control.Pack.Status.Percentage);
        }
    }

    private static bool IsWorking(ImageSearchService.Phase phase) => phase != ImageSearchService.Phase.Idle;

    private void RenderImageSearchSettings()
    {
        var service = ImageSearch;
        var status = service.Status;
        photoPackControls.Clear();
        displayedImagePhase = status.Phase;
        displayedImageReady = service.Ready;
        displayedImageAccess = SharedStorage.CanRead(this);
        displayedImageError = status.Error;

        if (!displayedImageAccess)
            SettingRow(AddSettingsCard(Localize(Resource.String.ui_permissions)), Localize(Resource.String.ui_allow_file_access), Localize(Resource.String.ui_use_the_existing_file_search_permission), RequestFileSearchAccess, "lock");
        var controls = AddSettingsCard(Localize(Resource.String.ui_photo_descriptions));
        if (status.Phase != ImageSearchService.Phase.Removing)
        {
            ToggleRow(controls, Localize(Resource.String.ui_search_photos_by_description), service.Enabled, enabled =>
            {
                if (enabled && !service.Ready)
                {
                    Confirm(Localize(Resource.String.ui_download_image_search_model),
                        Localize(Resource.String.ui_openclip_fast_needs_about_238_mb_photos_are_processed),
                        Localize(Resource.String.ui_download_enable), () => { service.SetEnabled(true); RenderBody(); });
                    RenderBody();
                    return;
                }
                service.SetEnabled(enabled);
                RenderBody();
            });
        }
        Paragraph(Localize(Resource.String.ui_search_in_files_using_a_description_such_as_dog), controls);

        var index = controls;
        imageSearchSummary = Text(ImageSearchDetail(status), 13, color: palette.Muted);
        imageSearchSummary.SetPadding(0, Dp(10), 0, Dp(10));
        index.AddView(imageSearchSummary, Match());
        imageSearchBar = IsWorking(status.Phase) && status.Phase != ImageSearchService.Phase.Removing ? ModelProgressBar(index) : null;
        if (service.Enabled && !IsWorking(status.Phase))
        {
            if (!service.Ready)
                SettingRow(index, Localize(Resource.String.ui_download_model), Localize(Resource.String.ui_openclip_fast_about_238_mb), () => { _ = service.Prepare(); RenderBody(); }, "down");
            else if (displayedImageAccess)
                SettingRow(index, Localize(Resource.String.ui_index_images_now), Localize(Resource.String.ui_new_and_changed_images_only_automatic_indexing_while_charging),
                    () => { _ = service.Index(); RenderBody(); }, "refresh");
        }

        if ((service.StorageBytes > 0 || status.Phase == ImageSearchService.Phase.Downloading) && status.Phase != ImageSearchService.Phase.Removing)
            SettingRow(index, Localize(Resource.String.ui_remove_image_search_data), Localize(Resource.String.ui_free_model_and_index_storage), () => Confirm(Localize(Resource.String.ui_remove_image_search_data_2),
                Localize(Resource.String.ui_this_turns_off_image_search_and_removes_the_downloaded), Localize(Resource.String.ui_remove_data), () => _ = RemoveImageSearchData()), "close");
        AddPhotoPackSettings(PhotoSearchPack.Text(this), people: false);
        AddPhotoPackSettings(PhotoSearchPack.Faces(this), people: true);
        UpdateImageSearchUi();
    }

    private void AddPhotoPackSettings(PhotoSearchPack pack, bool people)
    {
        string feature = people ? "People" : "Text in images";
        string model = people ? "YuNet and EdgeFace" : "PP-OCRv6 small";
        string explanation = people
            ? Localize(Resource.String.ui_group_similar_faces_on_this_device_name_a_group)
            : Localize(Resource.String.ui_find_words_in_screenshots_receipts_and_other_photos_from);
        var card = AddSettingsCard(feature);
        ToggleRow(card, people ? Localize(Resource.String.ui_search_by_person) : Localize(Resource.String.ui_search_text_inside_images), pack.Enabled, value =>
        {
            if (value && !pack.Ready)
                Confirm(Localize(Resource.String.ui_download_value_models, feature.ToLowerInvariant()), Localize(Resource.String.ui_value_needs_value_processing_runs_on_this_device, model, StorageSize(pack.ModelBytes)),
                    Localize(Resource.String.ui_download_enable), () => { pack.SetEnabled(true); RenderBody(); });
            else
                pack.SetEnabled(value);
            RenderBody();
        });
        Paragraph(explanation, card);
        var status = pack.Status;
        var summary = Text(PhotoPackStatus(pack), 13, color: palette.Muted);
        summary.SetPadding(0, Dp(8), 0, Dp(8));
        card.AddView(summary, Match());
        bool busy = status.Activity is not null;
        ProgressBar? bar = busy ? ModelProgressBar(card) : null;
        if (pack.Downloading)
            SettingRow(card, Localize(Resource.String.ui_cancel_download), Localize(Resource.String.ui_keep_completed_model_files), pack.CancelDownload, "close");
        else if (pack.Enabled && !busy)
        {
            if (!pack.Ready)
                SettingRow(card, Localize(Resource.String.ui_download_model_files), model, () => { pack.StartDownload(); RenderBody(); }, "down");
            else if (displayedImageAccess)
                SettingRow(card, Localize(Resource.String.ui_index_images_now), Localize(Resource.String.ui_process_new_and_changed_images), () => { _ = pack.Index(); RenderBody(); }, "refresh");
        }
        if (people && pack.Enabled && pack.Ready && displayedImageAccess)
            SettingRow(card, Localize(Resource.String.ui_name_people), Localize(Resource.String.ui_browse_and_name_detected_face_groups), () => { peopleOffset = 0; Navigate("People"); }, "search");
        if (pack.StorageBytes > 0 || pack.Downloading)
            SettingRow(card, Localize(Resource.String.ui_remove_model_and_index), people ? Localize(Resource.String.ui_remove_face_groups_and_names_from_launcher) : Localize(Resource.String.ui_free_text_model_and_index_storage),
                () => Confirm(Localize(Resource.String.ui_remove_value_data, feature.ToLowerInvariant()), people
                    ? Localize(Resource.String.ui_this_removes_the_face_model_face_groups_and_saved)
                    : Localize(Resource.String.ui_this_removes_the_text_model_and_recognized_text_index),
                    Localize(Resource.String.ui_remove_data), () => _ = RemovePhotoPack(pack)), "close");
        photoPackControls.Add((pack, summary, bar, busy, pack.Ready, pack.Downloading));
    }

    private static string PhotoPackStatus(PhotoSearchPack pack)
    {
        var status = pack.Status;
        string detail = status.Error ?? status.Activity ?? (pack.Ready
            ? Localize(Resource.String.ui_value_images_processed, status.Indexed)
            : Localize(Resource.String.ui_model_download_required_value, StorageSize(pack.ModelBytes)));
        if (pack.Downloading && status.Percentage is { } percent)
            detail += $" · {percent}%";
        return $"{detail}\n{StorageSize(pack.StorageBytes)} stored on device";
    }

    private async Task RemovePhotoPack(PhotoSearchPack pack)
    {
        await pack.Remove();
        if (!IsDestroyed && page == ImageSearchPage)
            RenderBody();
    }

    private async Task RemoveImageSearchData()
    {
        await ImageSearch.Remove();
        if (!IsDestroyed && page == ImageSearchPage)
            RenderBody();
    }

    private string ImageSearchDetail(ImageSearchService.Progress status)
    {
        string storage = $"{StorageSize(ImageSearch.StorageBytes)} stored on device";
        string detail = status.Phase switch
        {
            ImageSearchService.Phase.Downloading => Localize(Resource.String.ui_downloading_openclip_fast_value, status.Downloaded * 100 / ImageSearchModels.TotalBytes),
            ImageSearchService.Phase.Scanning => Localize(Resource.String.ui_looking_for_new_and_changed_images),
            ImageSearchService.Phase.Indexing => Localize(Resource.String.ui_indexing_images_value_of_value_processed, status.Counts.Processed, status.Counts.Total),
            ImageSearchService.Phase.Removing => Localize(Resource.String.ui_removing_model_and_image_index),
            _ when !ImageSearch.Ready => Localize(Resource.String.ui_model_download_required_about_238_mb),
            _ when !ImageSearch.HasIndex => Localize(Resource.String.ui_model_ready_waiting_to_index_images),
            _ when status.Counts.Processed < status.Counts.Total => Localize(Resource.String.ui_value_of_value_processed_indexing_resumes_while_charging, status.Counts.Processed, status.Counts.Total),
            _ => Localize(Resource.String.ui_value_images_indexed, Math.Max(0, status.Counts.Processed - status.Counts.Skipped))
        };
        if (status.Counts.Skipped > 0)
            detail += $" · {status.Counts.Skipped:N0} unreadable";
        return $"{status.Error ?? detail}\n{storage}";
    }

    private static string StorageSize(long bytes) => bytes >= 1_000_000 ? Localize(Resource.String.ui_value_mb_upper, bytes / 1_000_000d) : Localize(Resource.String.ui_value_kb_upper, bytes / 1_000d);
}
