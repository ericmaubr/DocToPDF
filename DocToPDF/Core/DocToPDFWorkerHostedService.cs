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
        _ipcServer.Start(_pollingService);
        await _pollingService.StartAsync(cancellationToken);

        _ = Task.Run(async () =>
        {
            await Task.Delay(2000, cancellationToken);
            UserSessionTrayLauncher.TryLaunchTrayUi();
        }, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _pollingService.StopAsync(cancellationToken);
        _ipcServer.Dispose();
    }
}
