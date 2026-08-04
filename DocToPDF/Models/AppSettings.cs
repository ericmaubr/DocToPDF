namespace DocToPDF.Models;

public class AppSettings
{
    public string InputDirectory { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public string ProcessedDirectory { get; set; } = "";
    public string ErrorDirectory { get; set; } = "";
    public string RobotDirectory { get; set; } = "";
    public int PollingIntervalSeconds { get; set; } = 30;
    public bool PadronizarPdfAtivo { get; set; } = false;
    public string PadronizarPdfUrl { get; set; } = "";
}
