using System.IO.Pipes;
using System.Text;

namespace DocToPDF.Core.Ipc;

public sealed class DocToPDFIpcClient : IDisposable
{
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    public event EventHandler<string>? LogReceived;

    public static bool IsServerAvailable(int attempts = 5, int delayMs = 400)
    {
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                using var client = new DocToPDFIpcClient();
                if (client.TryConnect(TimeSpan.FromMilliseconds(1500)))
                    return true;
            }
            catch
            {
                // Retry.
            }

            if (i < attempts - 1)
                Thread.Sleep(delayMs);
        }

        return false;
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

            var subscribe = SendCommand("SUBSCRIBE_LOGS");
            if (!subscribe.StartsWith("OK", StringComparison.Ordinal))
                return false;

            _readCts = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadLoopAsync(_readCts.Token));
            return true;
        }
        catch
        {
            Dispose();
            return false;
        }
    }

    public bool GetIsRunning()
    {
        var response = SendCommand("GET_STATUS");
        return response.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    public void SendStart() => SendCommand("START");

    public void SendStop() => SendCommand("STOP");

    public void SendRestartTimer() => SendCommand("RESTART_TIMER");

    public void SendProcessNow() => SendCommand("PROCESS_NOW");

    public void SendReloadSettings() => SendCommand("RELOAD_SETTINGS");

    private string SendCommand(string command)
    {
        if (_writer == null || _reader == null)
            return "ERR Desconectado.";

        lock (_writer)
        {
            _writer.WriteLine(command);
            return _reader.ReadLine() ?? "ERR Sem resposta.";
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
                    LogReceived?.Invoke(this, line[4..]);
            }
        }
        catch
        {
            // Disconnected.
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
