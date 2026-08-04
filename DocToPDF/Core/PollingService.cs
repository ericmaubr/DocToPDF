using DocToPDF.Models;

namespace DocToPDF.Core;

public sealed class PollingService : IDisposable
{
    /// <summary>
    /// Prefixo de mensagens de status ao vivo (fase do polling), distintas do histórico de
    /// log — quem recebe via <see cref="LogEvent"/> decide mostrar numa barra de status em
    /// vez de acumular no log. Atravessa IPC do mesmo jeito que qualquer outra linha de log.
    /// </summary>
    public const string StatusPrefix = "STATUS|";

    /// <summary>Espera antes da primeira verificação, pra dar tempo do sistema carregar.</summary>
    private const int StartupDelayMs = 5000;

    private readonly SettingsStore _settingsStore;
    private readonly FileProcessor _fileProcessor;
    private System.Threading.Timer? _timer;
    private readonly object _timerLock = new();
    private int _processingGate;

    public event EventHandler<string>? LogEvent;

    public bool IsRunning { get; private set; }

    public PollingService(SettingsStore settingsStore, FileProcessor fileProcessor)
    {
        _settingsStore = settingsStore;
        _fileProcessor = fileProcessor;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ReloadSettings();
        EnsureConfiguredDirectories();
        StartTimer();
        IsRunning = true;
        Log("DocToPDF — serviço iniciado.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopTimer();
        IsRunning = false;
        Log("DocToPDF — serviço parado.");
        return Task.CompletedTask;
    }

    public void ReloadSettings() => _settingsStore.Load();

    private void EnsureConfiguredDirectories()
    {
        foreach (var message in ConfiguredDirectories.EnsureExist(_settingsStore.Settings))
            Log(message);
    }

    public void StartTimer()
    {
        lock (_timerLock)
        {
            _timer?.Dispose();
            var intervalMs = Math.Max(1, _settingsStore.Settings.PollingIntervalSeconds) * 1000;
            _timer = new System.Threading.Timer(OnTimerCallback, null, StartupDelayMs, intervalMs);
            IsRunning = true;
        }

        Log(StatusPrefix + "Sistema iniciando...");
    }

    public void StopTimer()
    {
        lock (_timerLock)
        {
            _timer?.Dispose();
            _timer = null;
            IsRunning = false;
        }
    }

    public void RestartTimer()
    {
        ReloadSettings();
        if (IsRunning)
            StartTimer();
    }

    public void ProcessNow()
    {
        if (Interlocked.CompareExchange(ref _processingGate, 1, 0) != 0)
        {
            Log("Processamento já em andamento — ignorando novo disparo.");
            return;
        }

        Log(StatusPrefix + "Verificando arquivos...");
        try
        {
            ReloadSettings();
            _fileProcessor.ProcessAll();
        }
        catch (Exception ex)
        {
            Log($"❌ Erro ao processar — {ex.Message}");
            ServiceLog.Error($"ProcessNow: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _processingGate, 0);
            Log(StatusPrefix + "Aguardando próxima verificação.");
        }
    }

    private void OnTimerCallback(object? state)
    {
        // Evita rodar ProcessAll em paralelo com um ciclo anterior ainda em andamento
        // (ex.: padronização de PDF via OCR mais lenta que o intervalo de polling) — isso
        // enviaria requisições concorrentes ao conta-tools-pdf, que roda como processo único.
        if (Interlocked.CompareExchange(ref _processingGate, 1, 0) != 0)
            return;

        Log(StatusPrefix + "Verificando arquivos...");
        try
        {
            ReloadSettings();
            _fileProcessor.ProcessAll();
        }
        catch (Exception ex)
        {
            Log($"❌ Erro no polling — {ex.Message}");
            ServiceLog.Error($"Timer: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _processingGate, 0);
            Log(StatusPrefix + "Aguardando próxima verificação.");
        }
    }

    public void Log(string message)
    {
        if (message.StartsWith(StatusPrefix, StringComparison.Ordinal))
        {
            LogEvent?.Invoke(this, message);
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        string formatted;

        if (message.StartsWith('✅'))
            formatted = $"✅ {timestamp} — {message[1..].TrimStart()}";
        else if (message.StartsWith('❌'))
            formatted = $"❌ {timestamp} — {message[1..].TrimStart()}";
        else
            formatted = $"{timestamp} — {message}";

        LogEvent?.Invoke(this, formatted);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
