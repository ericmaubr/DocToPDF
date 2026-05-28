using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace DocToPDF.Core.Ipc;

public sealed class DocToPDFIpcClient : IDisposable
{
    private readonly object _writeLock = new();
    private readonly ConcurrentQueue<TaskCompletionSource<string>> _pendingResponses = new();

    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    private readonly object _logGate = new();
    private readonly Queue<string> _earlyLogs = new();
    private EventHandler<string>? _logReceived;

    /// <summary>
    /// Linhas de LOG que chegam antes de haver assinante (ex.: o histórico enviado pelo
    /// servidor no SUBSCRIBE_LOGS) ficam em fila e são entregues ao primeiro assinante.
    /// </summary>
    public event EventHandler<string> LogReceived
    {
        add
        {
            lock (_logGate)
            {
                _logReceived += value;
                while (_earlyLogs.Count > 0)
                    value(this, _earlyLogs.Dequeue());
            }
        }
        remove
        {
            lock (_logGate)
                _logReceived -= value;
        }
    }

    public string? LastError { get; private set; }

    private static readonly object DiagGate = new();
    private static bool _diagHeaderWritten;

    internal static void Diag(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(Environment.ProcessPath);
            if (string.IsNullOrEmpty(dir))
                dir = AppContext.BaseDirectory;

            var path = Path.Combine(dir, "DocToPDF-ipc.log");
            lock (DiagGate)
            {
                if (!_diagHeaderWritten)
                {
                    _diagHeaderWritten = true;
                    File.AppendAllText(path,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} === UI iniciada | versão={DocToPDF.Core.AppVersion.Display} | exe={Environment.ProcessPath} | user={Environment.UserName}{Environment.NewLine}");
                }

                File.AppendAllText(path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Fallback para TEMP se o diretório do exe não for gravável.
            try
            {
                var fallback = Path.Combine(Path.GetTempPath(), "DocToPDF-ipc.log");
                File.AppendAllText(fallback,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch
            {
                // Ignore.
            }
        }
    }

    /// <summary>
    /// Uma tentativa rápida para saber se o pipe do serviço está ativo (não bloqueia a UI).
    /// </summary>
    public static bool TryQuickPing(int connectMs = 250)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                DocToPDFIpcServer.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            pipe.Connect(connectMs);
            using var writer = new StreamWriter(pipe, DocToPDFIpcServer.Protocol, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, DocToPDFIpcServer.Protocol, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

            writer.WriteLine("PING");
            var response = reader.ReadLine();
            var ok = response != null && response.StartsWith("OK", StringComparison.Ordinal);
            if (!ok)
                Diag($"QuickPing falhou: resposta='{response ?? "<null>"}'");
            return ok;
        }
        catch (Exception ex)
        {
            Diag($"QuickPing exceção: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public bool TryConnect(TimeSpan timeout)
    {
        try
        {
            _pipe = new NamedPipeClientStream(
                ".",
                DocToPDFIpcServer.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            Diag($"TryConnect: Connect({(int)timeout.TotalMilliseconds}ms)…");
            _pipe.Connect((int)timeout.TotalMilliseconds);
            Diag("TryConnect: pipe conectado; criando reader/writer.");
            _reader = new StreamReader(_pipe, DocToPDFIpcServer.Protocol, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            _writer = new StreamWriter(_pipe, DocToPDFIpcServer.Protocol, leaveOpen: true) { AutoFlush = true };

            StartReader();

            Diag("TryConnect: enviando SUBSCRIBE_LOGS.");
            var subscribe = SendCommand("SUBSCRIBE_LOGS", timeout);
            Diag($"TryConnect: SUBSCRIBE_LOGS retornou '{subscribe}'.");
            if (subscribe.StartsWith("OK", StringComparison.Ordinal))
            {
                LastError = null;
                return true;
            }

            LastError = $"SUBSCRIBE_LOGS → {subscribe}";
            Diag($"TryConnect: {LastError}");
            Dispose();
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
            Diag($"TryConnect exceção: {LastError}");
            Dispose();
            return false;
        }
    }

    public bool GetIsRunning() =>
        GetStatusResponse().Contains("RUNNING", StringComparison.OrdinalIgnoreCase);

    public string GetStatusResponse() =>
        SendCommand("GET_STATUS", TimeSpan.FromSeconds(2));

    public void SendStart() => SendCommand("START", TimeSpan.FromSeconds(2));

    public void SendStop() => SendCommand("STOP", TimeSpan.FromSeconds(2));

    public void SendRestartTimer() => SendCommand("RESTART_TIMER", TimeSpan.FromSeconds(2));

    public void SendProcessNow() => SendCommand("PROCESS_NOW", TimeSpan.FromSeconds(30));

    public void SendReloadSettings() => SendCommand("RELOAD_SETTINGS", TimeSpan.FromSeconds(2));

    private void StartReader()
    {
        _readCts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoopAsync(_readCts.Token));
    }

    private string SendCommand(string command, TimeSpan timeout)
    {
        if (_writer == null)
            return "ERR Desconectado.";

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingResponses.Enqueue(tcs);

        try
        {
            lock (_writeLock)
            {
                _writer.WriteLine(command);
            }
        }
        catch (Exception ex)
        {
            Diag($"SendCommand('{command}'): falha ao escrever ({ex.GetType().Name}: {ex.Message}).");
            DrainPending(tcs);
            return "ERR Conexão perdida.";
        }

        if (!tcs.Task.Wait(timeout))
        {
            Diag($"SendCommand('{command}'): TIMEOUT após {timeout.TotalMilliseconds}ms.");
            DrainPending(tcs);
            return "ERR Timeout.";
        }

        return tcs.Task.GetAwaiter().GetResult();
    }

    private void DrainPending(TaskCompletionSource<string> except)
    {
        while (_pendingResponses.TryDequeue(out var pending))
        {
            if (!ReferenceEquals(pending, except))
                pending.TrySetResult("ERR Descartado.");
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_reader == null)
            return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line == null)
                    break;

                if (line.StartsWith("LOG ", StringComparison.Ordinal))
                {
                    var payload = line[4..];
                    lock (_logGate)
                    {
                        if (_logReceived != null)
                            _logReceived.Invoke(this, payload);
                        else
                            _earlyLogs.Enqueue(payload);
                    }
                    continue;
                }

                if (_pendingResponses.TryDequeue(out var pending))
                    pending.TrySetResult(line);
            }
        }
        catch
        {
            while (_pendingResponses.TryDequeue(out var pending))
                pending.TrySetResult("ERR Conexão encerrada.");
        }
    }

    public void Dispose()
    {
        _readCts?.Cancel();
        try
        {
            _readTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore.
        }

        _readCts?.Dispose();
        _reader?.Dispose();
        _writer?.Dispose();
        _pipe?.Dispose();
    }
}
