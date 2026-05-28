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
            RunAsTrayApp(uiOnly: true);
            return;
        }

        if (!Environment.UserInteractive)
        {
            RunAsWindowsService();
            return;
        }

        RunAsTrayApp(uiOnly: false);
    }

    private static void RunAsTrayApp(bool uiOnly)
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (UiInstanceHost.TryActivateExisting())
            return;

        using var uiInstance = UiInstanceHost.Start();

        var settingsStore = new SettingsStore();
        var useRemote = uiOnly || DocToPDFIpcClient.IsServerAvailable();

        if (useRemote)
        {
            var client = new DocToPDFIpcClient();
            if (!TryConnectWithRetry(client, attempts: 8, delayMs: 500))
            {
                MessageBox.Show(
                    "O serviço DocToPDF não está em execução ou não respondeu.\n\n" +
                    "Verifique em services.msc se o serviço está 'Em execução'.\n" +
                    "Depois execute: DocToPDF.exe --ui\n\n" +
                    "Confira também os ícones ocultos na bandeja (^ ao lado do relógio).",
                    $"DocToPDF {AppVersion.Display}",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var backend = new RemoteDocToPDFBackend(client);
            RunTrayLoop(uiInstance, settingsStore, backend);
            return;
        }

        if (uiOnly)
        {
            MessageBox.Show(
                "Modo de interface iniciado, mas o serviço DocToPDF não está disponível.",
                $"DocToPDF {AppVersion.Display}",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var pollingService = CreatePollingService(settingsStore);
        foreach (var message in ConfiguredDirectories.EnsureExist(settingsStore.Settings))
            pollingService.Log(message);

        // Programa desktop inicia processamento automaticamente por padrão.
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

    private static bool TryConnectWithRetry(DocToPDFIpcClient client, int attempts, int delayMs)
    {
        for (var i = 0; i < attempts; i++)
        {
            if (client.TryConnect(TimeSpan.FromSeconds(2)))
                return true;

            if (i < attempts - 1)
                Thread.Sleep(delayMs);
        }

        return false;
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
