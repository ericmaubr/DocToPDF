using System.Reflection;

namespace DocToPDF.Core;

public static class AppVersion
{
    public static string Current
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
                return informational.Split('+')[0];

            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    public static string Display => $"v{Current}";
}
