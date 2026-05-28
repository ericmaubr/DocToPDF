using DocToPDF.Core.Ipc;

namespace DocToPDF.Core;

public sealed class DeferredRemoteBackend : IDocToPDFBackend
{
    private readonly DocToPDFIpcClient _client = new();
    private readonly object _sync = new();
    private readonly CancellationTokenSource _connectCts = new();
    private RemoteDocToPDFBackend? _connected;
    private bool _connecting = true;
    private bool _connectFailed;

    public DeferredRemoteBackend()
    {
        _ = Task.Run(() => ConnectLoopAsync(_connectCts.Token));
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

    public bool IsConnecting => _connecting;

    public bool ConnectFailed => _connectFailed;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
                return _connected?.IsRunning ?? false;
        }
    }

    public event EventHandler<string>? LogEvent;
    public event EventHandler? ConnectionStateChanged;

    public void StartTimer() => WithConnected(b => b.StartTimer());

    public void StopTimer() => WithConnected(b => b.StopTimer());

    public void RestartTimer() => WithConnected(b => b.RestartTimer());

    public void ProcessNow() => WithConnected(b => b.ProcessNow());

    public void ReloadSettings() => WithConnected(b => b.ReloadSettings());

    public void RefreshStatus()
    {
        lock (_sync)
            _connected?.TryRefreshStatus();
    }

    public void Dispose()
    {
        _connectCts.Cancel();
        lock (_sync)
        {
            if (_connected != null)
            {
                _connected.LogEvent -= OnConnectedLog;
                _connected.ConnectionLost -= OnConnectedConnectionLost;
                _connected.Dispose();
                _connected = null;
            }
            else
                _client.Dispose();
        }

        _connectCts.Dispose();
    }

    private async Task ConnectLoopAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 20;

        for (var i = 0; i < maxAttempts && !cancellationToken.IsCancellationRequested; i++)
        {
            if (_client.TryConnect(TimeSpan.FromMilliseconds(500)))
            {
                AttachConnectedBackend();
                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        _connecting = false;
        _connectFailed = true;
        RaiseLog(
            "⚠ Não foi possível conectar ao serviço DocToPDF. " +
            "Verifique se o serviço está em execução em services.msc.");
        NotifyConnectionStateChanged();
    }

    private void AttachConnectedBackend()
    {
        RemoteDocToPDFBackend remote;
        lock (_sync)
        {
            remote = new RemoteDocToPDFBackend(_client, refreshStatusOnConnect: false);
            _connected = remote;
            _connected.LogEvent += OnConnectedLog;
            _connected.ConnectionLost += OnConnectedConnectionLost;
        }

        _connecting = false;
        _connectFailed = false;
        remote.TryRefreshStatus();
        RaiseLog("Conectado ao serviço DocToPDF.");
        NotifyConnectionStateChanged();
    }

    private void OnConnectedConnectionLost(object? sender, EventArgs e)
    {
        _connecting = false;
        _connectFailed = true;
        RaiseLog("⚠ Conexão com o serviço perdida.");
        NotifyConnectionStateChanged();
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
            RaiseLog("Aguardando conexão com o serviço…");
        else
            RaiseLog("Serviço DocToPDF indisponível.");
    }

    private void RaiseLog(string message) => LogEvent?.Invoke(this, message);

    private void NotifyConnectionStateChanged() =>
        ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
}
