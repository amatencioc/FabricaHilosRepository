namespace FabricaHilos.LecturaCorreos.Services.Email.Lectores;

using FabricaHilos.LecturaCorreos.Models;
using MimeKit;

/// <summary>
/// Descomprime un adjunto ZIP y extrae todas las entradas XML y PDF que contiene.
/// </summary>
public interface ILectorAdjuntoZip
{
    Task<IReadOnlyList<AdjuntoCorreo>> ExtraerAsync(
        MimePart parte, string asunto, string remitente, DateTime fecha, CancellationToken ct);
}
