using DocToPDF.Models;
using Microsoft.Extensions.Hosting;

namespace DocToPDF.Core;

public sealed class PollingService : IHostedService, IDisposable
{
    private readonly AppSettings _settings;
    private readonly FileProcessor _fileProcessor;
    private System.Threading.Timer? _timer;
    private readonly object _timerLock = new();

    public event EventHandler<string>? LogEvent;

    public bool IsRunning { get; private set; }

    public PollingService(AppSettings settings, FileProcessor fileProcessor)
    {
        _settings = settings;
        _fileProcessor = fileProcessor;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
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

    public void StartTimer()
    {
        lock (_timerLock)
        {
            _timer?.Dispose();
            var intervalMs = Math.Max(1, _settings.PollingIntervalSeconds) * 1000;
            _timer = new System.Threading.Timer(OnTimerCallback, null, intervalMs, intervalMs);
            IsRunning = true;
        }
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
        if (IsRunning)
            StartTimer();
    }

    public void ProcessNow()
    {
        try
        {
            _fileProcessor.ProcessAll();
        }
        catch (Exception ex)
        {
            Log($"❌ Erro ao processar — {ex.Message}");
        }
    }

    private void OnTimerCallback(object? state)
    {
        try
        {
            _fileProcessor.ProcessAll();
        }
        catch (Exception ex)
        {
            Log($"❌ Erro no polling — {ex.Message}");
        }
    }

    public void Log(string message)
    {
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
