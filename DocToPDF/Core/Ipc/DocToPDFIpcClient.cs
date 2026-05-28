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

    public event EventHandler<string>? LogReceived;

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
            using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);

            writer.WriteLine("PING");
            var response = reader.ReadLine();
            return response != null && response.StartsWith("OK", StringComparison.Ordinal);
        }
        catch
        {
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

            _pipe.Connect((int)timeout.TotalMilliseconds);
            _reader = new StreamReader(_pipe, Encoding.UTF8, leaveOpen: true);
            _writer = new StreamWriter(_pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            StartReader();

            var subscribe = SendCommand("SUBSCRIBE_LOGS", timeout);
            return subscribe.StartsWith("OK", StringComparison.Ordinal);
        }
        catch
        {
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

        lock (_writeLock)
        {
            _writer.WriteLine(command);
        }

        if (!tcs.Task.Wait(timeout))
        {
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
                    LogReceived?.Invoke(this, line[4..]);
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
