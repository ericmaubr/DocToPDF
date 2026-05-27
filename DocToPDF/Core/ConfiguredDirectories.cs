using DocToPDF.Models;

namespace DocToPDF.Core;

public static class ConfiguredDirectories
{
    public static IReadOnlyList<string> EnsureExist(AppSettings settings)
    {
        var messages = new List<string>();

        foreach (var (path, name) in EnumeratePaths(settings))
            EnsurePath(path, name, messages);

        return messages;
    }

    public static bool ValidateRequired(AppSettings settings, out string error)
    {
        var required = new (string Path, string Name)[]
        {
            (settings.InputDirectory, "Diretório de Entrada"),
            (settings.OutputDirectory, "Diretório de Saída PDF"),
            (settings.ProcessedDirectory, "Diretório Processados"),
            (settings.ErrorDirectory, "Diretório de Erros")
        };

        foreach (var (path, name) in required)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                error = $"{name} é obrigatório.";
                return false;
            }
        }

        error = "";
        return true;
    }

    public static bool ValidateAndCreateRequired(AppSettings settings, out string error)
    {
        if (!ValidateRequired(settings, out error))
            return false;

        foreach (var (path, name) in EnumerateRequiredPaths(settings))
        {
            if (!TryCreatePath(path, name, out error))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(settings.RobotDirectory) &&
            !TryCreatePath(settings.RobotDirectory, "Diretório do Robô", out error))
        {
            return false;
        }

        error = "";
        return true;
    }

    private static void EnsurePath(string? path, string name, List<string> messages)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var trimmed = path.Trim();

        try
        {
            var existed = Directory.Exists(trimmed);
            Directory.CreateDirectory(trimmed);

            if (!existed)
                messages.Add($"✅ Pasta criada: {name} — {trimmed}");
        }
        catch (Exception ex)
        {
            messages.Add($"❌ Não foi possível criar {name}: {ex.Message}");
        }
    }

    private static bool TryCreatePath(string path, string name, out string error)
    {
        try
        {
            Directory.CreateDirectory(path.Trim());
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            error = $"Não foi possível criar {name}: {ex.Message}";
            return false;
        }
    }

    private static IEnumerable<(string Path, string Name)> EnumeratePaths(AppSettings settings)
    {
        yield return (settings.InputDirectory, "Diretório de Entrada");
        yield return (settings.OutputDirectory, "Diretório de Saída PDF");
        yield return (settings.ProcessedDirectory, "Diretório Processados");
        yield return (settings.ErrorDirectory, "Diretório de Erros");
        yield return (settings.RobotDirectory, "Diretório do Robô");
    }

    private static IEnumerable<(string Path, string Name)> EnumerateRequiredPaths(AppSettings settings)
    {
        yield return (settings.InputDirectory, "Diretório de Entrada");
        yield return (settings.OutputDirectory, "Diretório de Saída PDF");
        yield return (settings.ProcessedDirectory, "Diretório Processados");
        yield return (settings.ErrorDirectory, "Diretório de Erros");
    }
}
