using Android.Content;
using Android.Database;
using Android.OS;
using Uri = Android.Net.Uri;

namespace LauncherApp.Services;

// Only this APK can be shared, read-only, with an explicit temporary URI grant.
[ContentProvider(["com.launcher.home.updates"], Exported = false, GrantUriPermissions = true)]
internal sealed class UpdateProvider : ContentProvider
{
    private void Validate(Uri uri)
    {
        if (uri.Authority != "com.launcher.home.updates" || uri.Path != "/Launcher-update.apk")
            throw new Java.IO.FileNotFoundException("Unknown update file.");
    }
    public override bool OnCreate() => true;
    public override string? GetType(Uri uri) { Validate(uri); return "application/vnd.android.package-archive"; }
    public override ParcelFileDescriptor? OpenFile(Uri uri, string mode)
    {
        Validate(uri);
        if (mode != "r") throw new Java.Lang.SecurityException("Read access only.");
        return ParcelFileDescriptor.Open(new Java.IO.File(Context!.CacheDir!, "Launcher-update.apk"), ParcelFileMode.ReadOnly);
    }
    public override ICursor? Query(Uri uri, string[]? projection, string? selection, string[]? selectionArgs, string? sortOrder)
    {
        Validate(uri);
        var columns = projection ?? ["_display_name", "_size"];
        var cursor = new MatrixCursor(columns);
        var row = cursor.NewRow()!;
        foreach (var column in columns)
        {
            if (column == "_display_name") row.Add("Launcher-update.apk");
            else if (column == "_size") row.Add(Java.Lang.Long.ValueOf(new Java.IO.File(Context!.CacheDir!, "Launcher-update.apk").Length()));
            else row.Add(null);
        }
        return cursor;
    }
    public override Uri? Insert(Uri uri, ContentValues? values) => throw new Java.Lang.UnsupportedOperationException();
    public override int Delete(Uri uri, string? selection, string[]? selectionArgs) => throw new Java.Lang.UnsupportedOperationException();
    public override int Update(Uri uri, ContentValues? values, string? selection, string[]? selectionArgs) => throw new Java.Lang.UnsupportedOperationException();
}

