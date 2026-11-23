using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml;
using Android.Content;
using Android.Database.Sqlite;
using SentenceTransformers.EmbeddingGemma2;

namespace LauncherApp.Services.DocumentSearch;

internal sealed class DocumentIndex : IDisposable
{
    internal const int Dimensions = 256;
    internal readonly record struct Counts(int Indexed, int Skipped, int Passages);
    private sealed record Document(string Path, long Modified, long Bytes, long Id = 0, int SavedChunks = 0);
    private readonly SQLiteDatabase database;

    internal DocumentIndex(string path, bool readOnly = false)
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
        database.ExecSQL("CREATE TABLE IF NOT EXISTS documents(id INTEGER PRIMARY KEY, path TEXT NOT NULL UNIQUE, modified INTEGER NOT NULL, bytes INTEGER NOT NULL, complete INTEGER NOT NULL)");
        database.ExecSQL("CREATE TABLE IF NOT EXISTS passages(doc INTEGER NOT NULL, position INTEGER NOT NULL, vector BLOB NOT NULL, PRIMARY KEY(doc, position)) WITHOUT ROWID");
    }

    internal Counts ReadCounts()
    {
        using var rows = database.RawQuery(
            "SELECT COUNT(DISTINCT p.doc), SUM(CASE WHEN d.complete = 1 AND p.doc IS NULL THEN 1 ELSE 0 END), COUNT(p.doc) " +
            "FROM documents d LEFT JOIN passages p ON p.doc = d.id", null)!;
        rows.MoveToFirst();
        return new(rows.GetInt(0), rows.IsNull(1) ? 0 : rows.GetInt(1), rows.GetInt(2));
    }

    internal async Task Update(SearchFile[] files, Func<bool> canContinue, Action<int, int> progress,
        Func<string, string, CancellationToken, Task<float[]>> encode, CancellationToken token)
    {
        var pending = new Dictionary<string, Document>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            if (DocumentText.Supports(file.Path) && TryReadStamp(file.Path) is { } stamp)
                pending[file.Path] = new(file.Path, stamp.Modified, stamp.Bytes);
        }
        if (!canContinue())
            return;
        int total = pending.Count;
        var stale = new List<long>();
        using (var rows = database.RawQuery("SELECT id, path, modified, bytes, complete, " +
            "(SELECT COUNT(*) FROM passages WHERE doc = documents.id) FROM documents", null)!)
        {
            while (rows.MoveToNext())
            {
                token.ThrowIfCancellationRequested();
                long id = rows.GetLong(0);
                string path = rows.GetString(1)!;
                if (!pending.TryGetValue(path, out var current) || current.Modified != rows.GetLong(2) || current.Bytes != rows.GetLong(3))
                    stale.Add(id);
                else if (rows.GetInt(4) != 0)
                    pending.Remove(path);
                else
                    pending[path] = current with { Id = id, SavedChunks = rows.GetInt(5) };
            }
        }
        database.BeginTransactionNonExclusive();
        try
        {
            foreach (long id in stale)
            {
                token.ThrowIfCancellationRequested();
                string key = id.ToString(CultureInfo.InvariantCulture);
                database.Delete("passages", "doc = ?", [key]);
                database.Delete("documents", "id = ?", [key]);
            }
            database.SetTransactionSuccessful();
        }
        finally { database.EndTransaction(); }

        int done = total - pending.Count;
        progress(done, total);
        if (pending.Count == 0)
            return;
        foreach (var document in pending.Values.OrderByDescending(doc => doc.Modified))
        {
            token.ThrowIfCancellationRequested();
            if (!canContinue())
                return;
            long id = document.Id == 0 ? SaveDocument(document) : document.Id;
            int position = 0;
            using var passages = DocumentText.Passages(document.Path, token).GetEnumerator();
            while (TryReadPassage(passages, out string passage))
            {
                token.ThrowIfCancellationRequested();
                if (!canContinue())
                    return;
                if (position++ < document.SavedChunks)
                    continue;
                string title = Path.GetFileNameWithoutExtension(document.Path);
                var embedding = await encode(passage, title, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (!canContinue())
                    return;
                SavePassage(id, position - 1, embedding);
            }
            using var values = new ContentValues();
            values.Put("complete", 1);
            database.Update("documents", values, "id = ?", [id.ToString(CultureInfo.InvariantCulture)]);
            progress(++done, total);
        }
    }

    private static bool TryReadPassage(IEnumerator<string> passages, out string text)
    {
        text = "";
        try
        {
            if (!passages.MoveNext())
                return false;
            text = passages.Current;
            return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or XmlException or
            UnauthorizedAccessException or ArgumentException or NotSupportedException ||
            error.GetType().Namespace?.StartsWith("UglyToad.PdfPig", StringComparison.Ordinal) == true)
        {
            // A missing, protected, or malformed document must not stop the remaining files.
            return false;
        }
    }

    internal static async Task<SentenceEncoder> LoadEncoder(string path)
    {
        var encoder = await SentenceEncoder.LoadAsync(path,
            new ParallelOptions { MaxDegreeOfParallelism = 2 }, EmbeddingGemma2Model.Text270M).ConfigureAwait(false);
        encoder.OutputDimension = Dimensions;
        return encoder;
    }

    private long SaveDocument(Document document)
    {
        using var values = new ContentValues();
        values.Put("path", document.Path);
        values.Put("modified", document.Modified);
        values.Put("bytes", document.Bytes);
        values.Put("complete", 0);
        return database.InsertOrThrow("documents", null, values);
    }

    private void SavePassage(long id, int position, float[] embedding)
    {
        if (embedding.Length != Dimensions)
            throw new InvalidDataException("Unexpected document embedding size.");
        var compact = Array.ConvertAll(embedding, value => (Half)value);
        using var values = new ContentValues();
        values.Put("doc", id);
        values.Put("position", position);
        values.Put("vector", MemoryMarshal.AsBytes(compact.AsSpan()).ToArray());
        database.InsertOrThrow("passages", null, values);
    }

    internal string[] Search(float[] query, Func<string, bool> isCandidate, CancellationToken token)
    {
        // A document ranks by its best matching passage.
        var best = new Dictionary<string, float>(StringComparer.Ordinal);
        using var rows = database.RawQuery("SELECT d.path, d.modified, d.bytes, p.vector FROM passages p JOIN documents d ON p.doc = d.id ORDER BY p.doc", null)!;
        string? previousPath = null;
        bool usable = false;
        while (rows.MoveToNext())
        {
            token.ThrowIfCancellationRequested();
            string path = rows.GetString(0)!;
            if (previousPath != path)
            {
                previousPath = path;
                usable = isCandidate(path) && TryReadStamp(path) == (rows.GetLong(1), rows.GetLong(2));
            }
            if (!usable)
                continue;
            var vector = MemoryMarshal.Cast<byte, Half>(rows.GetBlob(3)!);
            if (vector.Length != query.Length)
                continue;
            float score = 0;
            for (int i = 0; i < query.Length; i++)
                score += query[i] * (float)vector[i];
            if (score >= .58f && score > best.GetValueOrDefault(path))
                best[path] = score;
        }
        if (best.Count == 0)
            return [];
        float cutoff = best.Values.Max() - .12f;
        return best.Where(item => item.Value >= cutoff).OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal)
            .Take(24).Select(item => item.Key).ToArray();
    }

    internal bool HasPassages()
    {
        using var rows = database.RawQuery("SELECT 1 FROM passages LIMIT 1", null)!;
        return rows.MoveToFirst();
    }

    private static (long Modified, long Bytes)? TryReadStamp(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? (info.LastWriteTimeUtc.Ticks, info.Length) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }

    public void Dispose()
    {
        database.Close();
        database.Dispose();
    }
}
