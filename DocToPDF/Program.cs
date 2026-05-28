using DocToPDF.Core;
using DocToPDF.Core.Ipc;
using DocToPDF.Models;
using DocToPDF.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocToPDF;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--verify", StringComparer.OrdinalIgnoreCase))
        {
            var samplesRoot = args.Length > 1
                ? args[1]
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Samples"));
            Environment.Exit(Verify.ProcessingVerifier.Run(samplesRoot));
        }

        var uiOnly = args.Contains("--ui", StringComparer.OrdinalIgnoreCase);
        if (uiOnly)
        {
            RunAsTrayApp(useServiceBackend: true);
            return;
        }

        if (!Environment.UserInteractive)
        {
            RunAsWindowsService();
            return;
        }

        var serviceRunning = DocToPDFIpcClient.TryQuickPing();
        RunAsTrayApp(useServiceBackend: serviceRunning);
    }

    private static void RunAsTrayApp(bool useServiceBackend)
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (UiInstanceHost.TryActivateExisting())
            return;

        using var uiInstance = UiInstanceHost.Start();

        var settingsStore = new SettingsStore();

        if (useServiceBackend)
        {
            var backend = new DeferredRemoteBackend();
            RunTrayLoop(uiInstance, settingsStore, backend);
            return;
        }

        var pollingService = CreatePollingService(settingsStore);
        foreach (var message in ConfiguredDirectories.EnsureExist(settingsStore.Settings))
            pollingService.Log(message);

        pollingService.StartTimer();
        pollingService.Log("DocToPDF — processamento automático iniciado.");

        var localBackend = new LocalDocToPDFBackend(pollingService);
        RunTrayLoop(uiInstance, settingsStore, localBackend);
    }

    private static void RunTrayLoop(UiInstanceHost uiInstance, SettingsStore settingsStore, IDocToPDFBackend backend)
    {
        var mainForm = new MainForm(settingsStore, backend);
        var trayApp = new TrayApp(settingsStore, backend, mainForm);
        uiInstance.SetActivateHandler(trayApp.ActivateFromRunningInstance);
        Application.Run(trayApp);
    }

    private static void RunAsWindowsService()
    {
        Host.CreateDefaultBuilder()
            .UseWindowsService(options => options.ServiceName = "DocToPDF")
            .ConfigureServices(services =>
            {
                services.AddSingleton<SettingsStore>();
                services.AddSingleton<DocToPDFIpcServer>();
                services.AddSingleton(CreatePollingService);
                services.AddHostedService<DocToPDFWorkerHostedService>();
            })
            .Build()
            .Run();
    }

    private static PollingService CreatePollingService(SettingsStore settingsStore)
    {
        PollingService? polling = null;
        var fileProcessor = new FileProcessor(settingsStore.Settings, message => polling!.Log(message));
        polling = new PollingService(settingsStore, fileProcessor);
        return polling;
    }

    private static PollingService CreatePollingService(IServiceProvider sp) =>
        CreatePollingService(sp.GetRequiredService<SettingsStore>());
}
