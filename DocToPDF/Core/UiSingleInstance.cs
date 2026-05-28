using System.IO.Pipes;
using System.Text;

namespace DocToPDF.Core;

public sealed class UiInstanceHost : IDisposable
{
    public const string PipeName = "DocToPDF.UI.SINGLETON.v1";

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenerTask;
    private Action? _onActivate;

    private UiInstanceHost()
    {
        _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
    }

    public static bool TryActivateExisting()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.None);

            pipe.Connect(400);
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

    public static UiInstanceHost Start()
    {
        return new UiInstanceHost();
    }

    public void SetActivateHandler(Action onActivate) => _onActivate = onActivate;

    private static NamedPipeServerStream CreateServer() =>
        Ipc.NamedPipeHost.CreateServer(PipeName, PipeOptions.Asynchronous);

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = CreateServer();
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
                    _onActivate?.Invoke();
                    writer.WriteLine("OK");
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
