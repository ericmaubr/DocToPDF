using DocToPDF.Core.Ipc;

namespace DocToPDF.Core;

/// <summary>
/// Exibe a interface imediatamente e conecta ao serviço em segundo plano.
/// </summary>
public sealed class DeferredRemoteBackend : IDocToPDFBackend
{
    private readonly DocToPDFIpcClient _client = new();
    private readonly object _sync = new();
    private RemoteDocToPDFBackend? _connected;
    private bool _connecting = true;

    public DeferredRemoteBackend()
    {
        _ = Task.Run(ConnectLoopAsync);
    }

    public bool IsRemote => true;

    public bool IsConnected
    {
        get
        {
            lock (_sync)
                return _connected != null;
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
                return _connected?.IsRunning ?? false;
        }
    }

    public event EventHandler<string>? LogEvent;

    public void StartTimer() => WithConnected(b => b.StartTimer());

    public void StopTimer() => WithConnected(b => b.StopTimer());

    public void RestartTimer() => WithConnected(b => b.RestartTimer());

    public void ProcessNow() => WithConnected(b => b.ProcessNow());

    public void ReloadSettings() => WithConnected(b => b.ReloadSettings());

    public void RefreshStatus()
    {
        lock (_sync)
            _connected?.RefreshStatus();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_connected != null)
            {
                _connected.LogEvent -= OnConnectedLog;
                _connected.Dispose();
                _connected = null;
            }
            else
                _client.Dispose();
        }
    }

    private async Task ConnectLoopAsync()
    {
        const int maxAttempts = 25;

        for (var i = 0; i < maxAttempts; i++)
        {
            if (_client.TryConnect(TimeSpan.FromMilliseconds(400)))
            {
                AttachConnectedBackend();
                return;
            }

            if (i < maxAttempts - 1)
                await Task.Delay(200);
        }

        _connecting = false;
        RaiseLog(
            "⚠ Não foi possível conectar ao serviço DocToPDF. " +
            "Verifique em services.msc se o serviço está em execução.");
    }

    private void AttachConnectedBackend()
    {
        RemoteDocToPDFBackend remote;
        lock (_sync)
        {
            remote = new RemoteDocToPDFBackend(_client, refreshStatusOnConnect: false);
            _connected = remote;
            _connected.LogEvent += OnConnectedLog;
        }

        _connecting = false;
        remote.RefreshStatus();
        RaiseLog("Conectado ao serviço DocToPDF.");
    }

    private void OnConnectedLog(object? sender, string message) => RaiseLog(message);

    private void WithConnected(Action<RemoteDocToPDFBackend> action)
    {
        lock (_sync)
        {
            if (_connected != null)
            {
                action(_connected);
                return;
            }
        }

        if (_connecting)
            RaiseLog("Aguardando conexão com o serviço...");
        else
            RaiseLog("Serviço DocToPDF indisponível.");
    }

    private void RaiseLog(string message) => LogEvent?.Invoke(this, message);
}
