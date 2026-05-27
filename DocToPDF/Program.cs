using DocToPDF.Core;
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

        if (!Environment.UserInteractive)
        {
            RunAsWindowsService();
            return;
        }

        RunAsTrayApp();
    }

    private static void RunAsTrayApp()
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var settingsStore = new SettingsStore();
        var pollingService = CreatePollingService(settingsStore.Settings);
        var mainForm = new MainForm(settingsStore, pollingService);

        using var trayApp = new TrayApp(settingsStore, pollingService, mainForm);
        Application.Run(trayApp);
    }

    private static void RunAsWindowsService()
    {
        Host.CreateDefaultBuilder()
            .UseWindowsService(options => options.ServiceName = "DocToPDF")
            .ConfigureServices(services =>
            {
                services.AddSingleton<SettingsStore>();
                services.AddSingleton(sp => sp.GetRequiredService<SettingsStore>().Settings);
                services.AddSingleton(CreatePollingService);
                services.AddHostedService(sp => sp.GetRequiredService<PollingService>());
            })
            .Build()
            .Run();
    }

    private static PollingService CreatePollingService(AppSettings settings)
    {
        PollingService? polling = null;
        var fileProcessor = new FileProcessor(settings, message => polling!.Log(message));
        polling = new PollingService(settings, fileProcessor);
        return polling;
    }

    private static PollingService CreatePollingService(IServiceProvider sp) =>
        CreatePollingService(sp.GetRequiredService<AppSettings>());
}
