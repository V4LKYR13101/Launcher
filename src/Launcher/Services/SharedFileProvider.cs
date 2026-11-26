using Android.Content;
using Android.Database;
using Android.OS;
using Android.Webkit;
using Uri = Android.Net.Uri;

namespace LauncherApp.Services;

[ContentProvider([LauncherDistribution.FileAuthority], Exported = false, GrantUriPermissions = true)]
internal sealed class SharedFileProvider : ContentProvider
{
    private const string Authority = LauncherDistribution.FileAuthority;

    public static Uri UriFor(string path)
    {
        using var builder = new Uri.Builder();
        return builder.Scheme("content")!.Authority(Authority)!.AppendPath(path)!.Build()!;
    }

    public static string MimeType(string path)
    {
        string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return MimeTypeMap.Singleton?.GetMimeTypeFromExtension(extension) ?? "application/octet-stream";
    }

    private static string ReadPath(Uri uri) => uri.Authority == Authority && uri.PathSegments is { Count: 1 } segments
        ? segments[0]
        : throw new Java.IO.FileNotFoundException("Unknown file.");

    private Java.IO.File ResolveFile(Uri uri)
    {
        string path = ReadPath(uri);
        var context = Context!;
        if (!DownloadHistory.IsSavedLegacyFile(context, path) &&
            (!SharedStorage.CanRead(context) || !SharedStorage.MountedVolumes(context).Any(volume => SharedStorage.Contains(volume.Root, path))))
            throw new Java.IO.FileNotFoundException("File is outside accessible storage.");

        using var file = new Java.IO.File(path);
        string? canonical = file.CanonicalPath;
        if (canonical != path || !file.IsFile)
            throw new Java.IO.FileNotFoundException("File is no longer available.");

        return new Java.IO.File(canonical);
    }

    public override bool OnCreate() => true;

    public override string GetType(Uri uri) => MimeType(ReadPath(uri));

    public override ParcelFileDescriptor? OpenFile(Uri uri, string mode)
    {
        if (mode != "r")
            throw new Java.Lang.SecurityException("Read access only.");

        using var file = ResolveFile(uri);
        return ParcelFileDescriptor.Open(file, ParcelFileMode.ReadOnly);
    }

    public override ICursor Query(Uri uri, string[]? projection, string? selection, string[]? selectionArgs, string? sortOrder)
    {
        using var file = ResolveFile(uri);
        var columns = projection ?? ["_display_name", "_size"];
        var cursor = new MatrixCursor(columns);
        var row = cursor.NewRow()!;
        foreach (var column in columns)
        {
            if (column == "_display_name")
                row.Add(file.Name);
            else if (column == "_size")
                row.Add(Java.Lang.Long.ValueOf(file.Length()));
            else
                row.Add(null);
        }
        return cursor;
    }

    public override Uri? Insert(Uri uri, ContentValues? values) => throw new Java.Lang.UnsupportedOperationException();
    public override int Delete(Uri uri, string? selection, string[]? selectionArgs) => throw new Java.Lang.UnsupportedOperationException();
    public override int Update(Uri uri, ContentValues? values, string? selection, string[]? selectionArgs) => throw new Java.Lang.UnsupportedOperationException();
}
