using DocToPDF.Core.Ipc;

namespace DocToPDF.Core;

public sealed class RemoteDocToPDFBackend : IDocToPDFBackend
{
    private readonly DocToPDFIpcClient _client;
    private bool _isRunning;

    public RemoteDocToPDFBackend(DocToPDFIpcClient client)
    {
        _client = client;
        _client.LogReceived += OnLogReceived;
        _isRunning = _client.GetIsRunning();
    }

    public bool IsRemote => true;

    public bool IsRunning => _isRunning;

    public event EventHandler<string>? LogEvent;

    public void StartTimer()
    {
        _client.SendStart();
        _isRunning = true;
        RaiseStateChanged();
    }

    public void StopTimer()
    {
        _client.SendStop();
        _isRunning = false;
        RaiseStateChanged();
    }

    public void RestartTimer()
    {
        _client.SendReloadSettings();
        _client.SendRestartTimer();
        _isRunning = _client.GetIsRunning();
        RaiseStateChanged();
    }

    public void ProcessNow() => _client.SendProcessNow();

    public void ReloadSettings() => _client.SendReloadSettings();

    public void RefreshStatus()
    {
        _isRunning = _client.GetIsRunning();
        RaiseStateChanged();
    }

    public void Dispose()
    {
        _client.LogReceived -= OnLogReceived;
        _client.Dispose();
    }

    private void OnLogReceived(object? sender, string message)
    {
        LogEvent?.Invoke(this, message);
        if (message.Contains("serviço iniciado", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("serviço parado", StringComparison.OrdinalIgnoreCase))
        {
            _isRunning = _client.GetIsRunning();
            RaiseStateChanged();
        }
    }

    private void RaiseStateChanged() =>
        LogEvent?.Invoke(this, string.Empty);
}
