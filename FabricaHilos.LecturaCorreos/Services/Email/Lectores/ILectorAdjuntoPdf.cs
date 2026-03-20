namespace FabricaHilos.LecturaCorreos.Services.Email.Lectores;

using FabricaHilos.LecturaCorreos.Models;
using MimeKit;

/// <summary>
/// Extrae adjuntos PDF de un <see cref="MimePart"/> como arreglo de bytes.
/// </summary>
public interface ILectorAdjuntoPdf
{
    Task<AdjuntoCorreo> ExtraerAsync(
        MimePart parte, string asunto, string remitente, DateTime fecha, CancellationToken ct);
}
