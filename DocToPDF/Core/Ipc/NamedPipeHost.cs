using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DocToPDF.Core.Ipc;

/// <summary>
/// Cria o pipe já com a ACL correta para comunicação entre o serviço (SYSTEM, sessão 0)
/// e a UI na sessão do usuário. A ACL padrão de um pipe criado pelo SYSTEM nega escrita a
/// usuários comuns — por isso concedemos ReadWrite a usuários autenticados na criação.
/// A ACL precisa ser passada em <see cref="NamedPipeServerStreamAcl.Create"/>; chamar
/// SetAccessControl depois lança PlatformNotSupportedException no .NET moderno.
/// </summary>
internal static class NamedPipeHost
{
    public static NamedPipeServerStream CreateServer(string pipeName, PipeOptions options = PipeOptions.Asynchronous)
    {
        var security = new PipeSecurity();

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            options,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity: security);
    }
}
