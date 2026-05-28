using System.IO.Pipes;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DocToPDF.Core;

internal static class NamedPipeSecurityFactory
{
    public static NamedPipeServerStream CreateServer(
        string pipeName,
        PipeOptions options = PipeOptions.Asynchronous)
    {
        if (OperatingSystem.IsWindows())
        {
            var secured = TryCreateWithSecurityAtConstruction(pipeName, options);
            if (secured != null)
                return secured;
        }

        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            options);
    }

  /// <summary>
    /// No Windows, aplica ACL na criação (SetAccessControl depois falha no serviço SYSTEM).
    /// Usa reflexão para compilar em CI Linux e funcionar no runtime Windows.
    /// </summary>
    private static NamedPipeServerStream? TryCreateWithSecurityAtConstruction(
        string pipeName,
        PipeOptions options)
    {
        try
        {
            var ctor = typeof(NamedPipeServerStream).GetConstructor(
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types:
                [
                    typeof(string),
                    typeof(PipeDirection),
                    typeof(int),
                    typeof(PipeTransmissionMode),
                    typeof(PipeOptions),
                    typeof(int),
                    typeof(int),
                    typeof(PipeSecurity)
                ],
                modifiers: null);

            if (ctor == null)
            {
                ServiceLog.Error($"Pipe ACL ({pipeName}): ctor com PipeSecurity não encontrado.");
                return null;
            }

            var stream = (NamedPipeServerStream)ctor.Invoke(new object?[]
            {
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                options,
                0,
                0,
                CreatePipeSecurity()
            });

            ServiceLog.Info($"Pipe ACL ({pipeName}): aplicada na criação.");
            return stream;
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"Pipe ACL ({pipeName}): {ex.Message}");
            return null;
        }
    }

    private static PipeSecurity CreatePipeSecurity()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        return security;
    }
}
