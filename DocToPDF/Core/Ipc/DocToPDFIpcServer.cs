using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace DocToPDF.Core.Ipc;

public sealed class DocToPDFIpcServer : IDisposable
{
    public const string PipeName = "DocToPDF.IPC.v1";

    private readonly ConcurrentDictionary<Guid, StreamWriter> _logSubscribers = new();
    private readonly object _broadcastLock = new();
    private readonly object _lifecycleLock = new();
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private PollingService? _polling;
    private int _disposed;

    public void Start(PollingService polling)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        _polling = polling;
        _polling.LogEvent += OnPollingLog;

        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
        ServiceLog.Info("IPC server iniciado.");
    }

    private void OnPollingLog(object? sender, string message) =>
        BroadcastLog(message);

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = NamedPipeHost.CreateServer(PipeName);
                await server.WaitForConnectionAsync(cancellationToken);

                var connectedServer = server;
                server = null;
                _ = Task.Run(() => HandleClientAsync(connectedServer), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                ServiceLog.Error($"IPC listen: {ex.Message}");
                try
                {
                    await Task.Delay(500, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            finally
            {
                server?.Dispose();
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server)
    {
        var clientId = Guid.NewGuid();
        try
        {
            using (server)
            {
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var response = ExecuteCommand(line.Trim(), clientId, writer);
                    await writer.WriteLineAsync(response);
                }
            }
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"IPC client: {ex.Message}");
        }
        finally
        {
            _logSubscribers.TryRemove(clientId, out _);
        }
    }

    private string ExecuteCommand(string command, Guid clientId, StreamWriter writer)
    {
        if (_polling == null)
            return "ERR Serviço indisponível.";

        return command.ToUpperInvariant() switch
        {
            "PING" => "OK",
            "GET_STATUS" => _polling.IsRunning ? "OK RUNNING" : "OK STOPPED",
            "START" => Run(() => { _polling.StartTimer(); return "OK"; }),
            "STOP" => Run(() => { _polling.StopTimer(); return "OK"; }),
            "RESTART_TIMER" => Run(() => { _polling.RestartTimer(); return "OK"; }),
            "PROCESS_NOW" => Run(() => { _polling.ProcessNow(); return "OK"; }),
            "RELOAD_SETTINGS" => Run(() =>
            {
                _polling.ReloadSettings();
                return "OK";
            }),
            "SUBSCRIBE_LOGS" => SubscribeLogs(clientId, writer),
            _ => "ERR Comando desconhecido."
        };
    }

    private static string Run(Func<string> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            return $"ERR {ex.Message}";
        }
    }

    private string SubscribeLogs(Guid clientId, StreamWriter writer)
    {
        _logSubscribers[clientId] = writer;
        return "OK";
    }

    private void BroadcastLog(string message)
    {
        var line = $"LOG {message}";
        lock (_broadcastLock)
        {
            foreach (var (id, writer) in _logSubscribers)
            {
                try
                {
                    writer.WriteLine(line);
                }
                catch
                {
                    _logSubscribers.TryRemove(id, out _);
                }
            }
        }
    }

    public void StopListening()
    {
        lock (_lifecycleLock)
        {
            if (_disposed != 0)
                return;

            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Já cancelado.
            }
        }
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignore.
            }

            try
            {
                _listenerTask?.Wait(TimeSpan.FromSeconds(3));
            }
            catch
            {
                // Ignore shutdown timeout.
            }

            if (_polling != null)
                _polling.LogEvent -= OnPollingLog;

            _cts?.Dispose();
            _cts = null;
        }
    }
}
