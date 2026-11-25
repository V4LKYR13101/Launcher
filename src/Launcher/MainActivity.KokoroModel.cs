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
        AddSpeechActions(AddSettingsCard(Localize(Resource.String.ui_playback)));
        var panel = AddSettingsCard(Localize(Resource.String.ui_kokoro_model_files));
        bool installed = KokoroModelInstaller.IsInstalled(this);
        displayedKokoroBusy = kokoroDownload is not null || kokoroRemoving;
        kokoroStatus = Text("", 13, color: palette.Muted);
        kokoroStatus.SetPadding(0, Dp(10), 0, Dp(10));
        panel.AddView(kokoroStatus, Match());
        kokoroBar = null;
        if (kokoroDownload is not null)
        {
            kokoroBar = ModelProgressBar(panel);
            SettingRow(panel, Localize(Resource.String.ui_cancel_download), Localize(Resource.String.ui_stop_this_installation), () => kokoroDownload?.Cancel(), "close");
        }
        else if (!kokoroRemoving)
        {
            if (installed)
            {
                if (afterKokoroInstall is not null)
                    SettingRow(panel, Localize(Resource.String.ui_continue_to_speech), Localize(Resource.String.ui_use_the_installed_voice_model), () =>
                    {
                        var next = afterKokoroInstall;
                        afterKokoroInstall = null;
                        if (returnBeforeSpeech)
                            ReturnFromSettings();
                        else
                            RenderBody();
                        next?.Invoke();
                    }, "play");
                SettingRow(panel, Localize(Resource.String.ui_remove_model_files), Localize(Resource.String.ui_free_offline_speech_storage), () => Confirm(Localize(Resource.String.ui_remove_kokoro_model),
                    Localize(Resource.String.ui_download_it_again_to_use_kokoro_offline_speech), Localize(Resource.String.ui_remove_model), () => _ = RemoveKokoroModel()), "close");
            }
            else
                SettingRow(panel, Localize(Resource.String.ui_download_model), Localize(Resource.String.ui_kokoro_multilingual_about_350_mb), () => _ = DownloadKokoroModel(), "down");
        }
        Paragraph(Localize(Resource.String.ui_kokoro_reads_articles_notifications_and_other_text_aloud_on), panel);
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
        string detail = kokoroRemoving ? Localize(Resource.String.ui_removing_model_files) : kokoroDownload is not null
            ? kokoroProgress.Message
            : kokoroMessage ?? (KokoroModelInstaller.IsInstalled(this) ? Localize(Resource.String.ui_model_ready_offline_speech_available) : Localize(Resource.String.ui_model_download_required_about_350_mb));
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
        catch (OperationCanceledException) when (download.IsCancellationRequested) { kokoroMessage = Localize(Resource.String.ui_download_cancelled); }
        catch (Exception error)
        {
            Android.Util.Log.Warn("LauncherKokoro", error.ToString());
            kokoroMessage = Localize(Resource.String.ui_installation_failed) + error.Message;
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
            kokoroMessage = Localize(Resource.String.ui_couldn_t_remove_the_model_files_try_again);
        }
        finally
        {
            kokoroRemoving = false;
            UpdateKokoroModelUi();
        }
    }
}
