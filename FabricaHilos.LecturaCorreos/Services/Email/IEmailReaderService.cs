namespace FabricaHilos.LecturaCorreos.Services.Email;

using FabricaHilos.LecturaCorreos.Config;
using FabricaHilos.LecturaCorreos.Models;

/// <summary>
/// Escanea una cuenta IMAP y devuelve todos los adjuntos procesables
/// (XML y PDF), incluyendo los contenidos dentro de archivos ZIP.
/// </summary>
public interface IEmailReaderService
{
    Task<List<AdjuntoCorreo>> ObtenerAdjuntosAsync(
        CuentaCorreoOptions cuenta, int maxCorreos, CancellationToken ct);

    /// <summary>
    /// Marca como leídos (y opcionalmente mueve) los mensajes cuyos UIDs se indican,
    /// abriendo una nueva conexión IMAP. Se invoca DESPUÉS de que los adjuntos
    /// hayan sido persistidos en BD y disco.
    /// </summary>
    Task MarcarProcesadosAsync(
        CuentaCorreoOptions cuenta, IReadOnlySet<string> grupoIds, CancellationToken ct);
}
