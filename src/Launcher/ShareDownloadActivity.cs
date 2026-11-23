using System.Text.RegularExpressions;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using LauncherApp.Services;

namespace LauncherApp;

[Activity(Name = ShareActivityName, Label = "Download with Launcher", Exported = true,
    ExcludeFromRecents = true, Theme = "@android:style/Theme.Material.Dialog.NoActionBar")]
[IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault], DataMimeType = "text/*")]
public sealed class ShareDownloadActivity : Activity
{
    internal const string ShareActivityName = "com.launcher.home.ShareDownloadActivity";
    private System.Uri? url;
    private bool confirmed;
    private AlertDialog? confirmation;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (!DownloadRequest.Enabled(this))
        {
            FinishWithMessage("Downloads are off in Launcher settings.");
            return;
        }
        var intent = Intent;
        if (intent?.Action != Intent.ActionSend)
        {
            Finish();
            return;
        }
        var shared = intent.Extras?.GetCharSequence(Android.Content.Intent.ExtraText)?.ToString() ??
            intent.ClipData?.GetItemAt(0)?.Text?.ToString() ?? "";
        url = FindLink(shared);
        if (url is null)
        {
            FinishWithMessage("Share an HTTP or HTTPS file link.");
            return;
        }

        confirmed = savedInstanceState?.GetBoolean("confirmed") ?? false;
        if (confirmed)
            return;

        using var dialog = new AlertDialog.Builder(this);
        confirmation = dialog.SetTitle("Download with Launcher")!
            .SetMessage(url.AbsoluteUri)!
            .SetNegativeButton("Cancel", (_, _) => Finish())!
            .SetPositiveButton("Download", (_, _) =>
            {
                confirmed = true;
                if (!DownloadRequest.RequestPermission(this))
                    StartDownload();
            })!
            .Show();
        if (confirmation is not null)
            confirmation.CancelEvent += (_, _) => Finish();
    }

    private static System.Uri? FindLink(string text)
    {
        if (DirectFileTransfer.TryGetUrl(text, out var entire))
            return entire;
        foreach (Match match in Regex.Matches(text, "https?://[^\\s<>\"\\u201c\\u201d]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            string candidate = match.Value.TrimEnd('.', ',', ';');
            if (candidate.EndsWith(')') && candidate.Count(c => c == ')') > candidate.Count(c => c == '('))
                candidate = candidate[..^1];
            if (DirectFileTransfer.TryGetUrl(candidate, out var link))
                return link;
        }
        return null;
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        outState.PutBoolean("confirmed", confirmed);
        base.OnSaveInstanceState(outState);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == DownloadRequest.PermissionRequest && confirmed)
            StartDownload();
    }

    private void StartDownload()
    {
        if (url is not { } target)
        {
            Finish();
            return;
        }
        if (DownloadRequest.Start(this, target) is { } error)
        {
            FinishWithMessage(error);
            return;
        }
        using var home = new Intent(this, typeof(MainActivity));
        home.SetAction(Intent.ActionMain);
        home.AddCategory(Intent.CategoryHome);
        home.AddFlags(ActivityFlags.NewTask);
        home.PutExtra(MainActivity.DownloadHomeExtra, true);
        StartActivity(home);
        Finish();
    }

    private void FinishWithMessage(string message)
    {
        Toast.MakeText(this, message, ToastLength.Short)?.Show();
        Finish();
    }

    protected override void OnDestroy()
    {
        confirmation?.Dismiss();
        confirmation?.Dispose();
        base.OnDestroy();
    }
}
