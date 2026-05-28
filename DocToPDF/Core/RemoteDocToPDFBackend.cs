using DocToPDF.Core.Ipc;

namespace DocToPDF.Core;

public sealed class RemoteDocToPDFBackend : IDocToPDFBackend
{
    private readonly DocToPDFIpcClient _client;
    private bool _isRunning;

    public RemoteDocToPDFBackend(DocToPDFIpcClient client, bool refreshStatusOnConnect = true)
    {
        _client = client;
        _client.LogReceived += OnLogReceived;
        if (refreshStatusOnConnect)
            RefreshStatus();
    }

    public bool IsRemote => true;

    public bool IsRunning => _isRunning;

    public event EventHandler<string>? LogEvent;

    public void StartTimer()
    {
        _client.SendStart();
        RefreshStatus();
    }

    public void StopTimer()
    {
        _client.SendStop();
        RefreshStatus();
    }

    public void RestartTimer()
    {
        _client.SendReloadSettings();
        _client.SendRestartTimer();
        RefreshStatus();
    }

    public void ProcessNow() => _client.SendProcessNow();

    public void ReloadSettings() => _client.SendReloadSettings();

    public void RefreshStatus()
    {
        var response = _client.GetStatusResponse();
        _isRunning = response.StartsWith("OK", StringComparison.Ordinal)
                     && response.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _client.LogReceived -= OnLogReceived;
        _client.Dispose();
    }

    private void OnLogReceived(object? sender, string message)
    {
        if (message.Contains("serviço iniciado", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("serviço parado", StringComparison.OrdinalIgnoreCase))
        {
            RefreshStatus();
        }

        LogEvent?.Invoke(this, message);
    }
}
