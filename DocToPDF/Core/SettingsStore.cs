using System.Text.Json;
using DocToPDF.Models;

namespace DocToPDF.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Settings { get; } = new();

    public static string AppDirectory
    {
        get
        {
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrEmpty(exeDir))
                return exeDir;

            return AppContext.BaseDirectory;
        }
    }

    /// <summary>
    /// Ex.: C:\DocToPDF\DocToPDF.conf quando o executável é DocToPDF.exe
    /// </summary>
    public static string SettingsPath
    {
        get
        {
            var exePath = Environment.ProcessPath;
            var baseName = string.IsNullOrEmpty(exePath)
                ? "DocToPDF"
                : Path.GetFileNameWithoutExtension(exePath);
            return Path.Combine(AppDirectory, baseName + ".conf");
        }
    }

    private static string LegacyJsonPath => Path.Combine(AppDirectory, "appsettings.json");

    public SettingsStore() => Load();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                CopyTo(ConfSettingsFile.Load(SettingsPath), Settings);
                return;
            }

            if (TryMigrateLegacyJson())
                return;
        }
        catch
        {
            // Mantém configuração em memória.
        }
    }

    public void Save(AppSettings source)
    {
        CopyTo(source, Settings);
        ConfSettingsFile.Save(SettingsPath, Settings);
    }

    private bool TryMigrateLegacyJson()
    {
        if (!File.Exists(LegacyJsonPath))
            return false;

        try
        {
            var json = File.ReadAllText(LegacyJsonPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded == null)
                return false;

            CopyTo(loaded, Settings);
            ConfSettingsFile.Save(SettingsPath, Settings);
            return true;
        }
        catch
        {
            return false;
        }
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
