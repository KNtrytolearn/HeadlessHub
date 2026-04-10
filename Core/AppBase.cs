using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HeadlessHub.Core;

/// <summary>
/// Result of a process launch attempt. All callers MUST inspect <c>Success</c>
/// and log <c>Error</c> when it is false.
/// </summary>
public record ProcessLaunchResult(
    bool Success,
    int? ProcessId = null,
    string? Error = null);

/// <summary>
/// Bounded circular buffer 閳?keeps the last <c>capacity</c> entries and discards
/// the oldest when full. Prevents unbounded string growth in process output capture.
/// </summary>
public sealed class CircularBuffer<T>(int capacity)
{
    private readonly T[] _buf = new T[capacity];
    private int _head;

    public void Append(T item)
    {
        _buf[_head] = item;
        _head = (_head + 1) % capacity;
    }

    /// <summary>Yields items in oldest閳姧ewest order.</summary>
    public IEnumerable<T> ReadAll()
    {
        var start = _head;
        for (var i = 0; i < capacity; i++)
            yield return _buf[(start + i) % capacity];
    }
}

public abstract class AppBase
{
    public const int MonitorCapacity = 600;

    // 閳光偓閳光偓 JSON-serialised state 閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓

    [JsonProperty("Name")]
    public string Name { get; protected init; } = string.Empty;

    [JsonProperty("CustomName")]
    public string CustomName { get; protected init; } = string.Empty;

    [JsonProperty("HelpUrl")]
    public string? HelpUrl { get; protected init; }

    [JsonProperty("ChangelogUrl")]
    public string? ChangelogUrl { get; protected init; }

    [JsonProperty("DescriptionShort")]
    public string? DescriptionShort { get; protected init; }

    [JsonProperty("DescriptionLong")]
    public string? DescriptionLong { get; protected init; }

    [JsonProperty("RunAsAdmin")]
    public bool RunAsAdmin { get; protected init; }

    [JsonProperty("Chmod")]
    public bool Chmod { get; protected init; }

    [JsonProperty("StartWindowState")]
    public ProcessWindowStyle StartWindowState { get; protected init; } = ProcessWindowStyle.Minimized;

    [JsonProperty("Configuration")]
    public Configuration? Configuration { get; protected init; }

    // 閳光偓閳光偓 Runtime state (not serialised) 閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓

    /// <summary>All reads and writes go through _stateLock to avoid TOCTOU races.</summary>
    [JsonIgnore]
    private bool _running;

    /// <summary>Thread-safe accessor.</summary>
    [JsonIgnore]
    public bool IsRunning
    {
        get { lock (_stateLock) { return _running; } }
    }

    [JsonIgnore]
    private readonly CircularBuffer<string> _stdoutBuf = new(MonitorCapacity);
    [JsonIgnore]
    private readonly CircularBuffer<string> _stderrBuf = new(MonitorCapacity);

    /// <summary>Most recent stdout lines (bounded, newest last).</summary>
    [JsonIgnore]
    public IEnumerable<string> StdoutLines => _stdoutBuf.ReadAll();

    /// <summary>Most recent stderr lines (bounded, newest last).</summary>
    [JsonIgnore]
    public IEnumerable<string> StderrLines => _stderrBuf.ReadAll();

    [JsonIgnore]
    private readonly object _stateLock = new();

    [JsonIgnore]
    private Process? _proc;

    [JsonIgnore]
    private protected Dictionary<string, string>? _runtimeArgs;

    /// <summary>Injected by ProfileManager after deserialisation.</summary>
    [JsonIgnore]
    protected internal ILogger? Logger { get; set; }

    // 閳光偓閳光偓 Constructor 閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓

