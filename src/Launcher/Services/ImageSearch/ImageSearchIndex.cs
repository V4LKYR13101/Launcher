using System.Runtime.InteropServices;
using Android.Content;
using Android.Database.Sqlite;

namespace LauncherApp.Services.ImageSearch;

internal sealed class ImageSearchIndex : IDisposable
{
    internal sealed record Image(string Path, long Modified, long Bytes);
    internal readonly record struct Counts(int Processed, int Total, int Skipped);
    private readonly SQLiteDatabase database;

    internal ImageSearchIndex(string path, bool readOnly = false)
    {
        if (readOnly)
        {
            database = SQLiteDatabase.OpenDatabase(path, null, DatabaseOpenFlags.OpenReadonly)!;
            return;
        }
        bool isNew = !File.Exists(path);
        database = SQLiteDatabase.OpenOrCreateDatabase(path, null)!;
        if (isNew)
            database.ExecSQL("PRAGMA auto_vacuum = FULL");
        database.EnableWriteAheadLogging();
        database.ExecSQL("CREATE TABLE IF NOT EXISTS images (path TEXT PRIMARY KEY, modified INTEGER NOT NULL, bytes INTEGER NOT NULL, embedding BLOB)");
    }

    internal static bool IsSupported(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".jpg" or ".jpeg" or ".jpe" or ".png" or ".webp" or ".heic" or ".heif" or ".avif" or ".gif" or ".bmp";

    internal Counts ReadCounts()
    {
        using var rows = database.RawQuery("SELECT COUNT(*), COUNT(*) - COUNT(embedding) FROM images", null)!;
        rows.MoveToFirst();
        int count = rows.GetInt(0);
        return new(count, count, rows.GetInt(1));
    }

    internal void Update(Context context, string models, Action<Counts> progress, CancellationToken token)
    {
        var images = new Dictionary<string, Image>(StringComparer.Ordinal);
        foreach (var file in SharedStorage.Files(context, token))
        {
            if (!IsSupported(file.Path))
                continue;
            try
            {
                var info = new FileInfo(file.Path);
                if (info.Exists)
                    images[file.Path] = new(file.Path, info.LastWriteTimeUtc.Ticks, info.Length);
            }
            catch (IOException) { }
        }

        var pending = new Dictionary<string, Image>(images, StringComparer.Ordinal);
        var stale = new List<string>();
        int skipped = 0;
        using (var rows = database.RawQuery("SELECT path, modified, bytes, embedding IS NULL FROM images", null)!)
        {
            while (rows.MoveToNext())
            {
                token.ThrowIfCancellationRequested();
                string path = rows.GetString(0)!;
                if (!images.TryGetValue(path, out var image) || image.Modified != rows.GetLong(1) || image.Bytes != rows.GetLong(2))
                {
                    stale.Add(path);
                    continue;
                }
                pending.Remove(path);
                skipped += rows.GetInt(3);
            }
        }

        database.BeginTransactionNonExclusive();
        try
        {
            foreach (string path in stale)
            {
                token.ThrowIfCancellationRequested();
                database.Delete("images", "path = ?", [path]);
            }
            database.SetTransactionSuccessful();
        }
        finally { database.EndTransaction(); }

        int processed = images.Count - pending.Count;
        progress(new(processed, images.Count, skipped));
        if (pending.Count == 0)
            return;

        using var encoder = ClipEncoder.Open(models, ImageSearchModels.Visual);
        foreach (var image in pending.Values.OrderByDescending(image => image.Modified))
        {
            token.ThrowIfCancellationRequested();
            if (!SharedStorage.CanRead(context))
                return;

            float[]? vector;
            try { vector = ClipEncoder.Image(encoder, image.Path); }
            catch (Java.Lang.IllegalArgumentException) { vector = null; }
            catch (Java.IO.IOException) { vector = null; }
            token.ThrowIfCancellationRequested();
            using var values = new ContentValues();
            values.Put("path", image.Path);
            values.Put("modified", image.Modified);
            values.Put("bytes", image.Bytes);
            if (vector is null)
            {
                values.PutNull("embedding");
                skipped++;
            }
            else
            {
                var compact = Array.ConvertAll(vector, value => (Half)value);
                values.Put("embedding", MemoryMarshal.AsBytes(compact.AsSpan()).ToArray());
            }
            database.InsertOrThrow("images", null, values);
            progress(new(++processed, images.Count, skipped));
        }
    }

    internal string[] Match(float[] query, Func<string, bool> isCandidate, CancellationToken token)
    {
        const int limit = 24;
        // Starting cosine cutoff for this exact OpenCLIP export. It avoids always returning 24 items.
        const float minimumScore = 0.22f;
        var best = new PriorityQueue<string, float>();
        using var rows = database.RawQuery("SELECT path, embedding, modified, bytes FROM images WHERE embedding IS NOT NULL", null)!;
        while (rows.MoveToNext())
        {
            token.ThrowIfCancellationRequested();
            string path = rows.GetString(0)!;
            if (!isCandidate(path))
                continue;
            byte[] blob = rows.GetBlob(1)!;
            var vector = MemoryMarshal.Cast<byte, Half>(blob);
            if (vector.Length != query.Length)
                continue;
            float score = 0;
            for (int i = 0; i < vector.Length; i++)
                score += query[i] * (float)vector[i];
            if (score < minimumScore || best.Count == limit && best.TryPeek(out _, out float lowest) && score <= lowest)
                continue;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.LastWriteTimeUtc.Ticks != rows.GetLong(2) || file.Length != rows.GetLong(3))
                    continue;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { continue; }
            best.Enqueue(path, score);
            if (best.Count > limit)
                best.Dequeue();
        }
        return best.UnorderedItems.OrderByDescending(item => item.Priority).Select(item => item.Element).ToArray();
    }

    public void Dispose()
    {
        database.Close();
        database.Dispose();
    }
}
