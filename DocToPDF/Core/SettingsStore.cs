using System.Text.Json;
using DocToPDF.Models;

namespace DocToPDF.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Settings { get; } = new();

    public static string SettingsPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    public SettingsStore() => Load();

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return;

            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded == null)
                return;

            CopyTo(loaded, Settings);
        }
        catch
        {
            // Keep current in-memory settings.
        }
    }

    public void Save(AppSettings source)
    {
        CopyTo(source, Settings);
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private static void CopyTo(AppSettings from, AppSettings to)
    {
        to.InputDirectory = from.InputDirectory;
        to.OutputDirectory = from.OutputDirectory;
        to.ProcessedDirectory = from.ProcessedDirectory;
        to.ErrorDirectory = from.ErrorDirectory;
        to.RobotDirectory = from.RobotDirectory;
        to.PollingIntervalSeconds = from.PollingIntervalSeconds;
    }
}
