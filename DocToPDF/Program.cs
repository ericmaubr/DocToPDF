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
        if (args.Any(a =>
                a.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                a == "/?" ||
                a.Equals("/help", StringComparison.OrdinalIgnoreCase)))
        {
            ShowUsage();
            return;
        }

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
        DocToPDFIpcClient.Diag(
            $"RunInteractiveTray: attach={attachToService} UserInteractive={Environment.UserInteractive} " +
            $"session={Environment.GetEnvironmentVariable("SESSIONNAME") ?? "?"} exe={Environment.ProcessPath}");

        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (!SingleInstanceMutex.TryAcquire(out var instanceMutex))
        {
            DocToPDFIpcClient.Diag("RunInteractiveTray: mutex já em uso → ativando instância existente e saindo (nenhum backend criado).");
            var activated = UiInstanceHost.TryActivateExisting();

            // Lançamento automático pelo serviço (--attach-service) não deve exibir popup.
            if (!attachToService)
            {
                MessageBox.Show(
                    activated
                        ? "O DocToPDF já está em execução. A janela existente foi trazida para a frente."
                        : "O DocToPDF já está em execução nesta sessão.",
                    "DocToPDF",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return;
        }

        using (instanceMutex)
        {
            using var session = new InteractiveSession(new SettingsStore(), attachToService);
            var backend = session.CreateBackend();
            DocToPDFIpcClient.Diag(
                $"RunInteractiveTray: backend={backend.GetType().Name} UsesServiceBackend={session.UsesServiceBackend}");
            var mainForm = new MainForm(session.SettingsStore, backend);
            var trayApp = new TrayApp(session, backend, mainForm);

            using var uiHost = UiInstanceHost.Start(trayApp.ActivateFromRunningInstance);
            Application.Run(trayApp);
        }
    }

    private static void RunAsWindowsService()
    {
        ServiceLog.Initialize();

        if (LocalModeLock.IsHeld())
        {
            ServiceLog.Error(
                "Serviço não iniciado: há uma instância standalone (modo local) do DocToPDF em execução. " +
                "Feche-a antes de iniciar o serviço para evitar processamento duplicado da mesma pasta.");
            Environment.Exit(1);
        }

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

    private static void ShowUsage()
    {
        MessageBox.Show(
            $"DocToPDF {AppVersion.Display}\n\n" +
            "Uso:\n" +
            "  DocToPDF.exe                  Abre a bandeja (conecta ao serviço se houver).\n" +
            "  DocToPDF.exe --attach-service Abre a bandeja anexada ao serviço Windows.\n" +
            "  DocToPDF.exe --service        Executa como serviço Windows.\n" +
            "  DocToPDF.exe --verify [pasta] Valida o processamento e encerra.\n" +
            "  DocToPDF.exe --help           Mostra esta ajuda.",
            "DocToPDF — Ajuda",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
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
