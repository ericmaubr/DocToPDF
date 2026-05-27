using DocToPDF.Models;

namespace DocToPDF.Core;

public static class ConfiguredDirectories
{
    public static IReadOnlyList<string> EnsureExist(AppSettings settings)
    {
        var errors = new List<string>();

        foreach (var (path, name) in EnumeratePaths(settings))
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            try
            {
                Directory.CreateDirectory(path.Trim());
            }
            catch (Exception ex)
            {
                errors.Add($"❌ Não foi possível criar {name}: {ex.Message}");
            }
        }

        return errors;
    }

    public static bool ValidateAndCreateRequired(AppSettings settings, out string error)
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

            try
            {
                Directory.CreateDirectory(path.Trim());
            }
            catch (Exception ex)
            {
                error = $"Não foi possível criar {name}: {ex.Message}";
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.RobotDirectory))
        {
            try
            {
                Directory.CreateDirectory(settings.RobotDirectory.Trim());
            }
            catch (Exception ex)
            {
                error = $"Não foi possível criar Diretório do Robô: {ex.Message}";
                return false;
            }
        }

        error = "";
        return true;
    }

    private static IEnumerable<(string Path, string Name)> EnumeratePaths(AppSettings settings)
    {
        yield return (settings.InputDirectory, "Diretório de Entrada");
        yield return (settings.OutputDirectory, "Diretório de Saída PDF");
        yield return (settings.ProcessedDirectory, "Diretório Processados");
        yield return (settings.ErrorDirectory, "Diretório de Erros");
        yield return (settings.RobotDirectory, "Diretório do Robô");
    }
}
