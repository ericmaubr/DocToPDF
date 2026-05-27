using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace DocToPDF.Core.Ipc;

public sealed class DocToPDFIpcServer : IDisposable
{
    public const string PipeName = "DocToPDF.IPC.v1";

    private readonly ConcurrentDictionary<Guid, StreamWriter> _logSubscribers = new();
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private PollingService? _polling;

    public void Start(PollingService polling)
    {
        _polling = polling;
        _polling.LogEvent += OnPollingLog;

        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
    }

    private void OnPollingLog(object? sender, string message) =>
        BroadcastLog(message);

    private static NamedPipeServerStream CreatePipeServer()
    {
        var server = new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                server.SetAccessControl(CreatePipeSecurity());
            }
            catch
            {
                // Keep default ACL if adjustment fails.
            }
        }

        return server;
    }

    private static PipeSecurity CreatePipeSecurity()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        return security;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = CreatePipeServer();
                await server.WaitForConnectionAsync(cancellationToken);

                var connectedServer = server;
                server = null;
                _ = Task.Run(() => HandleClientAsync(connectedServer, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
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
            await using (server.ConfigureAwait(false))
            {
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    var response = ExecuteCommand(line.Trim(), clientId, writer);
                    await writer.WriteLineAsync(response);
                }
            }
        }
        catch
        {
            // Client disconnected.
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

    public void Dispose()
    {
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
