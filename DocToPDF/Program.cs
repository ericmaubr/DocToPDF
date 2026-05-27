using DocToPDF.Core;
using DocToPDF.Core.Ipc;
using DocToPDF.Models;
using DocToPDF.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocToPDF;

internal static class Program
{
    private const string UiMutexName = @"Global\DocToPDF.Tray.UI";

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

        if (!Environment.UserInteractive)
        {
            RunAsWindowsService();
            return;
        }

        var uiOnly = args.Contains("--ui", StringComparer.OrdinalIgnoreCase);
        RunAsTrayApp(uiOnly);
    }

    private static void RunAsTrayApp(bool uiOnly)
    {
        if (!TryAcquireUiMutex())
            return;

        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var settingsStore = new SettingsStore();
        var useRemote = uiOnly || DocToPDFIpcClient.IsServerAvailable();

        if (useRemote)
        {
            var client = new DocToPDFIpcClient();
            if (!TryConnectWithRetry(client, attempts: 15, delayMs: 1000))
            {
                MessageBox.Show(
                    "O serviço DocToPDF não está em execução ou não respondeu.\n\n" +
                    "Aguarde alguns segundos após iniciar o serviço ou execute: DocToPDF.exe --ui\n\n" +
                    "Se o serviço estiver parado, inicie-o em services.msc.",
                    "DocToPDF",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var backend = new RemoteDocToPDFBackend(client);
            RunTrayLoop(settingsStore, backend);
            return;
        }

        if (uiOnly)
        {
            MessageBox.Show(
                "Modo de interface iniciado, mas o serviço DocToPDF não está disponível.",
                "DocToPDF",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var pollingService = CreatePollingService(settingsStore);
        foreach (var message in ConfiguredDirectories.EnsureExist(settingsStore.Settings))
            pollingService.Log(message);

        var localBackend = new LocalDocToPDFBackend(pollingService);
        RunTrayLoop(settingsStore, localBackend);
    }

    private static void RunTrayLoop(SettingsStore settingsStore, IDocToPDFBackend backend)
    {
        var mainForm = new MainForm(settingsStore, backend);
        using var trayApp = new TrayApp(settingsStore, backend, mainForm);
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

    private static bool TryAcquireUiMutex()
    {
        var created = false;
        var mutex = new Mutex(true, UiMutexName, out created);
        if (created)
            return true;

        mutex.Dispose();
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
