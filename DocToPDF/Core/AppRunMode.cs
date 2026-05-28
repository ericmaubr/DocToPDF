namespace DocToPDF.Core;

public static class AppRunMode
{
    public static string Describe(IDocToPDFBackend backend)
    {
        if (backend is DeferredRemoteBackend deferred)
        {
            if (deferred.IsConnected)
                return "Serviço Windows";

            if (deferred.IsConnecting)
                return "Conectando ao serviço…";

            if (deferred.ConnectFailed)
                return "Serviço indisponível";
        }

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
        if (backend is DeferredRemoteBackend deferred)
        {
            if (deferred.IsConnecting)
                return Color.Goldenrod;

            if (deferred.ConnectFailed)
                return Color.OrangeRed;

            if (!deferred.IsConnected)
                return Color.Gray;
        }

        if (!isRunning)
            return Color.Gray;

        if (backend.IsRemote)
            return Color.LimeGreen;

        return Color.DodgerBlue;
    }
}
