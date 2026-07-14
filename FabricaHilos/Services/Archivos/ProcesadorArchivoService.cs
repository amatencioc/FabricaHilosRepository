using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace FabricaHilos.Services.Archivos;

/// <summary>
/// Implementación central del procesador de archivos.
///
/// Capas de seguridad aplicadas en orden:
///   1. Whitelist de extensiones (el perfil define qué se acepta)
///   2. Límite de tamaño en bytes
///   3. Verificación de magic bytes (firma binaria real del archivo)
///      — PDF   → %PDF (25 50 44 46)
///      — JPEG  → FF D8 FF
///      — PNG   → 89 50 4E 47
///      — MP4   → ftyp a los 4 bytes (66 74 79 70)
///      — WEBM  → 1A 45 DF A3
///   4. Re-render ImageSharp para imágenes (destruye cualquier payload embebido)
///   5. Sanitización del nombre de archivo
///   6. Validación de path traversal en todos los endpoints de servido
/// </summary>
public sealed class ProcesadorArchivoService : IProcesadorArchivoService
{
    private readonly ILogger<ProcesadorArchivoService> _logger;

    // ── Magic bytes por tipo ──────────────────────────────────────────────────
    private static readonly Dictionary<string, byte[]> _magicBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"]  = [0x25, 0x50, 0x44, 0x46],          // %PDF
        [".jpg"]  = [0xFF, 0xD8, 0xFF],                 // JPEG SOI
        [".jpeg"] = [0xFF, 0xD8, 0xFF],
        [".png"]  = [0x89, 0x50, 0x4E, 0x47],           // PNG header
        [".webp"] = [0x52, 0x49, 0x46, 0x46],           // RIFF (WebP)
        [".mp4"]  = [0x66, 0x74, 0x79, 0x70],           // ftyp @ offset 4
        [".webm"] = [0x1A, 0x45, 0xDF, 0xA3],           // EBML header
    };

    private static readonly HashSet<string> _extensionesImagen =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public ProcesadorArchivoService(ILogger<ProcesadorArchivoService> logger)
        => _logger = logger;

    // ── API pública ───────────────────────────────────────────────────────────

    public async Task<ResultadoArchivo> GuardarAsync(
        IFormFile     archivo,
        string        carpetaDestino,
        string        nombreBase,
        PerfilArchivo perfil)
    {
        try
        {
            Validar(archivo, perfil, out var ext);
            await VerificarMagicBytesAsync(archivo, ext);

            Directory.CreateDirectory(carpetaDestino);
            var nombre = SanitizarNombre(nombreBase, ext);
            var ruta   = Path.Combine(carpetaDestino, nombre);

            if (_extensionesImagen.Contains(ext) && perfil.ProcesarImagenes)
                await GuardarImagenAsync(archivo, ruta, perfil);
            else
                await GuardarDirectoAsync(archivo, ruta);

            var bytesFinales = new FileInfo(ruta).Length;
            _logger.LogInformation("Archivo guardado: {Nombre} ({Orig}KB → {Final}KB)",
                nombre, archivo.Length / 1024, bytesFinales / 1024);

            return new ResultadoArchivo(true, nombre, BytesOriginales: archivo.Length, BytesFinales: bytesFinales);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error procesando archivo {Nombre}", archivo?.FileName);
            return new ResultadoArchivo(false, archivo?.FileName ?? "", ex.Message);
        }
    }

    public async Task<IReadOnlyList<ResultadoArchivo>> GuardarVariosAsync(
        IEnumerable<IFormFile> archivos,
        string                 carpetaDestino,
        string                 nombreBase,
        PerfilArchivo          perfil)
    {
        var lista  = archivos.Where(f => f.Length > 0).ToList();
        var idx    = 0;
        var result = new List<ResultadoArchivo>(lista.Count);

        if (lista.Count > perfil.MaxArchivos)
        {
            result.Add(new ResultadoArchivo(false, "",
                $"Se permiten máximo {perfil.MaxArchivos} archivos por operación."));
            return result;
        }

        foreach (var archivo in lista)
        {
            var nombreBase2 = lista.Count == 1 ? nombreBase : $"{nombreBase}_{(++idx):D2}";
            result.Add(await GuardarAsync(archivo, carpetaDestino, nombreBase2, perfil));
        }
        return result;
    }

    public string ObtenerContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf"          => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"          => "image/png",
        ".webp"         => "image/webp",
        ".mp4"          => "video/mp4",
        ".webm"         => "video/webm",
        ".docx"         => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx"         => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx"         => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".doc"          => "application/msword",
        ".xls"          => "application/vnd.ms-excel",
        ".zip"          => "application/zip",
        _               => "application/octet-stream"
    };

    public void ValidarPathSeguro(string rutaArchivo, string carpetaRaiz)
    {
        var ruta = Path.GetFullPath(rutaArchivo);
        var raiz = Path.GetFullPath(carpetaRaiz);
        if (!ruta.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(ruta, raiz, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal detectado: {Ruta} fuera de {Raiz}", rutaArchivo, carpetaRaiz);
            throw new UnauthorizedAccessException("Acceso a ruta no permitido.");
        }
    }

    // ── Validaciones ──────────────────────────────────────────────────────────

    private static void Validar(IFormFile archivo, PerfilArchivo perfil, out string ext)
    {
        if (archivo is null || archivo.Length == 0)
            throw new ArgumentException("No se recibió ningún archivo.");

        if (archivo.Length > perfil.MaxBytes)
            throw new InvalidOperationException(
                $"El archivo supera el límite de {perfil.MaxBytes / 1024 / 1024} MB.");

        ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!perfil.ExtensionesPermitidas.Contains(ext))
            throw new InvalidOperationException(
                $"Formato '{ext}' no permitido. Aceptados: {string.Join(", ", perfil.ExtensionesPermitidas)}.");
    }

    private static async Task VerificarMagicBytesAsync(IFormFile archivo, string ext)
    {
        if (!_magicBytes.TryGetValue(ext, out var magic)) return; // tipo sin magic bytes definido → omitir

        // MP4: la firma "ftyp" está en el offset 4, no en el byte 0
        int offset = ext == ".mp4" ? 4 : 0;
        int total  = offset + magic.Length;

        var buffer = new byte[total];
        await using var stream = archivo.OpenReadStream();
        var leidos = await stream.ReadAsync(buffer.AsMemory(0, total));

        if (leidos < total || !buffer.AsSpan(offset, magic.Length).SequenceEqual(magic.AsSpan()))
            throw new InvalidOperationException(
                $"El archivo '{archivo.FileName}' no es un {ext.TrimStart('.').ToUpperInvariant()} válido " +
                $"(firma binaria incorrecta). Posible intento de evasión de seguridad.");
    }

    // ── Guardado ──────────────────────────────────────────────────────────────

    private static async Task GuardarDirectoAsync(IFormFile archivo, string ruta)
    {
        await using var fs = new FileStream(ruta, FileMode.Create, FileAccess.Write);
        await archivo.CopyToAsync(fs);
    }

    private static async Task GuardarImagenAsync(IFormFile archivo, string ruta, PerfilArchivo perfil)
    {
        // Nota: ruta ya tiene extensión .jpg — asignada por SanitizarNombre antes de llamar aquí
        await using var input = archivo.OpenReadStream();
        using var image = await Image.LoadAsync(input);

        // 1. Corregir orientación EXIF (fotos de celular)
        image.Mutate(x => x.AutoOrient());

        // 2. Redimensionar si supera el máximo
        if (image.Width > perfil.MaxLadoImagen || image.Height > perfil.MaxLadoImagen)
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(perfil.MaxLadoImagen, perfil.MaxLadoImagen)
            }));

        // 3. Guardar como JPEG comprimido
        var encoder = new JpegEncoder { Quality = perfil.CalidadJpeg };
        await using var output = new FileStream(ruta, FileMode.Create, FileAccess.Write);
        await image.SaveAsJpegAsync(output, encoder);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string SanitizarNombre(string nombreBase, string ext)
    {
        // Si el archivo es imagen procesada, siempre queda .jpg
        var extFinal = _extensionesImagen.Contains(ext) ? ".jpg" : ext;
        var nombre   = $"{nombreBase}{extFinal}";
        return string.Concat(nombre.Split(Path.GetInvalidFileNameChars()));
    }
}
