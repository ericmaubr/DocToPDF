using DocToPDF.Models;

namespace DocToPDF.Core;

/// <summary>
/// Lê e grava configuração em formato .conf (chave=valor, comentários com # ou ;).
/// </summary>
public static class ConfSettingsFile
{
    private static readonly Dictionary<string, Action<AppSettings, string>> Readers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DiretorioEntrada"] = (s, v) => s.InputDirectory = v,
            ["DiretorioSaidaPdf"] = (s, v) => s.OutputDirectory = v,
            ["DiretorioProcessados"] = (s, v) => s.ProcessedDirectory = v,
            ["DiretorioErros"] = (s, v) => s.ErrorDirectory = v,
            ["DiretorioRobo"] = (s, v) => s.RobotDirectory = v,
            ["IntervaloPollingSegundos"] = (s, v) =>
            {
                if (int.TryParse(v, out var seconds))
                    s.PollingIntervalSeconds = seconds;
            },
            // Chaves em inglês (migração de appsettings.json antigo)
            ["InputDirectory"] = (s, v) => s.InputDirectory = v,
            ["OutputDirectory"] = (s, v) => s.OutputDirectory = v,
            ["ProcessedDirectory"] = (s, v) => s.ProcessedDirectory = v,
            ["ErrorDirectory"] = (s, v) => s.ErrorDirectory = v,
            ["RobotDirectory"] = (s, v) => s.RobotDirectory = v,
            ["PollingIntervalSeconds"] = (s, v) =>
            {
                if (int.TryParse(v, out var seconds))
                    s.PollingIntervalSeconds = seconds;
            }
        };

    public static AppSettings Load(string path)
    {
        var settings = new AppSettings();
        if (!File.Exists(path))
            return settings;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith(';'))
                continue;

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();

            if (Readers.TryGetValue(key, out var apply))
                apply(settings, value);
        }

        return settings;
    }

    public static void Save(string path, AppSettings settings)
    {
        var lines = new[]
        {
            "# DocToPDF — Configuração",
            "# Coloque este arquivo na mesma pasta do executável.",
            "# Linhas vazias e linhas que começam com # ou ; são ignoradas.",
            "",
            $"DiretorioEntrada={settings.InputDirectory}",
            $"DiretorioSaidaPdf={settings.OutputDirectory}",
            $"DiretorioProcessados={settings.ProcessedDirectory}",
            $"DiretorioErros={settings.ErrorDirectory}",
            $"DiretorioRobo={settings.RobotDirectory}",
            $"IntervaloPollingSegundos={settings.PollingIntervalSeconds}",
            ""
        };

        File.WriteAllLines(path, lines);
    }
}
