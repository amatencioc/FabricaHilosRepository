using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace FabricaHilos.Services.Contabilidad;

/// <summary>
/// Procesa imágenes de Activo Fijo antes de guardarlas en el servidor:
/// — AutoOrient (corrige rotación EXIF de fotos tomadas desde celular)
/// — Redimensiona a máx. 1600 px por lado manteniendo relación de aspecto
/// — Convierte y comprime a JPEG 75 % de calidad
/// — Los PDF se guardan sin procesar (pasan directamente al disco)
/// Compatible con Oracle Forms Legacy (no interpreta EXIF).
/// </summary>
public class ProcesadorImagenActivoFijo
{
    private const int    MaxLado  = 1600;
    private const int    Calidad  = 75;
    private const long   MaxBytes = 20 * 1024 * 1024; // 20 MB

    private static readonly HashSet<string> _imageExt =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private static readonly HashSet<string> _extPermitidas =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

    private readonly ILogger<ProcesadorImagenActivoFijo> _logger;

    public ProcesadorImagenActivoFijo(ILogger<ProcesadorImagenActivoFijo> logger)
    {
        _logger = logger;
    }

    // ── Resultado del procesamiento ───────────────────────────────────────

    public record ResultadoProcesamiento(
        string NombreArchivo,
        long   BytesOriginales,
        long   BytesFinales,
        int    AnchoFinal,
        int    AltoFinal);

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Valida, procesa (si es imagen) y guarda el archivo en <paramref name="carpetaDestino"/>.
    /// Devuelve los metadatos del resultado.
    /// </summary>
    public async Task<ResultadoProcesamiento> ProcesarYGuardarAsync(
        IFormFile archivo,
        string    carpetaDestino,
        string    nombreBase)
    {
        if (archivo is null || archivo.Length == 0)
            throw new ArgumentException("No se recibió ningún archivo.");

        if (archivo.Length > MaxBytes)
            throw new InvalidOperationException(
                $"El archivo supera el límite de {MaxBytes / 1024 / 1024} MB.");

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!_extPermitidas.Contains(ext))
            throw new InvalidOperationException(
                $"Formato no permitido. Use: {string.Join(", ", _extPermitidas)}.");

        Directory.CreateDirectory(carpetaDestino);

        // PDF → guardar sin procesar
        if (ext == ".pdf")
        {
            var nombrePdf = SanitizarNombre($"{nombreBase}.pdf");
            var rutaPdf   = Path.Combine(carpetaDestino, nombrePdf);
            await using var fs = new FileStream(rutaPdf, FileMode.Create, FileAccess.Write);
            await archivo.CopyToAsync(fs);
            return new ResultadoProcesamiento(nombrePdf, archivo.Length, archivo.Length, 0, 0);
        }

        // Imagen → procesar con ImageSharp
        return await ProcesarImagenAsync(archivo, carpetaDestino, nombreBase);
    }

    // ── Procesamiento interno ─────────────────────────────────────────────

    private async Task<ResultadoProcesamiento> ProcesarImagenAsync(
        IFormFile archivo,
        string    carpetaDestino,
        string    nombreBase)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogDebug("ActivoFijo IMG: Cargando {Nombre} ({Bytes} bytes)", archivo.FileName, archivo.Length);

        await using var inputStream = archivo.OpenReadStream();
        using var image = await Image.LoadAsync(inputStream);

        var wOrig = image.Width;
        var hOrig = image.Height;

        // 1. Corregir orientación EXIF → Oracle Forms no interpreta EXIF
        image.Mutate(x => x.AutoOrient());

        // 2. Redimensionar si supera el máximo
        if (image.Width > MaxLado || image.Height > MaxLado)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxLado, MaxLado)
            }));
            _logger.LogDebug("ActivoFijo IMG: Redimensionado {W0}x{H0} → {W1}x{H1}",
                wOrig, hOrig, image.Width, image.Height);
        }

        // 3. Convertir a JPEG y comprimir
        var nombreJpeg = SanitizarNombre($"{nombreBase}.jpg");
        var rutaDest   = Path.Combine(carpetaDestino, nombreJpeg);

        var encoder = new JpegEncoder { Quality = Calidad };
        await using var output = new FileStream(rutaDest, FileMode.Create, FileAccess.Write);
        await image.SaveAsJpegAsync(output, encoder);

        var bytesFinales = new FileInfo(rutaDest).Length;
        _logger.LogDebug("ActivoFijo IMG: Guardado {Nombre} — {Orig}KB → {Final}KB ({Ms}ms)",
            nombreJpeg, archivo.Length / 1024, bytesFinales / 1024, sw.ElapsedMilliseconds);

        return new ResultadoProcesamiento(nombreJpeg, archivo.Length, bytesFinales, image.Width, image.Height);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string SanitizarNombre(string nombre) =>
        string.Concat(nombre.Split(Path.GetInvalidFileNameChars()));
}
