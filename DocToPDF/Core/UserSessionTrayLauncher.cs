using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DocToPDF.Core;

/// <summary>
/// Inicia a UI na sessão interativa do usuário (única forma suportada de "abrir bandeja" a partir do serviço).
/// Só chamar se <see cref="SingleInstanceMutex"/> / pipe indicarem que a UI ainda não está ativa.
/// </summary>
public static class UserSessionTrayLauncher
{
    public static void TryLaunchTrayInUserSession()
    {
        if (SingleInstanceMutex.TryAcquire(out var probe))
        {
            probe?.Dispose();
        }
        else
        {
            UiInstanceHost.TryActivateExisting();
            return;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return;

        TryLaunchInActiveSession(exePath);
    }

    private static bool TryLaunchInActiveSession(string exePath)
    {
        var sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId is 0xFFFFFFFF or 0)
            return false;

        if (!WTSQueryUserToken(sessionId, out var userToken) || userToken == IntPtr.Zero)
            return false;

        try
        {
            if (!DuplicateTokenEx(
                    userToken,
                    TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID,
                    IntPtr.Zero,
                    SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    TOKEN_TYPE.TokenPrimary,
                    out var primaryToken))
            {
                return false;
            }

            try
            {
                if (!CreateEnvironmentBlock(out var environment, primaryToken, false))
                    return false;

                try
                {
                    var commandLine = $"\"{exePath}\"";
                    var startupInfo = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf<STARTUPINFO>(),
                        lpDesktop = "winsta0\\default",
                        dwFlags = 0x00000001, // STARTF_USESHOWWINDOW
                        wShowWindow = 0 // SW_HIDE — só bandeja, sem janela console
                    };

                    var ok = CreateProcessAsUser(
                        primaryToken,
                        null,
                        commandLine,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        CREATE_UNICODE_ENVIRONMENT,
                        environment,
                        Path.GetDirectoryName(exePath),
                        ref startupInfo,
                        out var processInfo);

                    if (ok)
                    {
                        CloseHandle(processInfo.hProcess);
                        CloseHandle(processInfo.hThread);
                        ServiceLog.Info("Bandeja iniciada na sessão do usuário.");
                    }

                    return ok;
                }
                finally
                {
                    DestroyEnvironmentBlock(environment);
                }
            }
            finally
            {
                CloseHandle(primaryToken);
            }
        }
        finally
        {
            CloseHandle(userToken);
        }
    }

    private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint TOKEN_ADJUST_DEFAULT = 0x0080;
    private const uint TOKEN_ADJUST_SESSIONID = 0x0100;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    private enum SECURITY_IMPERSONATION_LEVEL
    {
        SecurityImpersonation = 2
    }

    private enum TOKEN_TYPE
    {
        TokenPrimary = 1
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        uint desiredAccess,
        IntPtr tokenAttributes,
        SECURITY_IMPERSONATION_LEVEL impersonationLevel,
        TOKEN_TYPE tokenType,
        out IntPtr newToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(
        IntPtr token,
        string? applicationName,
        string commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref STARTUPINFO startupInfo,
        out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }
}
