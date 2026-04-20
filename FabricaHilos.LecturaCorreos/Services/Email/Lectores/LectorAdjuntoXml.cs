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
    /// Lee el XML desde un MemoryStream con detección automática de encoding.
    /// Estrategia de lectura en orden:
    ///   1. UTF-8/UTF-16 con detección de BOM (StreamReader con detectEncodingFromByteOrderMarks).
    ///   2. Strip explícito del carácter BOM U+FEFF si quedó en el string tras la decodificación
    ///      (ocurre cuando el correo reenvía el XML como texto en lugar de adjunto binario).
    ///   3. Fallback a ISO-8859-1 si tras el strip el contenido no empieza con '&lt;'
    ///      (cubre proveedores como Facele y OSEs que emiten en Latin-1 sin declaración de encoding).
    ///   4. Segundo strip de BOM sobre el resultado ISO-8859-1 por si el BOM quedó como bytes
    ///      mal interpretados como Latin-1.
    /// </summary>
    public string LeerDesdeStream(MemoryStream ms)
    {
        ms.Position = 0;
        using var reader = new StreamReader(ms, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var contenido = reader.ReadToEnd();

        // Strip defensivo del BOM como carácter (U+FEFF / U+FFFE).
        // StreamReader lo elimina al leer bytes, pero si el XML llegó como texto en el cuerpo
        // del correo o fue re-serializado, el BOM puede aparecer como carácter en el string.
        contenido = EliminarBom(contenido);

        if (contenido.TrimStart().StartsWith('<'))
            return contenido;

        // Fallback ISO-8859-1: para proveedores que generan XML Latin-1 sin BOM ni declaración.
        ms.Position = 0;
        using var isoReader = new StreamReader(
            ms, System.Text.Encoding.GetEncoding("ISO-8859-1"),
            detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return EliminarBom(isoReader.ReadToEnd());
    }

    /// <summary>
    /// Elimina el carácter BOM (U+FEFF byte-order mark o U+FFFE reverse BOM)
    /// del inicio del string. También elimina whitespace previo al primer '&lt;'.
    /// </summary>
    private static string EliminarBom(string s)
    {
        // Eliminar BOM UTF-8 (U+FEFF) y BOM UTF-16 invertido (U+FFFE) que puedan
        // haber quedado como caracteres Unicode dentro del string .NET.
        int inicio = 0;
        while (inicio < s.Length && (s[inicio] == '\uFEFF' || s[inicio] == '\uFFFE'))
            inicio++;
        return inicio == 0 ? s : s[inicio..];
    }
}
