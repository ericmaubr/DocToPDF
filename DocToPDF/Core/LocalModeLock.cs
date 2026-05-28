namespace DocToPDF.Core;

/// <summary>
/// Trava de modo local (standalone). A bandeja em modo local a mantém aberta com
/// <see cref="FileShare.None"/> enquanto processa. O serviço Windows recusa iniciar
/// se a trava estiver retida, evitando dois processadores na mesma pasta.
/// Cross-session e sem privilégio elevado; o SO libera o handle se o processo morrer.
/// </summary>
public sealed class LocalModeLock : IDisposable
{
    private readonly FileStream _stream;

    private LocalModeLock(FileStream stream) => _stream = stream;

    private static string LockPath()
    {
        var dir = SettingsStore.AppDirectory;
        if (string.IsNullOrEmpty(dir))
            dir = AppContext.BaseDirectory;

        return Path.Combine(dir, "DocToPDF.local.lock");
    }

    public static bool TryAcquire(out LocalModeLock? instance)
    {
        instance = null;
        try
        {
            var stream = new FileStream(
                LockPath(),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            instance = new LocalModeLock(stream);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>True se uma bandeja em modo local mantém a trava neste momento.</summary>
    public static bool IsHeld()
    {
        try
        {
            using var stream = new FileStream(
                LockPath(),
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (IOException)
        {
            // Sharing violation: outro processo retém a trava.
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            _stream.Dispose();
        }
        catch
        {
            // Ignore.
        }
    }
}
