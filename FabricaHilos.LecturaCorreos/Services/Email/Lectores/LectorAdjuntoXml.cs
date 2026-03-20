namespace FabricaHilos.LecturaCorreos.Services.Email.Lectores;

using FabricaHilos.LecturaCorreos.Models;
using MimeKit;

public class LectorAdjuntoXml : ILectorAdjuntoXml
{
    // Límite por XML: facturas UBL no superan unos pocos KB. 10 MB es suficientemente generoso.
    private const long MaxXmlBytes = 10 * 1024 * 1024; // 10 MB

    public async Task<AdjuntoCorreo> ExtraerAsync(
        MimePart parte, string asunto, string remitente, DateTime fecha, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await parte.Content.DecodeToAsync(ms, ct);

        if (ms.Length > MaxXmlBytes)
            throw new InvalidOperationException(
                $"XML '{parte.FileName}' excede el límite de {MaxXmlBytes / (1024 * 1024)} MB " +
                $"({ms.Length / (1024.0 * 1024):F1} MB). Se omite el adjunto.");

        return new AdjuntoCorreo
        {
            TipoAdjunto   = "XML",
            NombreArchivo = parte.FileName ?? string.Empty,
            ContenidoXml  = LeerDesdeStream(ms),
            Asunto        = asunto,
            Remitente     = remitente,
            FechaCorreo   = fecha,
        };
    }

    /// <summary>
    /// Detecta y descarta BOM (UTF-8/UTF-16). Si el contenido no arranca con '&lt;'
    /// reintenta con ISO-8859-1, cobertura para proveedores como Facele y OSEs propias.
    /// </summary>
    public string LeerDesdeStream(MemoryStream ms)
    {
        ms.Position = 0;
        using var reader = new StreamReader(ms, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var contenido = reader.ReadToEnd();

        if (contenido.TrimStart().StartsWith('<'))
            return contenido;

        ms.Position = 0;
        using var isoReader = new StreamReader(
            ms, System.Text.Encoding.GetEncoding("ISO-8859-1"),
            detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return isoReader.ReadToEnd();
    }
}
