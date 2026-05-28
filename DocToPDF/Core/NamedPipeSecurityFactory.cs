using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DocToPDF.Core;

internal static class NamedPipeSecurityFactory
{
    public static NamedPipeServerStream CreateServer(
        string pipeName,
        PipeOptions options = PipeOptions.Asynchronous)
    {
        var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            options,
            0,
            0);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                server.SetAccessControl(CreatePipeSecurity());
            }
            catch (Exception ex)
            {
                ServiceLog.Error($"Pipe ACL ({pipeName}): {ex.Message}");
            }
        }

        return server;
    }

    private static PipeSecurity CreatePipeSecurity()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        return security;
    }
}
