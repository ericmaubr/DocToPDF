namespace DocToPDF.Core;

public interface IDocToPDFBackend : IDisposable
{
    bool IsRemote { get; }

    bool IsRunning { get; }

    event EventHandler<string>? LogEvent;

    void StartTimer();

    void StopTimer();

    void RestartTimer();

    void ProcessNow();

    void ReloadSettings();
}
