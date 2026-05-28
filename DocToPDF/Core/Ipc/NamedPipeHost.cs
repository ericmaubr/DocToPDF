using System.IO.Pipes;

namespace DocToPDF.Core.Ipc;

/// <summary>
/// Criação de pipes com a ACL padrão do Windows (Everyone pode conectar entre sessões).
/// Não chamar SetAccessControl — falha no serviço SYSTEM e quebra o acesso da UI.
/// </summary>
internal static class NamedPipeHost
{
    public static NamedPipeServerStream CreateServer(string pipeName, PipeOptions options = PipeOptions.Asynchronous) =>
        new(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            options);
}
