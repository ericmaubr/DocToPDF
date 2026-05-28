using DocToPDF.Core.Ipc;
using Microsoft.Extensions.Hosting;

namespace DocToPDF.Core;

public sealed class DocToPDFWorkerHostedService : IHostedService
{
    private readonly PollingService _pollingService;
    private readonly DocToPDFIpcServer _ipcServer;

    public DocToPDFWorkerHostedService(PollingService pollingService, DocToPDFIpcServer ipcServer)
    {
        _pollingService = pollingService;
        _ipcServer = ipcServer;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ipcServer.Start(_pollingService);
            await _pollingService.StartAsync(cancellationToken);
            ServiceLog.Info("Worker iniciado (IPC + polling).");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400, cancellationToken);
                    UserSessionTrayLauncher.TryLaunchTrayUi();
                }
                catch (Exception ex)
                {
                    ServiceLog.Error($"Tray launcher: {ex}");
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            ServiceLog.Fatal(ex, "Falha ao iniciar DocToPDFWorkerHostedService");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _pollingService.StopAsync(cancellationToken);
        _ipcServer.Dispose();
        ServiceLog.Info("Worker parado.");
    }
}
