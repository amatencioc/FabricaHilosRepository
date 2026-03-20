namespace FabricaHilos.LecturaCorreos.Services.Email.Lectores;

using FabricaHilos.LecturaCorreos.Models;
using MimeKit;

public class LectorAdjuntoPdf : ILectorAdjuntoPdf
{
    public async Task<AdjuntoCorreo> ExtraerAsync(
        MimePart parte, string asunto, string remitente, DateTime fecha, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await parte.Content.DecodeToAsync(ms, ct);

        return new AdjuntoCorreo
        {
            TipoAdjunto   = "PDF",
            NombreArchivo = parte.FileName ?? string.Empty,
            ContenidoPdf  = ms.ToArray(),
            Asunto        = asunto,
            Remitente     = remitente,
            FechaCorreo   = fecha,
        };
    }
}
