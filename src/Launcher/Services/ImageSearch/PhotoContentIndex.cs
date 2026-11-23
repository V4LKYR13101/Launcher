using System.Globalization;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using Android.Content;
using Android.Database.Sqlite;

namespace LauncherApp.Services.ImageSearch;

internal sealed class PhotoContentIndex : IDisposable
{
    internal sealed record Person(long Id, string Name, int Photos, byte[] Preview);
    private sealed record Group(long Id, float[] Embedding);
    private readonly SQLiteDatabase database;

    internal PhotoContentIndex(string path, bool readOnly = false)
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
        database.ExecSQL("CREATE TABLE IF NOT EXISTS photos(path TEXT PRIMARY KEY, modified INTEGER NOT NULL, bytes INTEGER NOT NULL, text TEXT NOT NULL)");
        database.ExecSQL("CREATE TABLE IF NOT EXISTS people(id INTEGER PRIMARY KEY, name TEXT NOT NULL DEFAULT '', embedding BLOB NOT NULL, preview BLOB NOT NULL)");
        database.ExecSQL("CREATE TABLE IF NOT EXISTS faces(path TEXT NOT NULL, person INTEGER NOT NULL, PRIMARY KEY(path, person))");
        database.ExecSQL("CREATE INDEX IF NOT EXISTS faces_by_person ON faces(person)");
    }

    internal int Count
    {
        get
        {
            using var cursor = database.RawQuery("SELECT COUNT(*) FROM photos", null)!;
            cursor.MoveToFirst();
            return cursor.GetInt(0);
        }
    }

    internal void Update(Context context, string models, bool people, Func<bool> stillEnabled,
        Action<int, int> progress, CancellationToken token)
    {
        var pending = new Dictionary<string, ImageSearchIndex.Image>(StringComparer.Ordinal);
        foreach (var file in SharedStorage.Files(context, token))
        {
            if (!ImageSearchIndex.IsSupported(file.Path))
                continue;
            try
            {
                var info = new FileInfo(file.Path);
                if (info.Exists)
                    pending[file.Path] = new(file.Path, info.LastWriteTimeUtc.Ticks, info.Length);
            }
            catch (IOException) { }
        }
        if (!stillEnabled())
            return;
        int total = pending.Count;
        var outdated = new List<string>();
        using (var rows = database.RawQuery("SELECT path, modified, bytes FROM photos", null)!)
        {
            while (rows.MoveToNext())
            {
                token.ThrowIfCancellationRequested();
                string path = rows.GetString(0)!;
                if (pending.TryGetValue(path, out var image) && image.Modified == rows.GetLong(1) && image.Bytes == rows.GetLong(2))
                    pending.Remove(path);
                else
                    outdated.Add(path);
            }
        }
        Transaction(() =>
        {
            foreach (string path in outdated)
            {
                token.ThrowIfCancellationRequested();
                database.Delete("faces", "path = ?", [path]);
                database.Delete("photos", "path = ?", [path]);
            }
        });
        int done = total - pending.Count;
        progress(done, total);
        if (pending.Count == 0)
        {
            if (people)
                PruneGroups();
            return;
        }

        using var text = people ? null : new PhotoTextReader(models);
        using var faces = people ? new PhotoFaceReader(models) : null;
        var groups = people ? ReadGroups() : [];
        foreach (var image in pending.Values.OrderByDescending(image => image.Modified))
        {
            token.ThrowIfCancellationRequested();
            if (!stillEnabled())
                break;
            string contents = "";
            IReadOnlyList<PhotoFaceReader.Face> found = [];
            try
            {
                using var bitmap = PhotoModelInput.Decode(image.Path, people ? 1280 : 1920);
                if (bitmap is not null)
                {
                    if (text is not null)
                        contents = text.Read(bitmap, token);
                    else if (faces is not null)
                        found = faces.Read(bitmap, token);
                }
            }
            catch (Java.Lang.IllegalArgumentException) { }
            catch (Java.IO.IOException) { }
            token.ThrowIfCancellationRequested();
            if (!stillEnabled())
                break;
            Transaction(() =>
            {
                using var values = new ContentValues();
                values.Put("path", image.Path);
                values.Put("modified", image.Modified);
                values.Put("bytes", image.Bytes);
                values.Put("text", contents);
                database.InsertOrThrow("photos", null, values);
                if (found.Count > 0)
                    SaveFaces(image.Path, found, groups);
            });
            progress(++done, total);
        }
        if (people)
            PruneGroups();
    }

    private void PruneGroups() => database.ExecSQL("DELETE FROM people WHERE name = '' AND NOT EXISTS (SELECT 1 FROM faces WHERE person = people.id)");

    private List<Group> ReadGroups()
    {
        var groups = new List<Group>();
        using var rows = database.RawQuery("SELECT id, embedding FROM people", null)!;
        while (rows.MoveToNext())
        {
            var vector = MemoryMarshal.Cast<byte, Half>(rows.GetBlob(1)!).ToArray();
            if (vector.Length == 512)
                groups.Add(new(rows.GetLong(0), Array.ConvertAll(vector, value => (float)value)));
        }
        return groups;
    }

    private void SaveFaces(string path, IReadOnlyList<PhotoFaceReader.Face> faces, List<Group> groups)
    {
        var used = new HashSet<long>();
        foreach (var face in faces)
        {
            // Favor a separate group when the match is uncertain.
            float best = .62f;
            long person = -1;
            foreach (var group in groups)
            {
                if (used.Contains(group.Id))
                    continue;
                float score = TensorPrimitives.Dot(group.Embedding, face.Embedding);
                if (score > best)
                {
                    person = group.Id;
                    best = score;
                }
            }
            if (person < 0)
            {
                var compact = Array.ConvertAll(face.Embedding, value => (Half)value);
                using var group = new ContentValues();
                group.Put("embedding", MemoryMarshal.AsBytes(compact.AsSpan()).ToArray());
                group.Put("preview", face.Preview);
                person = database.InsertOrThrow("people", null, group);
                groups.Add(new(person, face.Embedding));
            }
            used.Add(person);
            using var link = new ContentValues();
            link.Put("path", path);
            link.Put("person", person);
            database.InsertOrThrow("faces", null, link);
        }
    }

    internal Person[] People(int offset)
    {
        using var rows = database.RawQuery(
            "SELECT p.id, p.name, COUNT(*), p.preview FROM people p JOIN faces f ON f.person=p.id GROUP BY p.id ORDER BY (p.name = ''), COUNT(*) DESC, p.id LIMIT 26 OFFSET ?",
            [offset.ToString(CultureInfo.InvariantCulture)])!;
        var result = new List<Person>();
        while (rows.MoveToNext())
            result.Add(new(rows.GetLong(0), rows.GetString(1)!, rows.GetInt(2), rows.GetBlob(3)!));
        return result.ToArray();
    }

    internal void Name(long id, string name)
    {
        using var values = new ContentValues();
        values.Put("name", name.Trim());
        database.Update("people", values, "id = ?", [id.ToString(CultureInfo.InvariantCulture)]);
    }

    internal string[] MatchText(string query, Func<string, bool> isCandidate, CancellationToken token)
    {
        bool quoted = query.Length > 2 && query[0] == '"' && query[^1] == '"';
        string[] parts = quoted ? [CollapseSpaces(query[1..^1])] : query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts[0].Length == 0)
            return [];
        using var rows = database.RawQuery("SELECT path, text FROM photos WHERE text <> '' ORDER BY modified DESC", null)!;
        var matches = new List<string>();
        while (matches.Count < 100 && rows.MoveToNext())
        {
            token.ThrowIfCancellationRequested();
            string path = rows.GetString(0)!, content = rows.GetString(1)!;
            if (!isCandidate(path))
                continue;
            if (quoted)
                content = CollapseSpaces(content);
            if (parts.All(part => content.Contains(part, StringComparison.OrdinalIgnoreCase)))
                matches.Add(path);
        }
        return matches.ToArray();
    }

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    internal string[] MatchPeople(string query, Func<string, bool> isCandidate, CancellationToken token)
    {
        using var rows = database.RawQuery("SELECT f.path, p.name FROM faces f JOIN people p ON f.person=p.id JOIN photos img ON f.path=img.path WHERE p.name <> '' ORDER BY img.modified DESC", null)!;
        var matches = new HashSet<string>(StringComparer.Ordinal);
        while (rows.MoveToNext())
        {
            token.ThrowIfCancellationRequested();
            string path = rows.GetString(0)!, name = rows.GetString(1)!;
            if ((name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
                name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(part => part.Equals(query, StringComparison.OrdinalIgnoreCase))) && isCandidate(path))
                matches.Add(path);
        }
        return matches.ToArray();
    }

    private void Transaction(Action action)
    {
        database.BeginTransactionNonExclusive();
        try { action(); database.SetTransactionSuccessful(); }
        finally { database.EndTransaction(); }
    }

    public void Dispose()
    {
        database.Close();
        database.Dispose();
    }
}
