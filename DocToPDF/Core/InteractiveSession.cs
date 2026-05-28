using DocToPDF.Core.Ipc;

namespace DocToPDF.Core;

/// <summary>
/// Cria o backend da bandeja: remoto (serviço) ou local (standalone), sem misturar os dois.
/// </summary>
public sealed class InteractiveSession : IDisposable
{
    private readonly SettingsStore _settingsStore;
    private readonly bool _attachToService;
    private PollingService? _localPolling;

    public InteractiveSession(SettingsStore settingsStore, bool attachToService)
    {
        _settingsStore = settingsStore;
        _attachToService = attachToService;
    }

    public SettingsStore SettingsStore => _settingsStore;

    public bool AttachToService => _attachToService;

    /// <summary>True quando a UI deve falar só com o serviço (nunca polling local paralelo).</summary>
    public bool UsesServiceBackend { get; private set; }

    public IDocToPDFBackend CreateBackend()
    {
        if (_attachToService || TryDetectService())
        {
            UsesServiceBackend = true;
            return new DeferredRemoteBackend();
        }

        return CreateLocalBackend();
    }

    private static bool TryDetectService()
    {
        for (var i = 0; i < 15; i++)
        {
            if (DocToPDFIpcClient.TryQuickPing(400))
                return true;

            Thread.Sleep(300);
        }

        return false;
    }

    private LocalDocToPDFBackend CreateLocalBackend()
    {
        PollingService? polling = null;
        var fileProcessor = new FileProcessor(_settingsStore.Settings, message => polling!.Log(message));
        polling = new PollingService(_settingsStore, fileProcessor);
        _localPolling = polling;

        foreach (var message in ConfiguredDirectories.EnsureExist(_settingsStore.Settings))
            polling.Log(message);

        polling.StartTimer();
        polling.Log("DocToPDF — modo local (serviço não detectado).");

        return new LocalDocToPDFBackend(polling);
    }

    public void StopLocalProcessing() => _localPolling?.StopTimer();

    public void Dispose() => _localPolling?.Dispose();
}
