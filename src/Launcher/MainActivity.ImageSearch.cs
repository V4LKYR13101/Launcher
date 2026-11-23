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

    private void AddImageSearchSetting(LinearLayout parent) => SettingRow(parent, ImageSearchPage,
        "Find photos by description, text or people",
        () => Navigate(ImageSearchPage), "search", "AI semantic files photos OCR faces embedding CLIP model download storage");

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
            SettingRow(AddSettingsCard("Permissions"), "Allow file access", "Use the existing File Search permission", RequestFileSearchAccess, "lock");
        var controls = AddSettingsCard("Photo descriptions");
        if (status.Phase != ImageSearchService.Phase.Removing)
        {
            ToggleRow(controls, "Search photos by description", service.Enabled, enabled =>
            {
                if (enabled && !service.Ready)
                {
                    Confirm("Download image search model?",
                        "OpenCLIP Fast needs about 238 MB. Photos are processed on this device. Use Wi-Fi to avoid mobile data charges.",
                        "Download & enable", () => { service.SetEnabled(true); RenderBody(); });
                    RenderBody();
                    return;
                }
                service.SetEnabled(enabled);
                RenderBody();
            });
        }
        Paragraph("Search in Files using a description such as “dog on a couch”. Model files and the image index are stored on this device.", controls);

        var index = controls;
        imageSearchSummary = Text(ImageSearchDetail(status), 13, color: palette.Muted);
        imageSearchSummary.SetPadding(0, Dp(10), 0, Dp(10));
        index.AddView(imageSearchSummary, Match());
        imageSearchBar = IsWorking(status.Phase) && status.Phase != ImageSearchService.Phase.Removing ? ModelProgressBar(index) : null;
        if (service.Enabled && !IsWorking(status.Phase))
        {
            if (!service.Ready)
                SettingRow(index, "Download model", "OpenCLIP Fast · about 238 MB", () => { _ = service.Prepare(); RenderBody(); }, "down");
            else if (displayedImageAccess)
                SettingRow(index, "Index images now", "New and changed images only · automatic indexing while charging",
                    () => { _ = service.Index(); RenderBody(); }, "refresh");
        }

        if ((service.StorageBytes > 0 || status.Phase == ImageSearchService.Phase.Downloading) && status.Phase != ImageSearchService.Phase.Removing)
            SettingRow(index, "Remove image search data", "Free model and index storage", () => Confirm("Remove image search data?",
                "This turns off image search and removes the downloaded model and index from Launcher.", "Remove data", () => _ = RemoveImageSearchData()), "close");
        AddPhotoPackSettings(PhotoSearchPack.Text(this), people: false);
        AddPhotoPackSettings(PhotoSearchPack.Faces(this), people: true);
        UpdateImageSearchUi();
    }

    private void AddPhotoPackSettings(PhotoSearchPack pack, bool people)
    {
        string feature = people ? "People" : "Text in images";
        string model = people ? "YuNet and EdgeFace" : "PP-OCRv6 small";
        string explanation = people
            ? "Group similar faces on this device. Name a group to find its photos in Files."
            : "Find words in screenshots, receipts and other photos from Files. Use quotes for a phrase.";
        var card = AddSettingsCard(feature);
        ToggleRow(card, people ? "Search by person" : "Search text inside images", pack.Enabled, value =>
        {
            if (value && !pack.Ready)
                Confirm($"Download {feature.ToLowerInvariant()} models?", $"{model} needs {StorageSize(pack.ModelBytes)}. Processing runs on this device.",
                    "Download & enable", () => { pack.SetEnabled(true); RenderBody(); });
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
            SettingRow(card, "Cancel download", "Keep completed model files", pack.CancelDownload, "close");
        else if (pack.Enabled && !busy)
        {
            if (!pack.Ready)
                SettingRow(card, "Download model files", model, () => { pack.StartDownload(); RenderBody(); }, "down");
            else if (displayedImageAccess)
                SettingRow(card, "Index images now", "Process new and changed images", () => { _ = pack.Index(); RenderBody(); }, "refresh");
        }
        if (people && pack.Enabled && pack.Ready && displayedImageAccess)
            SettingRow(card, "Name people", "Browse and name detected face groups", () => { peopleOffset = 0; Navigate("People"); }, "search");
        if (pack.StorageBytes > 0 || pack.Downloading)
            SettingRow(card, "Remove model and index", people ? "Remove face groups and names from Launcher" : "Free text model and index storage",
                () => Confirm($"Remove {feature.ToLowerInvariant()} data?", people
                    ? "This removes the face model, face groups and saved names from Launcher."
                    : "This removes the text model and recognized text index from Launcher.",
                    "Remove data", () => _ = RemovePhotoPack(pack)), "close");
        photoPackControls.Add((pack, summary, bar, busy, pack.Ready, pack.Downloading));
    }

    private static string PhotoPackStatus(PhotoSearchPack pack)
    {
        var status = pack.Status;
        string detail = status.Error ?? status.Activity ?? (pack.Ready
            ? $"{status.Indexed:N0} images processed"
            : $"Model download required · {StorageSize(pack.ModelBytes)}");
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
            ImageSearchService.Phase.Downloading => $"Downloading OpenCLIP Fast · {status.Downloaded * 100 / ImageSearchModels.TotalBytes}%",
            ImageSearchService.Phase.Scanning => "Looking for new and changed images…",
            ImageSearchService.Phase.Indexing => $"Indexing images · {status.Counts.Processed:N0} of {status.Counts.Total:N0} processed",
            ImageSearchService.Phase.Removing => "Removing model and image index…",
            _ when !ImageSearch.Ready => "Model download required · about 238 MB",
            _ when !ImageSearch.HasIndex => "Model ready · waiting to index images",
            _ when status.Counts.Processed < status.Counts.Total => $"{status.Counts.Processed:N0} of {status.Counts.Total:N0} processed · indexing resumes while charging",
            _ => $"{Math.Max(0, status.Counts.Processed - status.Counts.Skipped):N0} images indexed"
        };
        if (status.Counts.Skipped > 0)
            detail += $" · {status.Counts.Skipped:N0} unreadable";
        return $"{status.Error ?? detail}\n{storage}";
    }

    private static string StorageSize(long bytes) => bytes >= 1_000_000 ? $"{bytes / 1_000_000d:0.#} MB" : $"{bytes / 1_000d:0.#} KB";
}