    protected AppBase(
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
    {
        Name = name;
        CustomName = customName ?? name;
        HelpUrl = helpUrl;
        ChangelogUrl = changelogUrl;
        DescriptionShort = descriptionShort;
        DescriptionLong = descriptionLong;
        RunAsAdmin = runAsAdmin;
        Chmod = chmod;
        StartWindowState = startWindowState ?? ProcessWindowStyle.Minimized;
        Configuration = configuration;
    }

    // 閳光偓閳光偓 Public API 閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓

    /// <returns>
    /// A result record. Callers MUST check <c>Success</c> and log <c>Error</c> when false.
    /// </returns>
    public ProcessLaunchResult Run(Dictionary<string, string>? runtimeArgs = null)
    {
        lock (_stateLock)
        {
            if (_running)
                return new ProcessLaunchResult(false, Error: $"{Name} is already running");

            if (IsInstallable() && !IsInstalled())
                return new ProcessLaunchResult(false, Error: $"{Name} is not installed");

            _runtimeArgs = runtimeArgs;
            return _StartProcessUnsafe();
        }
    }

    /// <returns>
    /// Stops the current process and restarts with the given arguments.
    /// </returns>
    public ProcessLaunchResult ReRun(Dictionary<string, string>? runtimeArgs = null)
    {
        lock (_stateLock)
        {
            if (!_running)
                return new ProcessLaunchResult(false, Error: $"{Name} is not running");
            _StopUnsafe();
            _runtimeArgs = runtimeArgs;
            return _StartProcessUnsafe();
        }
    }

    /// <summary>
    /// Stops the process (if running). Safe to call from any thread.
    /// </summary>
    public void Close()
    {
        lock (_stateLock)
        {
            if (!_running) return;
            _StopUnsafe();
        }
        // Also kill any stray process by executable name (outside the lock 閳?I/O)
        var exePath = SetRunExecutable();
        if (!string.IsNullOrEmpty(exePath))
            Helper.KillProcess(exePath);
    }

    /// <summary>
    /// Returns true if the application binary is present on disk.
    /// </summary>
    public bool IsInstalled() =>
        !string.IsNullOrEmpty(SetRunExecutable()) && File.Exists(SetRunExecutable()!);

    // 閳光偓閳光偓 Abstract contracts 閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓

    public abstract bool Install();
    public abstract bool IsConfigurable();
    public abstract bool IsInstallable();
    protected abstract string? SetRunExecutable();

    // 閳光偓閳光偓 Private helpers 閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓

    /// <summary>
    /// Stops and cleans up the current process. Must be called while holding _stateLock.
    /// </summary>
    private void _StopUnsafe()
    {
        Debug.Assert(Monitor.IsEntered(_stateLock));

        try
        {
            // Try graceful shutdown via main window
            _proc?.CloseMainWindow();

            if (_proc != null && !_proc.HasExited)
            {
                var exited = _proc.WaitForExit(3000);
                if (!exited)
                    _proc.Kill(entireProcessTree: true);
            }

            _proc?.Dispose();
            _proc = null;
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Error during Close of {App}", Name);
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>
    /// Launches the subprocess. Must be called while holding _stateLock.
    /// Returns a result record 閳?never swallows errors silently.
    /// </summary>
    private ProcessLaunchResult _StartProcessUnsafe()
    {
        Debug.Assert(Monitor.IsEntered(_stateLock));

        var exePath = SetRunExecutable();
        if (string.IsNullOrEmpty(exePath))
            return new ProcessLaunchResult(false, Error: $"No executable found for {Name}");

        var args = _BuildArgs();
        if (args == null)
            return new ProcessLaunchResult(false, Error: $"Failed to compose arguments for {Name}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && Chmod)
            _EnsureExecutable(exePath);

        var psi = new ProcessStartInfo
        {
            FileName        = exePath,
            Arguments       = args,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty,
            WindowStyle     = StartWindowState,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow  = true,
            UseShellExecute = !(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RunAsAdmin)
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RunAsAdmin)
            psi.Verb = "runas";

        _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _proc.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            _stdoutBuf.Append(e.Data);
            Logger?.LogInformation("[{App}] {Line}", Name, e.Data);
        };

        _proc.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            _stderrBuf.Append(e.Data);
            Logger?.LogError("[{App}] {Line}", Name, e.Data);
        };

        // Keep _running in sync when the process dies
        _proc.Exited += (_, _) =>
        {
            lock (_stateLock)
            {
                _running = false;
                try { _proc?.Dispose(); } catch { }
                _proc = null;
            }
            Logger?.LogInformation("Process {App} (PID {Pid}) exited", Name, _proc?.Id);
        };

        try
        {
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            _running = true;
            Logger?.LogInformation("Started {App} with PID {Pid}", Name, _proc.Id);

            // Keep process object alive until Exited fires
            _ = Task.Run(delegate { try { _proc?.WaitForExit(); } catch { } });

            return new ProcessLaunchResult(true, _proc.Id);
        }
        catch (Exception ex)
        {
            _running = false;
            try { _proc?.Dispose(); } catch { }
            _proc = null;

            Logger?.LogError(ex, "Failed to start {App}", exePath);

            var error = ex switch
            {
                System.ComponentModel.Win32Exception w32 =>
                    $"Cannot start {Name}: {w32.Message} (code {w32.NativeErrorCode})",
                _ => $"Failed to start {Name}: {ex.Message}"
            };
            return new ProcessLaunchResult(false, Error: error);
        }
    }

    private string? _BuildArgs()
    {
        if (!IsConfigurable()) return string.Empty;
        if (Configuration == null) return string.Empty;

        try { return Configuration.BuildArgumentString(_runtimeArgs); }
        catch (ArgumentException ex)
        {
            Logger?.LogError(ex, "Argument validation failed for {App}", Name);
            return null;
        }
    }

    private void _EnsureExecutable(string path)
    {
        try
        {
            using var chmod = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName        = "chmod",
                    Arguments       = $"+x \"{path}\"",
                    UseShellExecute = false,
                    CreateNoWindow  = true
                }
            };
            chmod.Start();
            chmod.WaitForExit();
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "chmod failed for {Path}", path);
        }
    }
}
