using Android.Content;

namespace LauncherApp;

public partial class MainActivity
{
    private const int OpenPlayFileRequest = 942;

    private void OpenPlayFile()
    {
        using var picker = new Intent(Intent.ActionOpenDocument);
        picker.AddCategory(Intent.CategoryOpenable);
        picker.SetType("*/*");
        picker.AddFlags(ActivityFlags.GrantReadUriPermission);
        try { StartActivityForResult(picker, OpenPlayFileRequest); }
        catch (ActivityNotFoundException) { Toast(Localize(Resource.String.ui_this_downloaded_file_is_no_longer_available)); }
    }

    private void ViewPlayFile(Android.Net.Uri uri)
    {
        using var intent = new Intent(Intent.ActionView);
        intent.SetDataAndType(uri, ContentResolver?.GetType(uri) ?? "application/octet-stream");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        OpenIntent(intent);
    }

    private void ShowPlayPrivacy() => Sheet(Localize(Resource.String.ui_play_privacy_policy), (panel, _) =>
    {
        Paragraph(Localize(Resource.String.ui_play_privacy_device), panel);
        Paragraph(Localize(Resource.String.ui_play_privacy_network), panel);
        Paragraph(Localize(Resource.String.ui_play_privacy_choices), panel);
    });
}
