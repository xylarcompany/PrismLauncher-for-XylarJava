using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XylarJavaLauncher;

internal static class ModrinthModpackInstaller
{
    public static async Task<string?> TryGetLatestMrpackUrlAsync(HttpClient http, string projectIdOrSlug, CancellationToken ct)
    {
        var id = Uri.EscapeDataString(projectIdOrSlug);
        var url = $"https://api.modrinth.com/v2/project/{id}/version?limit=80";
        using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var ver in doc.RootElement.EnumerateArray())
        {
            if (!ver.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
                continue;

            string? primaryMrpack = null;
            string? anyMrpack = null;

            foreach (var f in files.EnumerateArray())
            {
                var filename = f.TryGetProperty("filename", out var fe) ? fe.GetString() ?? "" : "";
                if (!filename.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileUrl = f.TryGetProperty("url", out var ue) ? ue.GetString() : null;
                if (string.IsNullOrWhiteSpace(fileUrl))
                    continue;

                var isPrimary = f.TryGetProperty("primary", out var pe) && pe.ValueKind == JsonValueKind.True;
                if (isPrimary)
                {
                    primaryMrpack = fileUrl;
                    break;
                }

                anyMrpack ??= fileUrl;
            }

            if (primaryMrpack != null)
                return primaryMrpack;
            if (anyMrpack != null)
                return anyMrpack;
        }

        return null;
    }

    public static async Task DownloadFileAsync(HttpClient http, string fileUrl, string destinationPath, CancellationToken ct)
    {
        using var response = await http.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fs, ct).ConfigureAwait(false);
    }

    public static void ExtractMrpackZip(string mrpackPath, string extractDirectory)
    {
        if (Directory.Exists(extractDirectory))
            Directory.Delete(extractDirectory, true);
        Directory.CreateDirectory(extractDirectory);
        ZipFile.ExtractToDirectory(mrpackPath, extractDirectory);
    }

    /// <summary>Folder that contains modrinth.index.json (root or nested inside the .mrpack extract).</summary>
    public static string? FindPackRoot(string extractDirectory)
    {
        if (!Directory.Exists(extractDirectory))
            return null;

        var direct = Path.Combine(extractDirectory, "modrinth.index.json");
        if (File.Exists(direct))
            return extractDirectory;

        try
        {
            var found = Directory
                .EnumerateFiles(extractDirectory, "modrinth.index.json", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (found != null)
                return Path.GetDirectoryName(found);
        }
        catch
        {
            // ignore IO errors (permissions, long paths)
        }

        return null;
    }

    public static bool LooksLikeExtractedPack(string directory)
        => FindPackRoot(directory) != null;

    /// <summary>
    /// Modrinth .mrpack only ships overrides + modrinth.index.json; every mod JAR is listed with CDN URLs and must be downloaded.
    /// </summary>
    public static async Task DownloadPackFilesFromIndexAsync(
        HttpClient http,
        string packRoot,
        IProgress<(int done, int total, string relativePath)>? progress,
        CancellationToken ct)
    {
        var indexPath = Path.Combine(packRoot, "modrinth.index.json");
        if (!File.Exists(indexPath))
            throw new FileNotFoundException("modrinth.index.json missing after extract.", indexPath);

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath, ct).ConfigureAwait(false));
        if (!doc.RootElement.TryGetProperty("files", out var filesEl) || filesEl.ValueKind != JsonValueKind.Array)
            return;

        var entries = new List<PackFileEntry>();
        foreach (var f in filesEl.EnumerateArray())
        {
            if (!f.TryGetProperty("path", out var pathEl))
                continue;
            var rel = pathEl.GetString();
            if (string.IsNullOrWhiteSpace(rel) || rel.Contains("..", StringComparison.Ordinal))
                continue;

            if (f.TryGetProperty("env", out var env))
            {
                if (env.TryGetProperty("client", out var c) &&
                    string.Equals(c.GetString(), "unsupported", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (!f.TryGetProperty("downloads", out var dl) || dl.ValueKind != JsonValueKind.Array)
                continue;
            string? url = null;
            foreach (var u in dl.EnumerateArray())
            {
                if (u.ValueKind == JsonValueKind.String)
                {
                    url = u.GetString();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(url))
                continue;

            string? sha512 = null;
            string? sha1 = null;
            if (f.TryGetProperty("hashes", out var hashes))
            {
                if (hashes.TryGetProperty("sha512", out var s512))
                    sha512 = s512.GetString();
                if (hashes.TryGetProperty("sha1", out var s1))
                    sha1 = s1.GetString();
            }

            entries.Add(new PackFileEntry(rel.Replace('/', Path.DirectorySeparatorChar), url, sha512, sha1));
        }

        var total = entries.Count;
        var done = 0;
        using var gate = new SemaphoreSlim(6, 6);

        async Task DownloadOneAsync(PackFileEntry entry)
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var dest = Path.GetFullPath(Path.Combine(packRoot, entry.RelativePath));
                var rootFull = Path.GetFullPath(packRoot);
                if (!dest.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                await using (var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                await using (var respStream = await http.GetStreamAsync(entry.Url, ct).ConfigureAwait(false))
                {
                    await respStream.CopyToAsync(fs, ct).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(entry.Sha512))
                {
                    await using var check = File.OpenRead(dest);
                    using var sha512 = SHA512.Create();
                    var hash = await sha512.ComputeHashAsync(check, ct).ConfigureAwait(false);
                    var hex = Convert.ToHexString(hash);
                    if (!hex.Equals(entry.Sha512.Trim(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"SHA-512 mismatch for {entry.RelativePath}");
                }
                else if (!string.IsNullOrWhiteSpace(entry.Sha1))
                {
                    await using var check = File.OpenRead(dest);
                    using var sha1 = SHA1.Create();
                    var hash = await sha1.ComputeHashAsync(check, ct).ConfigureAwait(false);
                    var hex = Convert.ToHexString(hash);
                    if (!hex.Equals(entry.Sha1.Trim(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"SHA-1 mismatch for {entry.RelativePath}");
                }

                var n = Interlocked.Increment(ref done);
                progress?.Report((n, total, entry.RelativePath));
            }
            finally
            {
                gate.Release();
            }
        }

        progress?.Report((0, total, "starting…"));
        await Task.WhenAll(entries.Select(DownloadOneAsync)).ConfigureAwait(false);
    }

    private sealed record PackFileEntry(string RelativePath, string Url, string? Sha512, string? Sha1);
}
