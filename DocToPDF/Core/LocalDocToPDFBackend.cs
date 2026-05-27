namespace DocToPDF.Core;

public sealed class LocalDocToPDFBackend : IDocToPDFBackend
{
    private readonly PollingService _pollingService;

    public LocalDocToPDFBackend(PollingService pollingService)
    {
        _pollingService = pollingService;
        _pollingService.LogEvent += ForwardLog;
    }

    public bool IsRemote => false;

    public bool IsRunning => _pollingService.IsRunning;

    public event EventHandler<string>? LogEvent;

    public void StartTimer() => _pollingService.StartTimer();

    public void StopTimer() => _pollingService.StopTimer();

    public void RestartTimer() => _pollingService.RestartTimer();

    public void ProcessNow() => _pollingService.ProcessNow();

    public void ReloadSettings()
    {
        // Local mode uses in-memory settings from MainForm save.
    }

    public void Dispose()
    {
        _pollingService.LogEvent -= ForwardLog;
    }

    private void ForwardLog(object? sender, string message) =>
        LogEvent?.Invoke(sender, message);
}
