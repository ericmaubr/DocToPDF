using DocToPDF.Core;
using DocToPDF.Core.Ipc;
using DocToPDF.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocToPDF;

/// <summary>
/// Um executável, três modos (recomendação Microsoft / Stephen Cleary):
/// <list type="bullet">
/// <item><b>Serviço</b> — worker headless na Session 0; expõe IPC.</item>
/// <item><b>Bandeja + serviço</b> — usuário executa na sessão interativa; conecta ao pipe se o serviço estiver ativo.</item>
/// <item><b>Standalone</b> — mesmo exe sem serviço; polling local na sessão do usuário.</item>
/// </list>
/// O serviço nunca exibe UI nem inicia a bandeja (isolamento Session 0).
/// </summary>
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

        if (IsServiceMode(args))
        {
            RunAsWindowsService();
            return;
        }

        RunInteractiveTray();
    }

    /// <summary>Serviço Windows ou flag explícita --service.</summary>
    private static bool IsServiceMode(string[] args) =>
        args.Contains("--service", StringComparer.OrdinalIgnoreCase) || !Environment.UserInteractive;

    /// <summary>
    /// Processo na sessão do usuário: conecta ao serviço se disponível; senão modo standalone.
    /// --ui é aceito por compatibilidade (equivale a abrir a bandeja).
    /// </summary>
    private static void RunInteractiveTray()
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (UiInstanceHost.TryActivateExisting())
            return;

        using var uiInstance = UiInstanceHost.Start();
        var settingsStore = new SettingsStore();

        if (DocToPDFIpcClient.TryQuickPing())
        {
            var backend = new DeferredRemoteBackend();
            RunTrayLoop(uiInstance, settingsStore, backend);
            return;
        }

        RunStandaloneTray(uiInstance, settingsStore);
    }

    private static void RunStandaloneTray(UiInstanceHost uiInstance, SettingsStore settingsStore)
    {
        var pollingService = CreatePollingService(settingsStore);
        foreach (var message in ConfiguredDirectories.EnsureExist(settingsStore.Settings))
            pollingService.Log(message);

        pollingService.StartTimer();
        pollingService.Log("DocToPDF — modo local (serviço não detectado).");

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
        ServiceLog.Initialize();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                ServiceLog.Fatal(ex, "UnhandledException");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ServiceLog.Fatal(e.Exception, "UnobservedTaskException");
            e.SetObserved();
        };

        Host.CreateDefaultBuilder()
            .UseWindowsService(options => options.ServiceName = "DocToPDF")
            .ConfigureServices(services =>
            {
                services.Configure<HostOptions>(options =>
                    options.BackgroundServiceExceptionBehavior =
                        BackgroundServiceExceptionBehavior.StopHost);

                services.AddSingleton<SettingsStore>();
                services.AddSingleton<DocToPDFIpcServer>();
                services.AddSingleton(CreatePollingService);
                services.AddHostedService<DocToPDFBackgroundService>();
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
