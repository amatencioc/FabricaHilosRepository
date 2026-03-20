namespace FabricaHilos.LecturaCorreos.Services.Email.Lectores;

using FabricaHilos.LecturaCorreos.Models;
using MimeKit;

/// <summary>
/// Extrae adjuntos XML de un <see cref="MimePart"/> con detección automática de encoding.
/// </summary>
public interface ILectorAdjuntoXml
{
    Task<AdjuntoCorreo> ExtraerAsync(
        MimePart parte, string asunto, string remitente, DateTime fecha, CancellationToken ct);

    /// <summary>
    /// Lee el contenido XML desde un <see cref="MemoryStream"/> ya decodificado,
    /// detectando BOM y haciendo fallback a ISO-8859-1 si no arranca con '&lt;'.
    /// </summary>
    string LeerDesdeStream(MemoryStream ms);
}
