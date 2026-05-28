namespace DocToPDF.Core;

/// <summary>
/// Garante uma única instância da UI por sessão Windows (padrão WinForms).
/// </summary>
public sealed class SingleInstanceMutex : IDisposable
{
    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;

    private SingleInstanceMutex(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    public static string MutexNameForCurrentSession()
    {
        var sessionId = 0;
        try
        {
            sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        }
        catch
        {
            // Ignore.
        }

        return $@"Local\DocToPDF.UI.Session{sessionId}";
    }

    public static bool TryAcquire(out SingleInstanceMutex? instance)
    {
        instance = null;
        var name = MutexNameForCurrentSession();

        try
        {
            var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
            if (!createdNew)
                return false;

            instance = new SingleInstanceMutex(mutex, ownsMutex: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_ownsMutex)
            return;

        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
            // Ignore.
        }

        _mutex.Dispose();
    }
}
