using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace DocToPDF.Core.Ipc;

public sealed class DocToPDFIpcServer : IDisposable
{
    public const string PipeName = "DocToPDF.IPC.v1";

    /// <summary>
    /// UTF-8 SEM BOM. Protocolo de linha: o BOM de <see cref="Encoding.UTF8"/> seria escrito
    /// no flush inicial (AutoFlush), travando em pipe com buffer 0 antes de qualquer leitura.
    /// </summary>
    public static readonly Encoding Protocol = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private const int LogHistoryMax = 500;

    private readonly ConcurrentDictionary<Guid, StreamWriter> _logSubscribers = new();
    private readonly object _broadcastLock = new();
    private readonly LinkedList<string> _logHistory = new();
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private PollingService? _polling;
    private int _disposed;

    public void Start(PollingService polling)
    {
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
                _ = Task.Run(() => HandleClientAsync(connectedServer, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ServiceLog.Error($"IPC listen: {ex.Message}");
                await Task.Delay(500, cancellationToken);
            }
            finally
            {
                server?.Dispose();
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid();
        try
        {
            using (server)
            {
                using var reader = new StreamReader(server, Protocol, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                using var writer = new StreamWriter(server, Protocol, leaveOpen: true) { AutoFlush = true };

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
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
        // Sob o mesmo lock do broadcast: reproduz o histórico e registra o assinante
        // atomicamente, sem perder nem duplicar linhas que cheguem nesse intervalo.
        lock (_broadcastLock)
        {
            foreach (var line in _logHistory)
            {
                try
                {
                    writer.WriteLine(line);
                }
                catch
                {
                    return "ERR Falha ao enviar histórico.";
                }
            }

            _logSubscribers[clientId] = writer;
        }

        return "OK";
    }

    private void BroadcastLog(string message)
    {
        var line = $"LOG {message}";
        lock (_broadcastLock)
        {
            _logHistory.AddLast(line);
            while (_logHistory.Count > LogHistoryMax)
                _logHistory.RemoveFirst();

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

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _cts?.Cancel();
        try
        {
            _listenerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore shutdown timeout.
        }

        if (_polling != null)
            _polling.LogEvent -= OnPollingLog;

        _cts?.Dispose();
    }
}
