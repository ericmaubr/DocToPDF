using DocToPDF.Models;

namespace DocToPDF.Core;

public sealed class FileProcessor
{
    private readonly AppSettings _settings;
    private readonly Action<string> _log;

    public FileProcessor(AppSettings settings, Action<string> log)
    {
        _settings = settings;
        _log = log;
    }

    public void ProcessAll()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.InputDirectory))
            {
                _log("❌ Diretório de entrada não configurado.");
                return;
            }

            if (!Directory.Exists(_settings.InputDirectory))
            {
                _log($"❌ Diretório de entrada não existe: {_settings.InputDirectory}");
                return;
            }

            var files = Directory
                .EnumerateFiles(_settings.InputDirectory)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    return ext.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
                           ext.Equals(".json", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(f => f)
                .ToList();

            foreach (var file in files)
                ProcessFile(file);
        }
        catch (Exception ex)
        {
            _log($"❌ Erro ao processar lote — {ex.Message}");
        }
    }

    private void ProcessFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        try
        {
            if (!ValidateDirectories(fileName))
                return;

            DocumentNode root;
            try
            {
                var extension = Path.GetExtension(filePath);
                root = extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
                    ? XmlParser.Parse(filePath)
                    : JsonParser.Parse(filePath);
            }
            catch (Exception ex)
            {
                _log($"❌ {fileName} — {ex.Message}");
                MoveToError(filePath);
                return;
            }

            var pdfFileName = Path.ChangeExtension(fileName, ".pdf");
            var outputPath = Path.Combine(_settings.OutputDirectory, pdfFileName);

            try
            {
                PdfGenerator.Generate(root, outputPath);
            }
            catch (Exception ex)
            {
                _log($"❌ {fileName} — {ex.Message}");
                MoveToError(filePath);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_settings.RobotDirectory))
            {
                Directory.CreateDirectory(_settings.RobotDirectory);
                var robotPath = Path.Combine(_settings.RobotDirectory, pdfFileName);
                File.Copy(outputPath, robotPath, overwrite: true);
            }

            var processedPath = Path.Combine(_settings.ProcessedDirectory, fileName);
            if (File.Exists(processedPath))
                File.Delete(processedPath);
            File.Move(filePath, processedPath);

            _log($"✅ {fileName} → {pdfFileName}");
        }
        catch (Exception ex)
        {
            _log($"❌ {fileName} — {ex.Message}");
            MoveToError(filePath);
        }
    }

    private bool ValidateDirectories(string fileName)
    {
        if (string.IsNullOrWhiteSpace(_settings.OutputDirectory) ||
            string.IsNullOrWhiteSpace(_settings.ProcessedDirectory) ||
            string.IsNullOrWhiteSpace(_settings.ErrorDirectory))
        {
            _log($"❌ {fileName} — diretórios de saída, processados ou erros não configurados.");
            return false;
        }

        try
        {
            Directory.CreateDirectory(_settings.OutputDirectory);
            Directory.CreateDirectory(_settings.ProcessedDirectory);
            Directory.CreateDirectory(_settings.ErrorDirectory);
            return true;
        }
        catch (Exception ex)
        {
            _log($"❌ {fileName} — {ex.Message}");
            return false;
        }
    }

    private void MoveToError(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.ErrorDirectory))
                return;

            Directory.CreateDirectory(_settings.ErrorDirectory);
            var destination = Path.Combine(_settings.ErrorDirectory, Path.GetFileName(filePath));
            if (File.Exists(destination))
                File.Delete(destination);

            if (File.Exists(filePath))
                File.Move(filePath, destination);
        }
        catch (Exception ex)
        {
            _log($"❌ Falha ao mover para erros — {ex.Message}");
        }
    }
}
