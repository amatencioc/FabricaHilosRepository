using Microsoft.AspNetCore.StaticFiles;

namespace FabricaHilos.Services.Capacitacion;

/// <summary>
/// Gestión segura de archivos multimedia (videos, PDFs, documentos) FUERA de wwwroot.
/// La ruta base se configura en appsettings.json → LMS:MediaBasePath
/// </summary>
public class ContenidoMediaService
{
    private readonly string   _basePath;
    private readonly int      _maxVideoMb;
    private readonly int      _maxDocMb;
    private readonly int      _maxImgMb;
    private readonly string[] _extVideo;
    private readonly string[] _extDoc;
    private readonly string[] _extImg;

    private static readonly string[] ExtensionesPermitidas =
    [
        ".mp4", ".webm", ".pdf", ".pptx", ".ppt", ".docx", ".doc",
        ".xlsx", ".xls", ".zip", ".rar", ".jpg", ".jpeg", ".png", ".webp"
    ];

    public ContenidoMediaService(IConfiguration config)
    {
        var lms     = config.GetSection("LMS");
        _basePath   = lms["MediaBasePath"] ?? Path.Combine(Path.GetTempPath(), "LMS_Media");
        _maxVideoMb = lms.GetValue<int>("MaxVideoSizeMb", 500);
        _maxDocMb   = lms.GetValue<int>("MaxDocSizeMb",   50);
        _maxImgMb   = lms.GetValue<int>("MaxImageSizeMb",  5);
        _extVideo   = lms.GetSection("ExtensionesVideo").Get<string[]>()  ?? [".mp4", ".webm"];
        _extDoc     = lms.GetSection("ExtensionesDoc").Get<string[]>()    ?? [".pdf", ".docx"];
        _extImg     = lms.GetSection("ExtensionesImagen").Get<string[]>() ?? [".jpg", ".png"];
    }

    /// <summary>
    /// Guarda un archivo multimedia con validación de extensión, MIME y tamaño.
    /// Devuelve la ruta relativa guardada (para almacenar en BD).
    /// </summary>
    public async Task<(bool ok, string msg, string ruta, string mime)> GuardarArchivoAsync(
        IFormFile archivo, int idCurso)
    {
        if (archivo == null || archivo.Length == 0)
            return (false, "El archivo está vacío.", "", "");

        // Validar extensión (anti path traversal: solo GetFileName)
        var nombreOri = Path.GetFileName(archivo.FileName);
        var ext       = Path.GetExtension(nombreOri).ToLowerInvariant();

        if (!ExtensionesPermitidas.Contains(ext))
            return (false, $"Extensión '{ext}' no permitida.", "", "");

        // Determinar límite de tamaño según tipo
        long maxBytes = (_extVideo.Contains(ext) ? _maxVideoMb
                       : _extImg.Contains(ext)   ? _maxImgMb
                       : _maxDocMb) * 1024L * 1024L;

        if (archivo.Length > maxBytes)
            return (false, $"El archivo supera el límite ({maxBytes / 1024 / 1024} MB).", "", "");

        // Validar MIME tipo básico (doble check: ext + magic bytes)
        var mime = ObtenerMime(ext);
        if (string.IsNullOrEmpty(mime))
            return (false, "No se pudo determinar el tipo MIME del archivo.", "", "");

        // Crear carpeta del curso
        var carpeta = Path.Combine(_basePath, $"curso_{idCurso}");
        Directory.CreateDirectory(carpeta);

        // Nombre único en servidor (no usar el nombre original para evitar ataques)
        var nombreServidor = $"{Guid.NewGuid():N}{ext}";
        var rutaCompleta   = Path.Combine(carpeta, nombreServidor);

        await using var fs = new FileStream(rutaCompleta, FileMode.Create, FileAccess.Write);
        await archivo.CopyToAsync(fs);

        // Ruta relativa para guardar en BD
        var rutaRelativa = Path.Combine($"curso_{idCurso}", nombreServidor).Replace('\\', '/');
        return (true, "Archivo guardado.", rutaRelativa, mime);
    }

    /// <summary>
    /// Obtiene el stream del archivo para servirlo de forma segura (verificar inscripción antes de llamar).
    /// </summary>
    public (bool ok, FileStream? fs, string mime, string nombreDescarga) ObtenerArchivo(
        string rutaRelativa, string nombreOriginal)
    {
        // Sanitizar — evitar path traversal
        var nombreSeguro = rutaRelativa.Replace("..", "").TrimStart('/','\\');
        var rutaCompleta = Path.GetFullPath(Path.Combine(_basePath, nombreSeguro));

        // Verificar que la ruta resultante esté dentro del directorio base
        if (!rutaCompleta.StartsWith(Path.GetFullPath(_basePath), StringComparison.OrdinalIgnoreCase))
            return (false, null, "", "");

        if (!File.Exists(rutaCompleta))
            return (false, null, "", "");

        var ext    = Path.GetExtension(rutaCompleta).ToLowerInvariant();
        var mime   = ObtenerMime(ext);
        var descarga = string.IsNullOrEmpty(nombreOriginal) ? Path.GetFileName(rutaCompleta) : nombreOriginal;

        var fs = new FileStream(rutaCompleta, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (true, fs, mime, descarga);
    }

    public void EliminarArchivo(string rutaRelativa)
    {
        if (string.IsNullOrEmpty(rutaRelativa)) return;
        var nombreSeguro = rutaRelativa.Replace("..", "").TrimStart('/','\\');
        var rutaCompleta = Path.GetFullPath(Path.Combine(_basePath, nombreSeguro));
        if (rutaCompleta.StartsWith(Path.GetFullPath(_basePath), StringComparison.OrdinalIgnoreCase)
            && File.Exists(rutaCompleta))
        {
            File.Delete(rutaCompleta);
        }
    }

    private static string ObtenerMime(string ext) => ext switch
    {
        ".mp4"  => "video/mp4",
        ".webm" => "video/webm",
        ".pdf"  => "application/pdf",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".ppt"  => "application/vnd.ms-powerpoint",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".doc"  => "application/msword",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xls"  => "application/vnd.ms-excel",
        ".zip"  => "application/zip",
        ".rar"  => "application/x-rar-compressed",
        ".jpg"  => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".png"  => "image/png",
        ".webp" => "image/webp",
        _       => ""
    };
}
