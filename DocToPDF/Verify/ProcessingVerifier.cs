using DocToPDF.Core;
using DocToPDF.Models;

namespace DocToPDF.Verify;

/// <summary>
/// Headless verification helper for CI (dotnet run -- --verify).
/// </summary>
public static class ProcessingVerifier
{
    public static int Run(string samplesRoot)
    {
        var xmlPath = Path.Combine(samplesRoot, "729494492026040001.xml");
        var jsonPath = Path.Combine(samplesRoot, "72949449-MIT-202604.json");
        var badJsonPath = Path.Combine(samplesRoot, "bad.json");

        if (!File.Exists(xmlPath) || !File.Exists(jsonPath))
        {
            Console.Error.WriteLine("Sample files not found.");
            return 1;
        }

        var workDir = Path.Combine(Path.GetTempPath(), "doctopdf-verify-" + Guid.NewGuid().ToString("N"));
        var input = Path.Combine(workDir, "input");
        var output = Path.Combine(workDir, "output");
        var processed = Path.Combine(workDir, "processed");
        var error = Path.Combine(workDir, "error");

        Directory.CreateDirectory(input);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(processed);
        Directory.CreateDirectory(error);

        var settings = new AppSettings
        {
            InputDirectory = input,
            OutputDirectory = output,
            ProcessedDirectory = processed,
            ErrorDirectory = error,
            PollingIntervalSeconds = 30
        };

        var logs = new List<string>();
        var processor = new FileProcessor(settings, logs.Add);

        File.Copy(xmlPath, Path.Combine(input, Path.GetFileName(xmlPath)));
        processor.ProcessAll();

        if (!File.Exists(Path.Combine(output, "729494492026040001.pdf")))
        {
            Console.Error.WriteLine("XML PDF was not generated.");
            return 1;
        }

        File.Copy(jsonPath, Path.Combine(input, Path.GetFileName(jsonPath)));
        processor.ProcessAll();

        if (!File.Exists(Path.Combine(output, "72949449-MIT-202604.pdf")))
        {
            Console.Error.WriteLine("JSON PDF was not generated.");
            return 1;
        }

        File.WriteAllText(Path.Combine(input, "bad.json"), File.ReadAllText(badJsonPath));
        processor.ProcessAll();

        if (!File.Exists(Path.Combine(error, "bad.json")))
        {
            Console.Error.WriteLine("Malformed JSON was not moved to error directory.");
            return 1;
        }

        if (PdfGenerator.FormatKey("codTrib") != "Cod Trib")
        {
            Console.Error.WriteLine("FormatKey failed for codTrib.");
            return 1;
        }

        Console.WriteLine("Verification passed.");
        return 0;
    }
}
