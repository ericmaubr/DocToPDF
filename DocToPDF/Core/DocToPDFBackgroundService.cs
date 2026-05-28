using DocToPDF.Core.Ipc;
using Microsoft.Extensions.Hosting;

namespace DocToPDF.Core;

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

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1500, stoppingToken);
                    UserSessionTrayLauncher.TryLaunchTrayInUserSession();
                }
                catch (Exception ex)
                {
                    ServiceLog.Error($"Bandeja automática: {ex.Message}");
                }
            }, stoppingToken);

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
        _ipcServer.StopListening();
        await _polling.StopAsync(cancellationToken);
        ServiceLog.Info("Serviço parado.");
        await base.StopAsync(cancellationToken);
    }
}
