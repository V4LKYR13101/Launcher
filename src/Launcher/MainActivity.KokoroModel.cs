using Android.Widget;
using LauncherApp.Services.Speech;

namespace LauncherApp;

public partial class MainActivity
{
    private const string SpeechSettingsPage = "Speech & audio";
    private static CancellationTokenSource? kokoroDownload;
    private static ModelInstallProgress kokoroProgress = new("");
    private static bool kokoroRemoving;
    private static string? kokoroMessage;
    private Action? afterKokoroInstall;
    private bool returnBeforeSpeech;
    private TextView? kokoroStatus;
    private ProgressBar? kokoroBar;
    private bool displayedKokoroBusy;
    private long kokoroBytes;

    internal void OpenSpeechSettings(Action? continuation = null)
    {
        returnBeforeSpeech = page != SpeechSettingsPage;
        afterKokoroInstall = continuation;
        Navigate(SpeechSettingsPage);
    }

    private ProgressBar ModelProgressBar(LinearLayout parent)
    {
        var bar = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleHorizontal)
        {
            Max = 100,
            ProgressTintList = AccentTint(),
            IndeterminateTintList = AccentTint()
        };
        parent.AddView(bar, Match());
        return bar;
    }

    private static void UpdateModelProgress(ProgressBar bar, int? percentage)
    {
        bar.Indeterminate = !percentage.HasValue;
        if (percentage.HasValue)
            bar.Progress = Math.Clamp(percentage.Value, 0, 100);
    }

    private void RenderSpeechSettings()
    {
        AddSpeechActions(AddSettingsCard("Playback"));
        var panel = AddSettingsCard("Kokoro model files");
        bool installed = KokoroModelInstaller.IsInstalled(this);
        displayedKokoroBusy = kokoroDownload is not null || kokoroRemoving;
        kokoroStatus = Text("", 13, color: palette.Muted);
        kokoroStatus.SetPadding(0, Dp(10), 0, Dp(10));
        panel.AddView(kokoroStatus, Match());
        kokoroBar = null;
        if (kokoroDownload is not null)
        {
            kokoroBar = ModelProgressBar(panel);
            SettingRow(panel, "Cancel download", "Stop this installation", () => kokoroDownload?.Cancel(), "close");
        }
        else if (!kokoroRemoving)
        {
            if (installed)
            {
                if (afterKokoroInstall is not null)
                    SettingRow(panel, "Continue to speech", "Use the installed voice model", () =>
                    {
                        var next = afterKokoroInstall;
                        afterKokoroInstall = null;
                        if (returnBeforeSpeech)
                            ReturnFromSettings();
                        else
                            RenderBody();
                        next?.Invoke();
                    }, "play");
                SettingRow(panel, "Remove model files", "Free offline speech storage", () => Confirm("Remove Kokoro model?",
                    "Download it again to use Kokoro offline speech.", "Remove model", () => _ = RemoveKokoroModel()), "close");
            }
            else
                SettingRow(panel, "Download model", "Kokoro multilingual · about 350 MB", () => _ = DownloadKokoroModel(), "down");
        }
        Paragraph("Kokoro reads articles, notifications and other text aloud on this device.", panel);
        kokoroBytes = 0;
        if (installed)
            _ = ReadKokoroStorage();
        UpdateKokoroModelUi();
    }

    private async Task ReadKokoroStorage()
    {
        string[] paths = KokoroModelPaths.InstallLocations(this).Distinct(StringComparer.Ordinal).ToArray();
        kokoroBytes = await Task.Run(() => paths.Sum(path =>
        {
            try { return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length); }
            catch (IOException) { return 0; }
            catch (UnauthorizedAccessException) { return 0; }
        }));
        UpdateKokoroModelUi();
    }

    private void UpdateKokoroModelUi()
    {
        if (IsDestroyed || page != SpeechSettingsPage || kokoroStatus is null)
            return;
        if (displayedKokoroBusy != (kokoroDownload is not null || kokoroRemoving))
        {
            RenderBody();
            return;
        }
        string detail = kokoroRemoving ? "Removing model files…" : kokoroDownload is not null
            ? kokoroProgress.Message
            : kokoroMessage ?? (KokoroModelInstaller.IsInstalled(this) ? "Model ready · offline speech available" : "Model download required · about 350 MB");
        string statusText = kokoroBytes > 0 ? $"{detail}\n{StorageSize(kokoroBytes)} stored on device" : detail;
        if (kokoroStatus.Text != statusText)
            kokoroStatus.Text = statusText;
        if (kokoroBar is not null)
            UpdateModelProgress(kokoroBar, kokoroProgress.Percentage);
    }

    private async Task DownloadKokoroModel()
    {
        if (kokoroDownload is not null || kokoroRemoving)
            return;
        using var download = new CancellationTokenSource();
        kokoroDownload = download;
        kokoroProgress = new("Preparing model download…");
        kokoroMessage = null;
        RenderBody();
        var progress = new Progress<ModelInstallProgress>(update =>
        {
            if (kokoroDownload == download)
            {
                kokoroProgress = update;
                UpdateKokoroModelUi();
            }
        });
        try { await Task.Run(() => KokoroModelInstaller.InstallAsync(ApplicationContext!, progress, download.Token)); }
        catch (OperationCanceledException) when (download.IsCancellationRequested) { kokoroMessage = "Download cancelled."; }
        catch (Exception error)
        {
            Android.Util.Log.Warn("LauncherKokoro", error.ToString());
            kokoroMessage = "Installation failed. " + error.Message;
        }
        finally
        {
            kokoroDownload = null;
            UpdateKokoroModelUi();
        }
    }

    private async Task RemoveKokoroModel()
    {
        if (kokoroRemoving || kokoroDownload is not null)
            return;
        kokoroRemoving = true;
        kokoroMessage = null;
        RenderBody();
        try { await Task.Run(() => KokoroModelInstaller.RemoveAsync(ApplicationContext!)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            kokoroMessage = "Couldn't remove the model files. Try again.";
        }
        finally
        {
            kokoroRemoving = false;
            UpdateKokoroModelUi();
        }
    }
}
