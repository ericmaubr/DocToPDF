using DocToPDF.Core;
using DocToPDF.Core.Ipc;
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

        if (IsServiceMode(args))
        {
            RunAsWindowsService();
            return;
        }

        var attachToService = args.Contains("--attach-service", StringComparer.OrdinalIgnoreCase);
        RunInteractiveTray(attachToService);
    }

    private static bool IsServiceMode(string[] args) =>
        args.Contains("--service", StringComparer.OrdinalIgnoreCase) || !Environment.UserInteractive;

    private static void RunInteractiveTray(bool attachToService)
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (!SingleInstanceMutex.TryAcquire(out var instanceMutex))
        {
            UiInstanceHost.TryActivateExisting();
            return;
        }

        using (instanceMutex)
        {
            using var session = new InteractiveSession(new SettingsStore(), attachToService);
            var backend = session.CreateBackend();
            var mainForm = new MainForm(session.SettingsStore, backend);
            var trayApp = new TrayApp(session, backend, mainForm);

            using var uiHost = UiInstanceHost.Start(trayApp.ActivateFromRunningInstance);
            Application.Run(trayApp);
        }
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
