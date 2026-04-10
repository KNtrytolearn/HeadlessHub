using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace HeadlessHub.Core;

/// <summary>
/// Broadcasts structured log messages to all connected WebSocket clients.
/// Registered as a <see cref="IHostedService"/> only — callers use it via DI
/// as a plain singleton (the IHostedService interface is satisfied by inheritance).
/// </summary>
public sealed class LogBroadcastService : IHostedService
{
    private readonly ILogger<LogBroadcastService> _logger;
    private readonly List<WebSocket> _clients = new();
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _cleanupTask;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public LogBroadcastService(ILogger<LogBroadcastService> logger)
    {
        _logger = logger;
    }

    // ── Public broadcast API ───────────────────────────────────────────────────

    public async Task BroadcastAsync(string message, LogLevel level = LogLevel.Information)
    {
        var payload = JsonSerializer.Serialize(new { timestamp = DateTime.UtcNow, level = level.ToString(), message }, JsonOpts);
        var buffer = Encoding.UTF8.GetBytes(payload);
        var segment = new ArraySegment<byte>(buffer);

        List<WebSocket> clients;
        lock (_lock)
        {
            clients = _clients.ToList();
        }

        var dead = new List<WebSocket>();

        foreach (var ws in clients)
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                    await ws.SendAsync(segment, WebSocketMessageType.Text, endOfMessage: true, _cts.Token);
                else
                    dead.Add(ws);   // closed or aborted
            }
            catch
            {
                dead.Add(ws);
            }
        }

        if (dead.Count != 0)
        {
            lock (_lock)
            {
                foreach (var d in dead)
                    _clients.Remove(d);
            }
        }
    }

    public void AddClient(WebSocket socket)
    {
        lock (_lock) { _clients.Add(socket); }
    }

    public void RemoveClient(WebSocket socket)
    {
        lock (_lock) { _clients.Remove(socket); }
    }

    public int ClientCount
    {
        get { lock (_lock) { return _clients.Count; } }
    }

    // ── IHostedService ─────────────────────────────────────────────────────────

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _cleanupTask = Task.Run(_CleanupLoop);
        return Task.CompletedTask;
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();

        List<WebSocket> clients;
        lock (_lock) { clients = _clients.ToList(); _clients.Clear(); }

        foreach (var ws in clients)
        {
            try { ws.Dispose(); } catch { }
        }

        return Task.CompletedTask;
    }

    /// <summary>Periodically removes WebSockets that are no longer open.</summary>
    private async Task _CleanupLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var dead = new List<WebSocket>();
            lock (_lock)
            {
                foreach (var ws in _clients)
                {
                    if (ws.State != WebSocketState.Open && ws.State != WebSocketState.Connecting)
                        dead.Add(ws);
                }
                foreach (var d in dead) _clients.Remove(d);
            }

            if (dead.Count != 0)
                _logger.LogDebug("Cleaned up {N} dead WebSocket clients", dead.Count);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
