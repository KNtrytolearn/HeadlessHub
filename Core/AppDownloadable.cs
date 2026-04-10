using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SharpCompress.Readers;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace HeadlessHub.Core;

/// <summary>
/// An app that downloads and extracts its binary from a URL at startup
/// (if the binary is missing or outdated).
/// </summary>
public class AppDownloadable : AppBase
{
    // ── JSON state ─────────────────────────────────────────────────────────────

    [JsonProperty("DownloadUrl")]
    public string DownloadUrl { get; }

    // ── Paths (derived, not serialised) ───────────────────────────────────────

    private string AppDir => Path.Combine(Helper.GetAppBasePath(), Name);
    private string ArchivePath => Path.Combine(AppDir, Helper.GetFileNameByUrl(DownloadUrl));

    // ── Constructor ───────────────────────────────────────────────────────────

    public AppDownloadable(
        string downloadUrl,
        string name,
        string? customName = null,
        string? helpUrl = null,
        string? changelogUrl = null,
        string? descriptionShort = null,
        string? descriptionLong = null,
        bool runAsAdmin = false,
        bool chmod = true,
        ProcessWindowStyle? startWindowState = null,
        Configuration? configuration = null)
        : base(name, customName, helpUrl, changelogUrl,
               descriptionShort, descriptionLong, runAsAdmin, chmod, startWindowState, configuration)
    {
        DownloadUrl = downloadUrl;
    }

    // ── Abstract overrides ────────────────────────────────────────────────────

    public override bool IsConfigurable() => Configuration != null;
    public override bool IsInstallable() => true;

    protected override string? SetRunExecutable() => Helper.SearchExecutable(AppDir);

    /// <summary>Returns the absolute path to the discovered executable, or null.</summary>
    public string? GetExecutablePath() => SetRunExecutable();

    // ── Install ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads and extracts the binary if it is missing or the remote file
    /// has a different size. Does NOT auto-start the app — callers are
    /// responsible for that decision.
    /// </summary>
    /// <returns>
    /// true  — a download was performed and the binary is now available<br/>
    /// false — nothing was downloaded (already up-to-date, or my_version dir exists)
    /// </returns>
    public override bool Install()
    {
        // Guard: if the user has a local my_version/ override, skip everything
        if (Helper.DirectoryOrFileStartsWith(AppDir, "my_version"))
        {
            Logger?.LogDebug("Skipping download for {App}: my_version/ override exists", Name);
            return false;
        }

        var localSize   = Helper.GetFileSizeByLocal(ArchivePath);
        var remoteSize  = _FetchRemoteFileSize();

        if (localSize > 0 && remoteSize > 0 && localSize == remoteSize)
        {
            Logger?.LogDebug("Skipping download for {App}: size matches ({N} bytes)", Name, localSize);
            return false;
        }

        Logger?.LogInformation("Downloading {App} from {Url}", Name, DownloadUrl);

        try
        {
            // Clean and recreate the app directory
            Helper.RemoveDirectory(AppDir, createAfterRemove: true);

            // Download
            _DownloadSync(DownloadUrl, ArchivePath);

            // Extract
            var ext = Path.GetExtension(ArchivePath).ToLowerInvariant();
            switch (ext)
            {
                case ".zip":
                    System.IO.Compression.ZipFile.ExtractToDirectory(ArchivePath, AppDir);
                    break;
                case ".gz":
                case ".tar":
                case ".tar.gz":
                case ".tgz":
                    _ExtractTarGz(ArchivePath, AppDir);
                    break;
                default:
                    Logger?.LogWarning("Unknown archive extension '{Ext}' for {App}", ext, Name);
                    break;
            }

            Logger?.LogInformation("Downloaded {App} successfully", Name);
            return true;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to download {App}", Name);
            Helper.RemoveDirectory(AppDir, createAfterRemove: false);
            return false;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static long _FetchRemoteFileSize()
    {
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var client  = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            using var resp = client.GetAsync(
                DownloadUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            return resp.Content.Headers.ContentLength ?? -1;
        }
        catch { return -1; }
    }

    /// <summary>Synchronous wrapper so Install() can remain non-async.</summary>
    private static void _DownloadSync(string url, string dest)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = true };
        using var client  = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        using var resp = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? -1L;
        var downloaded = 0L;

        using var netStream   = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var fileStream  = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);

        var buf = new byte[81920];
        int n;
        while ((n = netStream.ReadAsync(buf).GetAwaiter().GetResult()) > 0)
        {
            fileStream.Write(buf, 0, n);
            downloaded += n;
        }
    }

    private static void _ExtractTarGz(string archive, string dest)
    {
        using var stream  = File.OpenRead(archive);
        using var reader = ReaderFactory.Open(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryToDirectory(dest,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
            }
        }
    }
}
