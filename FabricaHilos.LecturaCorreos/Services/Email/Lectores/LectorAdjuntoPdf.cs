namespace FabricaHilos.LecturaCorreos.Services.Email.Lectores;

using FabricaHilos.LecturaCorreos.Models;
using MimeKit;

public class LectorAdjuntoPdf : ILectorAdjuntoPdf
{
    // Límite por PDF: previene OutOfMemoryException con adjuntos malformados o inusualmente grandes.
    private const long MaxPdfBytes = 25 * 1024 * 1024; // 25 MB

    public async Task<AdjuntoCorreo> ExtraerAsync(
        MimePart parte, string asunto, string remitente, DateTime fecha, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await parte.Content.DecodeToAsync(ms, ct);

        if (ms.Length > MaxPdfBytes)
            throw new InvalidOperationException(
                $"PDF '{parte.FileName}' excede el límite de {MaxPdfBytes / (1024 * 1024)} MB " +
                $"({ms.Length / (1024.0 * 1024):F1} MB). Se omite el adjunto.");

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
