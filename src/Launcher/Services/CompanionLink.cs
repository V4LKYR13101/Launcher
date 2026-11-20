using Android.Content;
using Android.Provider;
using Launcher.Core;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Uri = Android.Net.Uri;
namespace LauncherApp.Services;

// A connection runs only while Launcher is visible. No background file access.
public sealed class CompanionLink(Context context) : IDisposable
{
    private readonly ISharedPreferences prefs = context.GetSharedPreferences("companion", FileCreationMode.Private)!;
    private CancellationTokenSource? lifetime;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> allowed = new();
    public bool Running => lifetime is not null;
    public string Status { get; private set; } = "Disconnected";
    public string Address => prefs.GetString("address", "") ?? "";
    public bool Paired => !string.IsNullOrEmpty(prefs.GetString("token", null));
    public bool Uploads => prefs.GetBoolean("uploads", false);
    public string Tree => prefs.GetString("tree", "") ?? "";
    public void SetUploads(bool value) => prefs.Edit()!.PutBoolean("uploads", value)!.Apply();
    public void SetFolder(string value)
    {
        Stop();
        prefs.Edit()!.PutString("tree", value)!.Apply();
        allowed.Clear();
    }
    public void Forget()
    {
        Stop();
        prefs.Edit()!.Remove("token")!.Remove("fingerprint")!.Remove("address")!.Apply();
    }
    public static string ValidateAddress(string text)
    {
        if (!System.Uri.TryCreate(text.Trim().TrimEnd('/'), UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Port != 17842 || uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || !IPAddress.TryParse(uri.Host, out var ip))
            throw new ArgumentException("Use the companion address, such as https://192.168.1.10:17842");
        var b = ip.GetAddressBytes();
        if (b.Length != 4 || !(b[0] == 10 || b[0] == 192 && b[1] == 168 || b[0] == 172 && b[1] >= 16 && b[1] <= 31))
            throw new ArgumentException("Use a private Wi-Fi network address.");
        return uri.GetLeftPart(UriPartial.Authority);
    }
    private static HttpClient Client(string fingerprint)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false };
        handler.ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert is not null && Convert.ToHexString(SHA256.HashData(cert.RawData)) == fingerprint;
        return new(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
    public static async Task<string> Probe(string address)
    {
        string fingerprint = "";
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false };
        // No credentials or phone data are sent before the user compares this pin.
        handler.ServerCertificateCustomValidationCallback = (_, cert, _, _) => { if (cert is null) return false; fingerprint = Convert.ToHexString(SHA256.HashData(cert.RawData)); return true; };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        using var response = await client.GetAsync(ValidateAddress(address) + "/pair", HttpCompletionOption.ResponseHeadersRead);
        if (fingerprint.Length != 64)
            throw new HttpRequestException("Could not read the companion fingerprint.");
        return fingerprint;
    }
    public async Task Pair(string address, string fingerprint, string pin)
    {
        using var client = Client(fingerprint);
        using var content = new StringContent(JsonSerializer.Serialize(new CompanionPair { Pin = pin.Trim() }, CompanionJson.Default.CompanionPair), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(ValidateAddress(address) + "/pair", content);
        response.EnsureSuccessStatusCode();
        var paired = await Read(response, CompanionJson.Default.CompanionPair, CancellationToken.None);
        if (paired is null || paired.Token.Length != 64 || !paired.Token.All(System.Uri.IsHexDigit))
            throw new InvalidDataException("Invalid pairing response.");
        prefs.Edit()!.PutString("address", address)!.PutString("fingerprint", fingerprint)!.PutString("token", paired.Token)!.Apply();
    }
    public void Start()
    {
        if (Running || !Paired)
            return;
        allowed.Clear();
        lifetime = new();
        _ = Run(lifetime.Token);
    }
    public void Stop()
    {
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
        Status = "Disconnected";
        allowed.Clear();
    }
    public void Dispose() => Stop();
    private static async Task<T?> Read<T>(HttpResponseMessage response, JsonTypeInfo<T> type, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > 24_000_000)
            throw new InvalidDataException("Transfer is too large.");
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var block = new byte[32768];
        int n;
        while ((n = await stream.ReadAsync(block, ct)) > 0)
        {
            if (buffer.Length + n > 24_000_000)
                throw new InvalidDataException("Transfer is too large.");
            buffer.Write(block, 0, n);
        }
        return JsonSerializer.Deserialize(buffer.ToArray(), type);
    }
    private async Task Run(CancellationToken ct)
    {
        try
        {
            using var client = Client(prefs.GetString("fingerprint", "")!);
            client.BaseAddress = new(ValidateAddress(Address));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", prefs.GetString("token", "")!);
            while (!ct.IsCancellationRequested)
            {
                var current = new StateStore(context).Load();
                var snapshot = new CompanionSnapshot { Notes = current.Notes, FolderShared = Tree.Length > 0, AllowUploads = Uploads, Device = Android.OS.Build.Model ?? "Android" };
                using (var content = new StringContent(JsonSerializer.Serialize(snapshot, CompanionJson.Default.CompanionSnapshot), Encoding.UTF8, "application/json"))
                using (var sent = await client.PostAsync("/api/snapshot", content, ct))
                    sent.EnsureSuccessStatusCode();
                Status = "Connected while Launcher is open";
                using var next = await client.GetAsync("/api/next", HttpCompletionOption.ResponseHeadersRead, ct);
                next.EnsureSuccessStatusCode();
                if (next.StatusCode != HttpStatusCode.NoContent && await Read(next, CompanionJson.Default.CompanionCommand, ct) is { } command)
                {
                    CompanionResult result;
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        result = await Task.Run(() => Execute(command), ct);
                    }
                    catch (Exception e) when (e is Java.Lang.RuntimeException or IOException or ArgumentException or InvalidOperationException or FormatException)
                    {
                        result = new()
                        {
                            Id = command.Id,
                            Error = e is UnauthorizedAccessException ? "Access denied." : e.Message
                        };
                    }
                    using var content = new StringContent(JsonSerializer.Serialize(result, CompanionJson.Default.CompanionResult), Encoding.UTF8, "application/json");
                    using var posted = await client.PostAsync("/api/result", content, ct);
                    posted.EnsureSuccessStatusCode();
                }
                await Task.Delay(2000, ct);
            }
        }
        catch (OperationCanceledException) { if (!ct.IsCancellationRequested) Status = "Connection timed out. Stop and connect again."; }
        catch (Exception e) when (e is HttpRequestException or IOException or ArgumentException or JsonException or Java.Lang.RuntimeException)
        {
            Status = "Connection stopped. Check the desktop address and reconnect.";
        }
        // Keep the session object until Stop: a failed connection never retries silently.
    }
    private CompanionResult Execute(CompanionCommand command)
    {
        var result = new CompanionResult { Id = command.Id };
        if (command.Kind == "note")
        {
            var note = command.Note ?? throw new ArgumentException("No note provided.");
            if (note.Id.Length != 32 || !note.Id.All(System.Uri.IsHexDigit) || note.Text.Length > 20000 || note.Title.Length > 48)
                throw new ArgumentException("Invalid note.");
            var store = new StateStore(context);
            store.EditNotes(state =>
            {
                var old = state.Notes.FirstOrDefault(n => n.Id == note.Id);
                if (old is not null)
                    note.Checklist = old.Checklist;
                if (state.DeletedNoteIds.Contains(note.Id) || old?.UpdatedAt != command.ExpectedAt && !(old is null && command.ExpectedAt == 0) || WorkspaceEditing.NoteId == note.Id)
                    throw new InvalidOperationException("This note changed on the phone. Close the phone editor and reopen the note on desktop before saving.");
                if (old is null && state.Notes.Count >= 200)
                    throw new InvalidOperationException("The note limit is 200.");
                note.Title = LauncherState.CleanName(note.Title, "Note");
                note.UpdatedAt = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), (old?.UpdatedAt ?? 0) + 1);
                if (old is not null)
                    state.Notes.Remove(old);
                else
                    state.RootOrder.Add("note:" + note.Id);
                state.Notes.Add(note);
            });
            return result;
        }
        if (Tree.Length == 0)
            throw new InvalidOperationException("Choose a shared folder on your phone first.");
        var tree = Uri.Parse(Tree)!;
        string uriText = command.Uri;
        if (uriText.Length == 0)
            uriText = DocumentsContract.BuildDocumentUriUsingTree(tree, DocumentsContract.GetTreeDocumentId(tree))!.ToString()!;
        string root = DocumentsContract.BuildDocumentUriUsingTree(tree, DocumentsContract.GetTreeDocumentId(tree))!.ToString()!;
        if (uriText != root && !allowed.ContainsKey(uriText))
            throw new InvalidOperationException("Refresh the shared folder before opening this item.");
        var uri = Uri.Parse(uriText)!;
        bool directory = uriText == root || allowed.GetValueOrDefault(uriText);
        if (command.Kind == "list")
        {
            if (!directory)
                throw new InvalidOperationException("Choose a folder.");
            var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, DocumentsContract.GetDocumentId(uri));
            using var cursor = context.ContentResolver!.Query(children!, [DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName, DocumentsContract.Document.ColumnMimeType, DocumentsContract.Document.ColumnSize], null, null, null);
            while (cursor?.MoveToNext() == true && result.Files.Count < 1000)
            {
                string child = DocumentsContract.BuildDocumentUriUsingTree(tree, cursor.GetString(0))!.ToString()!;
                bool folder = cursor.GetString(2) == DocumentsContract.Document.MimeTypeDir;
                allowed[child] = folder;
                result.Files.Add(new()
                {
                    Uri = child,
                    Name = cursor.GetString(1) ?? "File",
                    Mime = cursor.GetString(2) ?? "application/octet-stream",
                    Size = cursor.IsNull(3) ? 0 : cursor.GetLong(3),
                    Folder = folder
                });
            }
            if (allowed.Count > 10000)
            {
                allowed.Clear();
                throw new InvalidOperationException("Folder session limit reached. Refresh the shared folder.");
            }
            return result;
        }
        if (command.Kind == "download")
        {
            if (directory)
                throw new InvalidOperationException("Choose a file.");
            using var cursor = context.ContentResolver!.Query(uri, [DocumentsContract.Document.ColumnDisplayName, DocumentsContract.Document.ColumnMimeType, DocumentsContract.Document.ColumnSize], null, null, null);
            if (cursor?.MoveToFirst() != true)
                throw new IOException("File is no longer available.");
            if (!cursor.IsNull(2) && cursor.GetLong(2) > 16 * 1024 * 1024)
                throw new IOException("Transfers are limited to 16 MB.");
            result.Name = cursor.GetString(0) ?? "File";
            result.Mime = cursor.GetString(1) ?? "application/octet-stream";
            using var input = context.ContentResolver.OpenInputStream(uri) ?? throw new IOException("File is unavailable.");
            using var output = new MemoryStream();
            var buffer = new byte[32768];
            int n;
            while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + n > 16 * 1024 * 1024)
                    throw new IOException("Transfers are limited to 16 MB.");
                output.Write(buffer, 0, n);
            }
            result.Data = Convert.ToBase64String(output.ToArray());
            return result;
        }
        if (command.Kind == "upload")
        {
            if (!Uploads || !directory)
                throw new InvalidOperationException("Enable uploads on your phone and choose a folder.");
            if (command.Name.Length == 0 || command.Name.Length > 160 || command.Name.IndexOfAny(['/', '\\', '\0']) >= 0 || command.Name is "." or "..")
                throw new ArgumentException("Invalid filename.");
            byte[] bytes = Convert.FromBase64String(command.Data);
            if (bytes.Length > 16 * 1024 * 1024)
                throw new IOException("Transfers are limited to 16 MB.");
            var created = DocumentsContract.CreateDocument(context.ContentResolver!, uri, command.Mime.Length < 150 ? command.Mime : "application/octet-stream", command.Name) ?? throw new IOException("Could not create file.");
            try
            {
                using var output = context.ContentResolver!.OpenOutputStream(created) ?? throw new IOException("Could not write file.");
                output.Write(bytes);
            }
            catch { try { DocumentsContract.DeleteDocument(context.ContentResolver!, created); } catch (Java.Lang.RuntimeException) { } throw; }
            return result;
        }
        throw new ArgumentException("Unknown command.");
    }
}
