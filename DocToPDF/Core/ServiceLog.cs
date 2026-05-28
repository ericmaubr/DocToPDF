using System.Diagnostics;

namespace DocToPDF.Core;

/// <summary>
/// Log em arquivo ao lado do executável (útil quando o serviço falha sem UI).
/// </summary>
public static class ServiceLog
{
    private static readonly object Gate = new();
    private static string? _logPath;

    public static void Initialize()
    {
        try
        {
            var dir = SettingsStore.AppDirectory;
            if (string.IsNullOrEmpty(dir))
                dir = AppContext.BaseDirectory;

            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "DocToPDF-service.log");
        }
        catch
        {
            _logPath = null;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message) => Write("ERROR", message);

    public static void Fatal(Exception ex, string context = "")
    {
        var text = string.IsNullOrEmpty(context)
            ? ex.ToString()
            : $"{context}{Environment.NewLine}{ex}";
        Write("FATAL", text);

        try
        {
            if (OperatingSystem.IsWindows())
                EventLog.WriteEntry("DocToPDF", text, EventLogEntryType.Error);
        }
        catch
        {
            // Event source may not exist yet.
        }
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        lock (Gate)
        {
            try
            {
                if (_logPath != null)
                    File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch
            {
                // Ignore log failures.
            }
        }
    }
}
