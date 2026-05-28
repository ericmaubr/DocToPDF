using DocToPDF.Core.Ipc;
using Microsoft.Extensions.Hosting;

namespace DocToPDF.Core;

/// <summary>
/// Worker do serviço Windows: apenas polling + IPC. Sem UI (Session 0).
/// A bandeja roda em processo separado na sessão do usuário (ver README).
/// </summary>
public sealed class DocToPDFBackgroundService : BackgroundService
{
    private readonly PollingService _polling;
    private readonly DocToPDFIpcServer _ipcServer;

    public DocToPDFBackgroundService(PollingService polling, DocToPDFIpcServer ipcServer)
    {
        _polling = polling;
        _ipcServer = ipcServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _ipcServer.Start(_polling);
            await _polling.StartAsync(stoppingToken);
            ServiceLog.Info("Serviço em execução (IPC + processamento).");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Parada normal.
        }
        catch (Exception ex)
        {
            ServiceLog.Fatal(ex, "Serviço encerrado por exceção");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _polling.StopAsync(cancellationToken);
        _ipcServer.Dispose();
        ServiceLog.Info("Serviço parado.");
        await base.StopAsync(cancellationToken);
    }
}
