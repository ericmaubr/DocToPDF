using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace DocToPDF.Core.Ipc;

public sealed class DocToPDFIpcClient : IDisposable
{
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    private readonly object _writeLock = new();
    private readonly ConcurrentQueue<TaskCompletionSource<string>> _pendingResponses = new();

    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private volatile bool _isRunning;

    public event EventHandler<string>? LogReceived;
    public event EventHandler? ConnectionLost;

    public bool IsConnected => _pipe?.IsConnected == true;

    public static bool TryQuickPing(int connectMs = 300)
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

            _pipe.Connect((int)Math.Clamp(timeout.TotalMilliseconds, 1, 30_000));
            _reader = new StreamReader(_pipe, Encoding.UTF8, leaveOpen: true);
            _writer = new StreamWriter(_pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            StartReader();

            var subscribe = SendCommand("SUBSCRIBE_LOGS", timeout);
            if (!subscribe.StartsWith("OK", StringComparison.Ordinal))
            {
                Dispose();
                return false;
            }

            RefreshRunningState();
            return true;
        }
        catch
        {
            Dispose();
            return false;
        }
    }

    public bool IsRunning => _isRunning;

    public bool TryRefreshRunningState()
    {
        var response = SendCommand("GET_STATUS", StatusTimeout);
        if (!response.StartsWith("OK", StringComparison.Ordinal))
        {
            if (response.Contains("Desconectado", StringComparison.OrdinalIgnoreCase) ||
                response.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            {
                ConnectionLost?.Invoke(this, EventArgs.Empty);
            }

            return false;
        }

        _isRunning = response.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    public void SendStart()
    {
        SendCommand("START", CommandTimeout);
        TryRefreshRunningState();
    }

    public void SendStop()
    {
        SendCommand("STOP", CommandTimeout);
        TryRefreshRunningState();
    }

    public void SendRestartTimer()
    {
        SendCommand("RELOAD_SETTINGS", CommandTimeout);
        SendCommand("RESTART_TIMER", CommandTimeout);
        TryRefreshRunningState();
    }

    public void SendProcessNow() => SendCommand("PROCESS_NOW", ProcessTimeout);

    public void SendReloadSettings() => SendCommand("RELOAD_SETTINGS", CommandTimeout);

    private void RefreshRunningState() => TryRefreshRunningState();

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
        catch
        {
            DrainPending(tcs);
            return "ERR Desconectado.";
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
        finally
        {
            ConnectionLost?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        try
        {
            _readCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore.
        }

        try
        {
            _readTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore.
        }

        _readCts?.Dispose();
        _readCts = null;
        _reader?.Dispose();
        _writer?.Dispose();
        _pipe?.Dispose();
        _reader = null;
        _writer = null;
        _pipe = null;
    }
}
