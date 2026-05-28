using System.IO.Pipes;
using System.Text;

namespace DocToPDF.Core;

/// <summary>
/// Segunda instância envia ACTIVATE pela pipe; a primeira exibe o painel.
/// Use junto com <see cref="SingleInstanceMutex"/>.
/// </summary>
public sealed class UiInstanceHost : IDisposable
{
    public const string PipeName = "DocToPDF.UI.SINGLETON.v1";

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenerTask;
    private Action? _onActivate;
    private readonly TaskCompletionSource _listenerReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private UiInstanceHost()
    {
        _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
        if (!_listenerReady.Task.Wait(TimeSpan.FromSeconds(5)))
            ServiceLog.Error("UI singleton: pipe de ativação não ficou pronto a tempo.");
    }

    public static bool TryActivateExisting(int attempts = 15, int delayMs = 150)
    {
        for (var i = 0; i < attempts; i++)
        {
            if (TryActivateOnce())
                return true;

            if (i < attempts - 1)
                Thread.Sleep(delayMs);
        }

        return false;
    }

    private static bool TryActivateOnce()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.None);

            pipe.Connect(800);
            using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);

            writer.WriteLine("ACTIVATE");
            return reader.ReadLine() == "OK";
        }
        catch
        {
            return false;
        }
    }

    public static UiInstanceHost Start(Action onActivate)
    {
        var host = new UiInstanceHost();
        host.SetActivateHandler(onActivate);
        return host;
    }

    public void SetActivateHandler(Action onActivate) => _onActivate = onActivate;

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = Ipc.NamedPipeHost.CreateServer(PipeName, PipeOptions.Asynchronous);
                _listenerReady.TrySetResult();

                await server.WaitForConnectionAsync(cancellationToken);

                var connected = server;
                server = null;
                _ = Task.Run(() => HandleClientAsync(connected), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(300, cancellationToken);
            }
            finally
            {
                server?.Dispose();
            }
        }
    }

    private void HandleClientAsync(NamedPipeServerStream server)
    {
        try
        {
            using (server)
            using (var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true))
            using (var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true) { AutoFlush = true })
            {
                var command = reader.ReadLine();
                if (command == "ACTIVATE")
                {
                    var handler = _onActivate;
                    if (handler != null)
                    {
                        handler();
                        writer.WriteLine("OK");
                    }
                    else
                    {
                        writer.WriteLine("ERR");
                    }
                }
                else
                {
                    writer.WriteLine("ERR");
                }
            }
        }
        catch
        {
            // Client disconnected.
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _listenerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore.
        }

        _cts.Dispose();
    }
}
