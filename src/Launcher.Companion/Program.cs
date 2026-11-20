using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Launcher.Core;

var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LauncherCompanion");
Directory.CreateDirectory(data);
var certPath = Path.Combine(data, "server.pfx");
if (!File.Exists(certPath))
{
    using var rsa = RSA.Create(3072);
    var request = new CertificateRequest("CN=Launcher Companion", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
    using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    File.WriteAllBytes(certPath, created.Export(X509ContentType.Pfx));
}
using var certificate = X509CertificateLoader.LoadPkcs12FromFile(certPath, null);
string fingerprint = Convert.ToHexString(SHA256.HashData(certificate.RawData));
string csrf = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
string pin = "", token = "";
DateTime pinUntil = DateTime.MinValue, lastSeen = DateTime.MinValue;
int failures = 0;
var gate = new object();
var commands = new ConcurrentQueue<CompanionCommand>();
var results = new ConcurrentDictionary<string, CompanionResult>();
var resultTimes = new ConcurrentDictionary<string, DateTime>();
CompanionSnapshot snapshot = new();
void ResetPair() { lock (gate) { pin = RandomNumberGenerator.GetInt32(10000000, 100000000).ToString(); pinUntil = DateTime.UtcNow.AddMinutes(10); token = ""; failures = 0; snapshot = new(); while (commands.TryDequeue(out _)) { } results.Clear(); resultTimes.Clear(); } }
ResetPair();
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(k => { k.Limits.MaxRequestBodySize = 24_000_000; k.Listen(IPAddress.Loopback, 17841); k.ListenAnyIP(17842, l => l.UseHttps(certificate)); });
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    bool local = context.Connection.LocalPort == 17841 && IPAddress.IsLoopback(context.Connection.RemoteIpAddress!);
    if (local)
    {
        if (context.Request.Host.Host is not ("127.0.0.1" or "localhost"))
        {
            context.Response.StatusCode = 403;
            return;
        }
        string origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && origin != "http://127.0.0.1:17841" && origin != "http://localhost:17841")
        {
            context.Response.StatusCode = 403;
            return;
        }
        if (context.Request.Method != "GET" && context.Request.Headers["X-Launcher"].ToString() != csrf)
        {
            context.Response.StatusCode = 403;
            return;
        }
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path == "/pair")
        {
            context.Response.StatusCode = 404;
            return;
        }
    }
    else
    {
        if (!context.Request.IsHttps || !(context.Request.Path.StartsWithSegments("/api") || context.Request.Path == "/pair"))
        {
            context.Response.StatusCode = 404;
            return;
        }
        if (context.Request.Path != "/pair")
        {
            string supplied = context.Request.Headers.Authorization.ToString();
            bool valid;
            lock (gate)
                valid = token.Length > 0 && CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(supplied), System.Text.Encoding.UTF8.GetBytes("Bearer " + token));
            if (!valid)
            {
                context.Response.StatusCode = 401;
                return;
            }
            lock (gate)
                lastSeen = DateTime.UtcNow;
        }
    }
    await next();
});
app.MapGet("/", () =>
{
    using var stream = typeof(Program).Assembly.GetManifestResourceStream("Launcher.Companion.Companion.html") ?? typeof(Program).Assembly.GetManifestResourceStream("LauncherCompanion.Companion.html")!;
    using var reader = new StreamReader(stream);
    return Results.Content(reader.ReadToEnd().Replace("{{CSRF}}", csrf), "text/html");
});
app.MapGet("/local/state", () =>
{
    var addresses = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up).SelectMany(n => n.GetIPProperties().UnicastAddresses).Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)).Select(a => "https://" + a.Address + ":17842").ToArray();
    lock (gate)
        return Results.Json(new
        {
            Pin = DateTime.UtcNow < pinUntil ? pin : "Expired. Generate a new code.",
            Fingerprint = fingerprint,
            Addresses = addresses,
            Connected = DateTime.UtcNow - lastSeen < TimeSpan.FromSeconds(12),
            Snapshot = snapshot
        }, new JsonSerializerOptions { PropertyNamingPolicy = null });
});
app.MapPost("/local/pairing", () => { ResetPair(); return Results.Ok(); });
app.MapPost("/pair", (CompanionPair request) =>
{
    lock (gate)
    {
        if (DateTime.UtcNow > pinUntil || failures >= 5 || token.Length > 0)
            return Results.StatusCode(403);
        if (!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(request.Pin), System.Text.Encoding.UTF8.GetBytes(pin)))
        {
            failures++;
            return Results.StatusCode(403);
        }
        token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        pinUntil = DateTime.MinValue;
        pin = "";
        return Results.Json(new CompanionPair { Token = token }, CompanionJson.Default.CompanionPair);
    }
});
app.MapPost("/api/snapshot", (CompanionSnapshot value) =>
{
    if (value.Notes.Count > 200 || value.Notes.Any(n => n.Text.Length > 20000 || n.Title.Length > 48))
        return Results.BadRequest();
    lock (gate)
        snapshot = value;
    return Results.Ok();
});
app.MapGet("/api/next", () => commands.TryDequeue(out var command) ? Results.Json(command, CompanionJson.Default.CompanionCommand) : Results.NoContent());
app.MapPost("/api/result", (CompanionResult value) =>
{
    if (value.Data.Length > 22_400_000 || value.Files.Count > 1000)
        return Results.BadRequest();
    // Keep only a bounded number of short-lived transfer results in memory.
    foreach (var old in resultTimes.Where(p => DateTime.UtcNow - p.Value > TimeSpan.FromMinutes(2)).Select(p => p.Key).ToArray())
    {
        results.TryRemove(old, out _);
        resultTimes.TryRemove(old, out _);
    }
    if (results.Count >= 8)
        return Results.StatusCode(429);
    results[value.Id] = value;
    resultTimes[value.Id] = DateTime.UtcNow;
    return Results.Ok();
});
app.MapPost("/local/command", (CompanionCommand command) =>
{
    if (command.Kind is not ("list" or "download" or "upload" or "note") || command.Data.Length > 22_400_000 || commands.Count >= 8)
        return Results.BadRequest();
    lock (gate)
    if (DateTime.UtcNow - lastSeen > TimeSpan.FromSeconds(12))
        return Results.Conflict("Keep Launcher open and connected on your phone.");
    command.Id = Guid.NewGuid().ToString("N");
    commands.Enqueue(command);
    return Results.Json(new
    {
        Id = command.Id
    }, new JsonSerializerOptions { PropertyNamingPolicy = null });
});
app.MapGet("/local/result/{id}", (string id) => results.TryRemove(id, out var result) ? Results.Json(result, CompanionJson.Default.CompanionResult) : Results.NoContent());
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine("Launcher Companion: http://127.0.0.1:17841\nKeep this window open. Close it to disconnect.");
    try
    {
        Process.Start(new ProcessStartInfo("http://127.0.0.1:17841") { UseShellExecute = true });
    }
    catch (System.ComponentModel.Win32Exception) { }
});
app.Run();
