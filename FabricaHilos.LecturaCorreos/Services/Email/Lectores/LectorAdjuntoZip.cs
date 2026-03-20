namespace FabricaHilos.LecturaCorreos.Services.Email.Lectores;

using System.IO.Compression;
using FabricaHilos.LecturaCorreos.Models;
using MimeKit;
using Microsoft.Extensions.Logging;

public class LectorAdjuntoZip : ILectorAdjuntoZip
{
    // Límite por entrada descomprimida: previene ZIP bomb y OutOfMemoryException.
    private const long MaxEntradaBytes = 25 * 1024 * 1024; // 25 MB

    private readonly ILectorAdjuntoXml              _lectorXml;
    private readonly ILogger<LectorAdjuntoZip>      _logger;

    public LectorAdjuntoZip(ILectorAdjuntoXml lectorXml, ILogger<LectorAdjuntoZip> logger)
    {
        _lectorXml = lectorXml;
        _logger    = logger;
    }

    public async Task<IReadOnlyList<AdjuntoCorreo>> ExtraerAsync(
        MimePart parte, string asunto, string remitente, DateTime fecha, CancellationToken ct)
    {
        var resultado = new List<AdjuntoCorreo>();

        using var ms = new MemoryStream();
        await parte.Content.DecodeToAsync(ms, ct);
        ms.Position = 0;

        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (ct.IsCancellationRequested) break;

            // Tamaño descomprimido desconocido (entry.Length == 0) o excesivo → omitir entrada.
            if (entry.Length > MaxEntradaBytes)
            {
                _logger.LogWarning(
                    "Entrada ZIP '{Nombre}' omitida: tamaño descomprimido {MB:F1} MB supera el límite de {Max} MB.",
                    entry.Name, entry.Length / (1024.0 * 1024), MaxEntradaBytes / (1024 * 1024));
                continue;
            }

            if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                using var entryMs = new MemoryStream();
                using (var s = entry.Open())
                    await s.CopyToAsync(entryMs, ct);

                resultado.Add(new AdjuntoCorreo
                {
                    TipoAdjunto   = "XML",
                    NombreArchivo = entry.Name,
                    ContenidoXml  = _lectorXml.LeerDesdeStream(entryMs),
                    Asunto        = asunto,
                    Remitente     = remitente,
                    FechaCorreo   = fecha,
                });
            }
            else if (entry.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                using var entryMs = new MemoryStream();
                using (var s = entry.Open())
                    await s.CopyToAsync(entryMs, ct);

                resultado.Add(new AdjuntoCorreo
                {
                    TipoAdjunto   = "PDF",
                    NombreArchivo = entry.Name,
                    ContenidoPdf  = entryMs.ToArray(),
                    Asunto        = asunto,
                    Remitente     = remitente,
                    FechaCorreo   = fecha,
                });
            }
        }

        return resultado;
    }
}
