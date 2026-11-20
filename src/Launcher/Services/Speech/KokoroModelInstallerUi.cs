
using Android.App;
using Android.Widget;

namespace LauncherApp.Services.Speech;

public static class KokoroModelInstallerUi
{
    public static void Show(
        Activity activity,
        Action? afterInstall = null)
    {
        if (KokoroModelInstaller.IsInstalled(activity))
        {
            new AlertDialog.Builder(activity)
                .SetTitle("Kokoro model")
                .SetMessage(
                    "Kokoro is installed and ready to use.\n\n" +
                    "The model, voices, lexicons and phonemizer " +
                    "data are stored locally on this device.")
                .SetPositiveButton(
                    "OK",
                    (_, _) => afterInstall?.Invoke())
                .Show();

            return;
        }

        new AlertDialog.Builder(activity)
            .SetTitle("Download Kokoro model?")
            .SetMessage(
                "Launcher needs to download the Kokoro " +
                "voice model before it can generate speech.\n\n" +
                "Download size: approximately 350 MB.\n\n" +
                "The files will be stored in Launcher's " +
                "private app-specific folder and used offline.\n\n" +
                "A Wi-Fi connection and sufficient free " +
                "storage are recommended.")
            .SetNegativeButton(
                "Not now",
                (_, _) => { })
            .SetPositiveButton(
                "Download",
                (_, _) =>
                {
                    _ = DownloadAsync(activity, afterInstall);
                })
            .Show();
    }

    private static async Task DownloadAsync(
        Activity activity,
        Action? afterInstall)
    {
        using var cts = new CancellationTokenSource();

        var layout = new LinearLayout(activity)
        {
            Orientation = Orientation.Vertical
        };

        layout.SetPadding(32, 24, 32, 24);

        var status = new TextView(activity)
        {
            Text = "Preparing model download..."
        };

        layout.AddView(status);

        var progressBar = new ProgressBar(
            activity,
            null,
            Android.Resource.Attribute.ProgressBarStyleHorizontal)
        {
            Max = 100,
            Progress = 0,
            Indeterminate = true
        };

        layout.AddView(
            progressBar,
            new LinearLayout.LayoutParams(-1, -2));

        var cancelButton = new Button(activity)
        {
            Text = "Cancel download"
        };

        cancelButton.Click += (_, _) =>
        {
            cancelButton.Enabled = false;
            status.Text = "Cancelling installation...";
            cts.Cancel();
        };

        layout.AddView(cancelButton);

        var dialog = new AlertDialog.Builder(activity)
            .SetTitle("Installing Kokoro")
            .SetView(layout)
            .SetCancelable(false)
            .Create();

        dialog.Show();

        var progress = new Progress<ModelInstallProgress>(
            update =>
            {
                if (activity.IsDestroyed ||
                    activity.IsFinishing)
                    return;

                activity.RunOnUiThread(() =>
                {
                    status.Text = update.Message;

                    if (update.Percentage.HasValue)
                    {
                        progressBar.Indeterminate = false;

                        progressBar.Progress =
                            Math.Clamp(
                                update.Percentage.Value,
                                0,
                                100);
                    }
                    else
                    {
                        progressBar.Indeterminate = true;
                    }
                });
            });

        try
        {
            await KokoroModelInstaller.InstallAsync(
                activity.ApplicationContext ?? activity,
                progress,
                cts.Token);

            dialog.Dismiss();

            if (activity.IsDestroyed || activity.IsFinishing)
                return;

            new AlertDialog.Builder(activity)
                .SetTitle("Kokoro is ready")
                .SetMessage(
                    "The model was downloaded, verified, " +
                    "and installed successfully.\n\n" +
                    "Speech generation now works offline.")
                .SetPositiveButton(
                    "Continue",
                    (_, _) => afterInstall?.Invoke())
                .Show();
        }
        catch (OperationCanceledException)
        {
            dialog.Dismiss();

            if (!activity.IsDestroyed && !activity.IsFinishing)
            {
                Toast.MakeText(
                    activity,
                    "Kokoro installation cancelled.",
                    ToastLength.Long)?.Show();
            }
        }
        catch (Exception ex)
        {
            dialog.Dismiss();

            Android.Util.Log.Error(
                "LauncherKokoro",
                ex.ToString());

            if (activity.IsDestroyed || activity.IsFinishing)
                return;

            new AlertDialog.Builder(activity)
                .SetTitle("Kokoro installation failed")
                .SetMessage(
                    ex.Message +
                    "\n\nNo incomplete model will be used. " +
                    "You can retry from Launcher Settings.")
                .SetPositiveButton(
                    "OK",
                    (_, _) => { })
                .Show();
        }
    }
}
