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

    /// <summary>
    /// StreamWriter não é thread-safe pra escritas concorrentes: o loop de comandos desta
    /// conexão (HandleClientAsync) e o broadcast de log (disparado por outra thread — o timer
    /// de polling) podem escrever ao mesmo tempo no mesmo writer. Esse semáforo serializa toda
    /// escrita de uma conexão, não importa qual caminho a originou.
    /// </summary>
    private sealed class ClientWriter(StreamWriter writer)
    {
        private readonly SemaphoreSlim _lock = new(1, 1);

        public async Task WriteLineAsync(string line)
        {
            await _lock.WaitAsync();
            try
            {
                await writer.WriteLineAsync(line);
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    private readonly ConcurrentDictionary<Guid, ClientWriter> _logSubscribers = new();
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
                using var rawWriter = new StreamWriter(server, Protocol, leaveOpen: true) { AutoFlush = true };
                var writer = new ClientWriter(rawWriter);

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    var response = await ExecuteCommandAsync(line.Trim(), clientId, writer);
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

    private async Task<string> ExecuteCommandAsync(string command, Guid clientId, ClientWriter writer)
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
            // Roda em segundo plano: ProcessNow pode chamar um serviço externo (ex.: OCR) e
            // demorar minutos. Bloquear aqui travaria a fila de comandos desta conexão (ex.:
            // GET_STATUS periódico do tray), estourando timeouts e dessincronizando o IPC.
            "PROCESS_NOW" => Run(() => { Task.Run(() => _polling.ProcessNow()); return "OK"; }),
            "RELOAD_SETTINGS" => Run(() =>
            {
                _polling.ReloadSettings();
                return "OK";
            }),
            "SUBSCRIBE_LOGS" => await SubscribeLogsAsync(clientId, writer),
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

    private async Task<string> SubscribeLogsAsync(Guid clientId, ClientWriter writer)
    {
        // Snapshot do histórico sob lock (protege _logHistory), mas escreve fora dele — a
        // escrita em si já é serializada por ClientWriter, não precisa do _broadcastLock.
        string[] history;
        lock (_broadcastLock)
        {
            history = _logHistory.ToArray();
            _logSubscribers[clientId] = writer;
        }

        foreach (var line in history)
        {
            try
            {
                await writer.WriteLineAsync(line);
            }
            catch
            {
                return "ERR Falha ao enviar histórico.";
            }
        }

        return "OK";
    }

    private void BroadcastLog(string message)
    {
        var line = $"LOG {message}";
        List<KeyValuePair<Guid, ClientWriter>> subscribers;

        lock (_broadcastLock)
        {
            _logHistory.AddLast(line);
            while (_logHistory.Count > LogHistoryMax)
                _logHistory.RemoveFirst();

            subscribers = _logSubscribers.ToList();
        }

        foreach (var (id, writer) in subscribers)
        {
            try
            {
                // ClientWriter serializa com o loop de comandos da mesma conexão — sem isso,
                // duas threads (esta e HandleClientAsync) escrevendo ao mesmo tempo no mesmo
                // StreamWriter causa corrupção ("Pipe is broken") ou trava indefinidamente.
                writer.WriteLineAsync(line).GetAwaiter().GetResult();
            }
            catch
            {
                _logSubscribers.TryRemove(id, out _);
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
