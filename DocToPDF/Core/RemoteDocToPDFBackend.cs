using DocToPDF.Core.Ipc;

namespace DocToPDF.Core;

public sealed class RemoteDocToPDFBackend : IDocToPDFBackend
{
    private readonly DocToPDFIpcClient _client;

    public RemoteDocToPDFBackend(DocToPDFIpcClient client, bool refreshStatusOnConnect = true)
    {
        _client = client;
        _client.LogReceived += OnLogReceived;
        _client.ConnectionLost += OnConnectionLost;

        if (refreshStatusOnConnect)
            _client.TryRefreshRunningState();
    }

    public bool IsRemote => true;

    public bool IsRunning => _client.IsRunning;

    public event EventHandler<string>? LogEvent;
    public event EventHandler? ConnectionLost;

    public void StartTimer()
    {
        _client.SendStart();
    }

    public void StopTimer()
    {
        _client.SendStop();
    }

    public void RestartTimer()
    {
        _client.SendRestartTimer();
    }

    public void ProcessNow() => _client.SendProcessNow();

    public void ReloadSettings() => _client.SendReloadSettings();

    /// <summary>Não bloquear a UI — use <see cref="TryRefreshStatus"/> em thread de fundo.</summary>
    public void RefreshStatus() => _client.TryRefreshRunningState();

    public bool TryRefreshStatus() => _client.TryRefreshRunningState();

    public void Dispose()
    {
        _client.LogReceived -= OnLogReceived;
        _client.ConnectionLost -= OnConnectionLost;
        _client.Dispose();
    }

    private void OnLogReceived(object? sender, string message)
    {
        if (message.Contains("serviço iniciado", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("serviço parado", StringComparison.OrdinalIgnoreCase))
        {
            _client.TryRefreshRunningState();
        }

        LogEvent?.Invoke(this, message);
    }

    private void OnConnectionLost(object? sender, EventArgs e) =>
        ConnectionLost?.Invoke(this, EventArgs.Empty);
}
