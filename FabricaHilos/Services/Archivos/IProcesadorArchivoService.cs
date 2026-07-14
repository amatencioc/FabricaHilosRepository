namespace FabricaHilos.Services.Archivos;

// ── Tipos de archivo que el servicio puede manejar ────────────────────────────
public enum TipoArchivo
{
    Imagen,      // JPG, PNG, WEBP  → re-renderiza con ImageSharp (destruye payloads embebidos)
    Documento,   // PDF             → verifica magic bytes %PDF y guarda sin modificar
    Multimedia,  // MP4, WEBM       → verifica magic bytes y guarda sin modificar
    Generico     // Cualquier otro tipo permitido por el perfil → solo whitelist + tamaño
}

// ── Perfil de subida — define qué acepta cada módulo ─────────────────────────
/// <summary>
/// Describe las reglas de aceptación para un contexto de subida específico.
/// Cada módulo crea su propio perfil estático con las extensiones, tipos y límites que necesita.
/// </summary>
public sealed class PerfilArchivo
{
    /// <summary>Extensiones permitidas en minúsculas con punto, ej: ".jpg", ".pdf"</summary>
    public required string[] ExtensionesPermitidas { get; init; }

    /// <summary>Tamaño máximo en bytes. Por defecto 20 MB.</summary>
    public long MaxBytes { get; init; } = 20 * 1024 * 1024;

    /// <summary>Número máximo de archivos por operación. Por defecto 10.</summary>
    public int MaxArchivos { get; init; } = 10;

    /// <summary>
    /// Indica si las imágenes deben re-renderizarse con ImageSharp.
    /// Si es false se guardan directo (útil para miniaturas ya procesadas).
    /// </summary>
    public bool ProcesarImagenes { get; init; } = true;

    /// <summary>Ancho/alto máximo al redimensionar imágenes. Por defecto 1600 px.</summary>
    public int MaxLadoImagen { get; init; } = 1600;

    /// <summary>Calidad JPEG al comprimir imágenes. Por defecto 75.</summary>
    public int CalidadJpeg { get; init; } = 75;

    // ── Perfiles predefinidos reutilizables ───────────────────────────────────

    /// <summary>Imágenes + PDF — módulos de documentos (Activo Fijo, Reclamos, Logística…)</summary>
    public static readonly PerfilArchivo ImagenYPdf = new()
    {
        ExtensionesPermitidas = [".jpg", ".jpeg", ".png", ".webp", ".pdf"],
        MaxBytes    = 20 * 1024 * 1024,
        MaxArchivos = 10
    };

    /// <summary>Solo imágenes — módulos de fotos de campo (Seguridad, Inspecciones…)</summary>
    public static readonly PerfilArchivo SoloImagen = new()
    {
        ExtensionesPermitidas = [".jpg", ".jpeg", ".png", ".webp"],
        MaxBytes    = 20 * 1024 * 1024,
        MaxArchivos = 10
    };

    /// <summary>PDF solamente</summary>
    public static readonly PerfilArchivo SoloPdf = new()
    {
        ExtensionesPermitidas = [".pdf"],
        MaxBytes              = 50 * 1024 * 1024,
        MaxArchivos           = 5,
        ProcesarImagenes      = false
    };

    /// <summary>Multimedia — video + imagen + PDF (Capacitación…)</summary>
    public static readonly PerfilArchivo Multimedia = new()
    {
        ExtensionesPermitidas = [".mp4", ".webm", ".pdf", ".jpg", ".jpeg", ".png", ".webp"],
        MaxBytes              = 500 * 1024 * 1024,
        MaxArchivos           = 3,
        ProcesarImagenes      = true
    };

    /// <summary>Documentos office + PDF (Logística, importaciones…)</summary>
    public static readonly PerfilArchivo DocumentoOficina = new()
    {
        ExtensionesPermitidas = [".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".zip"],
        MaxBytes              = 50 * 1024 * 1024,
        MaxArchivos           = 10,
        ProcesarImagenes      = false
    };
}

// ── Resultado de procesar un archivo ─────────────────────────────────────────
public sealed record ResultadoArchivo(
    bool   Ok,
    string NombreArchivo,
    string? Error          = null,
    long   BytesOriginales = 0,
    long   BytesFinales    = 0);

// ── Interfaz pública ──────────────────────────────────────────────────────────
/// <summary>
/// Servicio centralizado de subida de archivos.
/// Aplica: whitelist de extensiones, magic bytes, límite de tamaño,
/// re-render de imágenes (ImageSharp), sanitización de nombre y path-traversal.
/// </summary>
public interface IProcesadorArchivoService
{
    /// <summary>
    /// Valida y guarda un único archivo en <paramref name="carpetaDestino"/> con el
    /// nombre <paramref name="nombreBase"/> (sin extensión — el servicio asigna la correcta).
    /// </summary>
    Task<ResultadoArchivo> GuardarAsync(
        IFormFile      archivo,
        string         carpetaDestino,
        string         nombreBase,
        PerfilArchivo  perfil);

    /// <summary>
    /// Guarda una colección de archivos. Devuelve un resultado por cada uno.
    /// </summary>
    Task<IReadOnlyList<ResultadoArchivo>> GuardarVariosAsync(
        IEnumerable<IFormFile> archivos,
        string                 carpetaDestino,
        string                 nombreBase,
        PerfilArchivo          perfil);

    /// <summary>
    /// Resuelve el Content-Type correcto a partir de la extensión del archivo en disco.
    /// </summary>
    string ObtenerContentType(string extension);

    /// <summary>
    /// Verifica que <paramref name="rutaArchivo"/> esté dentro de <paramref name="carpetaRaiz"/>.
    /// Lanza <see cref="UnauthorizedAccessException"/> si detecta path traversal.
    /// </summary>
    void ValidarPathSeguro(string rutaArchivo, string carpetaRaiz);
}
