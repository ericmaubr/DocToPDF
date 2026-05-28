using DocToPDF.Core.Ipc;

namespace DocToPDF.Core;

/// <summary>
/// Exibe a interface imediatamente e conecta ao serviço em segundo plano.
/// </summary>
public sealed class DeferredRemoteBackend : IDocToPDFBackend
{
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
        }
    }

    private async Task ConnectLoopAsync()
    {
        const int maxAttempts = 25;
        string? lastError = null;

        DocToPDFIpcClient.Diag($"ConnectLoop: iniciando, maxAttempts={maxAttempts}");

        for (var i = 0; i < maxAttempts; i++)
        {
            var client = new DocToPDFIpcClient();
            if (client.TryConnect(TimeSpan.FromMilliseconds(400)))
            {
                DocToPDFIpcClient.Diag($"ConnectLoop: conectado na tentativa {i + 1}.");
                AttachConnectedBackend(client);
                return;
            }
            lastError = client.LastError;
            client.Dispose();

            if (i == 0 && lastError != null)
                RaiseLog($"Conectando ao serviço… (1ª tentativa: {lastError})");

            if (i < maxAttempts - 1)
                await Task.Delay(200);
        }

        DocToPDFIpcClient.Diag($"ConnectLoop: FALHOU após {maxAttempts} tentativas. lastError={lastError ?? "<null>"}");
        _connecting = false;
        RaiseLog(
            "⚠ Não foi possível conectar ao serviço DocToPDF. " +
            $"Último erro: {lastError ?? "desconhecido"}. " +
            "Verifique services.msc ou execute DocToPDF.exe sem o serviço (modo local).");
    }

    private void AttachConnectedBackend(DocToPDFIpcClient client)
    {
        RemoteDocToPDFBackend remote;
        lock (_sync)
        {
            remote = new RemoteDocToPDFBackend(client, refreshStatusOnConnect: false);
            _connected = remote;
            _connected.LogEvent += OnConnectedLog;
            // Só agora inicia o recebimento: o histórico drenado já encontra o handler conectado.
            remote.StartReceivingLogs();
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
