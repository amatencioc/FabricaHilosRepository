namespace FabricaHilos.LecturaCorreos.Services.Email.Conexion;

using FabricaHilos.LecturaCorreos.Config;
using MailKit.Net.Imap;

/// <summary>
/// Crea, conecta y autentica un <see cref="ImapClient"/> listo para usar.
/// El llamador es responsable de hacer Dispose sobre el cliente devuelto.
/// </summary>
public interface IImapConexionService
{
    Task<ImapClient> ConectarAsync(CuentaCorreoOptions cuenta, CancellationToken ct);
}
