namespace DocToPDF.Core;

/// <summary>
/// Textos e cores para indicar modo local vs serviço Windows.
/// </summary>
public static class AppRunMode
{
    public static string Describe(IDocToPDFBackend backend)
    {
        if (backend is DeferredRemoteBackend deferred)
            return deferred.IsConnected ? "Serviço Windows" : "Conectando ao serviço…";

        return backend.IsRemote ? "Serviço Windows" : "Local";
    }

    public static string TrayStatusSuffix(IDocToPDFBackend backend, bool isRunning)
    {
        var mode = Describe(backend);
        var state = isRunning ? "Rodando" : "Parado";
        return $"{state} · {mode}";
    }

    public static Color TrayIndicatorColor(IDocToPDFBackend backend, bool isRunning)
    {
        if (!isRunning)
            return Color.Gray;

        if (backend is DeferredRemoteBackend { IsConnected: false })
            return Color.Goldenrod;

        if (backend.IsRemote)
            return Color.LimeGreen;

        return Color.DodgerBlue;
    }
}
