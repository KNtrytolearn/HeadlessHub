using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HeadlessHub.Core;

/// <summary>
/// Stateless utilities safe to call from any context.
/// </summary>
public static class Helper
{
    /// <summary>Returns true if any file or sub-directory name starts with <paramref name="prefix"/>.</summary>
    public static bool DirectoryOrFileStartsWith(string path, string prefix)
    {
        if (!Directory.Exists(path)) return false;
        if (Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        return Directory.GetFiles(path).Any(f =>
            Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static long GetFileSizeByLocal(string path)
        => File.Exists(path) ? new FileInfo(path).Length : -2;

    /// <summary>Downloads a URL and returns content. Returns empty string on failure.</summary>
    public static async Task<string> AsyncHttpGet(string url, double timeoutSeconds = 10)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            var resp = await client.GetAsync(url);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : string.Empty;
        }
        catch { return string.Empty; }
    }

    /// <summary>Returns the directory containing the running executable.</summary>
    public static string GetAppBasePath()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory;
        return Path.GetDirectoryName(AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
    }

    /// <summary>Returns the user's home directory across all platforms.</summary>
    public static string GetUserDirectoryPath()
    {
        // Walk up from AppData to the user root
        var path = Directory.GetParent(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))?.FullName;

        if (Environment.OSVersion.Version.Major >= 6 && path != null)
            path = Directory.GetParent(path)?.ToString();

        return path ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>Removes a directory (optionally recreating it immediately).</summary>
    public static void RemoveDirectory(string path, bool createAfterRemove = false)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        if (createAfterRemove)
            Directory.CreateDirectory(path);
    }

    /// <summary>Extracts the filename component from a URL.</summary>
    public static string GetFileNameByUrl(string url) =>
        url.Split('/')[^1];

    /// <summary>Recursively searches <paramref name="path"/> for the first executable.</summary>
    public static string? SearchExecutable(string path)
    {
        if (!Directory.Exists(path)) return null;

        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { ".exe" }
            : Array.Empty<string>();

        return Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
            .FirstOrDefault(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return extensions.Length == 0 || extensions.Contains(ext);
            });
    }

    public static bool IsProcessRunning(int pid)
    {
        if (pid <= 0) return false;
        try { _ = Process.GetProcessById(pid); return true; }
        catch { return false; }
    }

    public static bool IsProcessRunning(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        name = Path.GetFileNameWithoutExtension(name);
        return Process.GetProcesses()
            .Any(p => p.ProcessName.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Kills a process by ID. Silently ignores failures.</summary>
    public static void KillProcess(int pid)
    {
        if (pid <= 0) return;
        try
        {
            var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: false);
        }
        catch { }
    }

    /// <summary>Kills all processes matching <paramref name="name"/>. Platform-aware.</summary>
    public static void KillProcess(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        name = Path.GetFileNameWithoutExtension(name);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var pids = _FindPidsOsX(name);
            foreach (var pid in pids.Reverse()) KillProcess(pid);
        }
        else
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { p.Kill(entireProcessTree: false); } catch { }
            }
        }
    }

    private static int[] _FindPidsOsX(string name)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName          = "/bin/bash",
                Arguments         = $"-c \"pgrep -d' ' {name}\"",
                RedirectStandardOutput = true,
                UseShellExecute    = false,
                CreateNoWindow    = true
            };
            using var proc = Process.Start(psi);
            var out_ = proc?.StandardOutput.ReadToEnd() ?? string.Empty;
            proc?.WaitForExit();
            return string.IsNullOrWhiteSpace(out_)
                ? []
                : out_.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries)
                       .Select(int.Parse).ToArray();
        }
        catch { return []; }
    }
}
