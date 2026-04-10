using System.Runtime.InteropServices;

namespace HeadlessHub.Core;

public static class PlatformHelper
{
    public static bool IsLinux   => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsMacOS   => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>Well-known architectures only — throws on anything unexpected.</summary>
    public static Architecture Architecture
    {
        get
        {
            var a = RuntimeInformation.ProcessArchitecture;
            return a switch
            {
                Architecture.X64  => Architecture.X64,
                Architecture.X86  => Architecture.X86,
                Architecture.Arm  => Architecture.Arm,
                Architecture.Arm64 => Architecture.Arm64,
                _ => throw new PlatformNotSupportedException(
                    "Unsupported architecture: " + a)
            };
        }
    }

    public static string ArchName => Architecture switch
    {
        Architecture.X64   => "x64",
        Architecture.X86   => "x86",
        Architecture.Arm   => "arm",
        Architecture.Arm64 => "arm64",
        _                  => "unknown"
    };

    public static string OSName => (IsLinux, IsWindows, IsMacOS) switch
    {
        (true,  _,  _) => "linux",
        (_,  true,  _) => "windows",
        (_,   _,  true) => "macos",
        _                => "unknown"
    };

    private const string CallerUrlBase  = "https://github.com/lbormann/darts-caller/releases/download/v2.20.3";
    private const string WledUrlBase  = "https://github.com/lbormann/darts-wled/releases/download/v1.10.4";
    private const string PixelitUrlBase = "https://github.com/lbormann/darts-pixelit/releases/download/v1.3.1";

    public static string? GetCallerDownloadUrl()
    {
        return (OSName, ArchName) switch
        {
            ("linux",   "arm64") => $"{CallerUrlBase}/darts-caller-arm64",
            ("linux",   "x64")  => $"{CallerUrlBase}/darts-caller",
            ("windows", "x64")  => $"{CallerUrlBase}/darts-caller.exe",
            ("macos",   "arm64")=> $"{CallerUrlBase}/darts-caller-mac",
            ("macos",   "x64")  => $"{CallerUrlBase}/darts-caller-macx64",
            _                    => null
        };
    }

    public static string? GetWledDownloadUrl()
    {
        return (OSName, ArchName) switch
        {
            ("linux",   "arm64") => $"{WledUrlBase}/darts-wled-arm64",
            ("linux",   "x64")  => $"{WledUrlBase}/darts-wled",
            ("windows", "x64")  => $"{WledUrlBase}/darts-wled.exe",
            ("macos",   "arm64")=> $"{WledUrlBase}/darts-wled-mac",
            ("macos",   "x64")  => $"{WledUrlBase}/darts-wled-mac64",
            _                    => null
        };
    }

    public static string? GetPixelitDownloadUrl()
    {
        return (OSName, ArchName) switch
        {
            ("linux",   "arm64") => $"{PixelitUrlBase}/darts-pixelit-arm64",
            ("linux",   "x64")  => $"{PixelitUrlBase}/darts-pixelit",
            ("windows", "x64")  => $"{PixelitUrlBase}/darts-pixelit.exe",
            ("macos",   "arm64")=> $"{PixelitUrlBase}/darts-pixelit-mac",
            ("macos",   "x64")  => $"{PixelitUrlBase}/darts-pixelit-mac",
            _                    => null
        };
    }
}
