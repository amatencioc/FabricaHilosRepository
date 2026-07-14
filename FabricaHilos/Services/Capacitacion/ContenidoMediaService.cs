using Microsoft.AspNetCore.StaticFiles;

namespace FabricaHilos.Services.Capacitacion;

/// <summary>
/// Gestion segura de archivos multimedia (videos, PDFs, documentos) FUERA de wwwroot.
/// La ruta base se configura en appsettings.json -> LMS:MediaBasePath
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
    /// Guarda un archivo multimedia con validacion de extension, MIME y tamano.
    /// Devuelve la ruta relativa, la ClaveMedia (GUID sin guiones) y el MIME.
    /// ClaveMedia es el nombre del archivo sin extension: llave inmutable BD<->disco.
    /// </summary>
    public async Task<(bool ok, string msg, string clave, string ruta, string mime)> GuardarArchivoAsync(
        IFormFile archivo, int idCurso)
    {
        if (archivo == null || archivo.Length == 0)
            return (false, "El archivo esta vacio.", "", "", "");

        var nombreOri = Path.GetFileName(archivo.FileName);
        var ext       = Path.GetExtension(nombreOri).ToLowerInvariant();

        if (!ExtensionesPermitidas.Contains(ext))
            return (false, $"Extension '{ext}' no permitida.", "", "", "");

        long maxBytes = (_extVideo.Contains(ext) ? _maxVideoMb
                       : _extImg.Contains(ext)   ? _maxImgMb
                       : _maxDocMb) * 1024L * 1024L;

        if (archivo.Length > maxBytes)
            return (false, $"El archivo supera el limite ({maxBytes / 1024 / 1024} MB).", "", "", "");

        var mime = ObtenerMime(ext);
        if (string.IsNullOrEmpty(mime))
            return (false, "No se pudo determinar el tipo MIME del archivo.", "", "", "");

        var carpeta = Path.Combine(_basePath, $"curso_{idCurso}");
        Directory.CreateDirectory(carpeta);

        // ClaveMedia = GUID sin guiones: llave inmutable que vincula BD con archivo en disco
        var clave          = Guid.NewGuid().ToString("N");
        var nombreServidor = $"{clave}{ext}";
        var rutaCompleta   = Path.Combine(carpeta, nombreServidor);

        await using var fs = new FileStream(rutaCompleta, FileMode.Create, FileAccess.Write);
        await archivo.CopyToAsync(fs);

        var rutaRelativa = Path.Combine($"curso_{idCurso}", nombreServidor).Replace('\\', '/');
        return (true, "Archivo guardado.", clave, rutaRelativa, mime);
    }

    /// <summary>
    /// Obtiene el stream del archivo por su ClaveMedia (GUID) + extension.
    /// Mas seguro que usar la ruta relativa directamente.
    /// </summary>
    public (bool ok, FileStream? fs, string mime, string nombreDescarga) ObtenerArchivoPorClave(
        string clave, string ext, string nombreOriginal)
    {
        if (string.IsNullOrWhiteSpace(clave) || clave.Contains("..") || clave.Contains('/') || clave.Contains('\\'))
            return (false, null, "", "");

        ext = ext.ToLowerInvariant();
        if (!ext.StartsWith('.')) ext = "." + ext;

        foreach (var carpeta in Directory.EnumerateDirectories(_basePath, "curso_*"))
        {
            var rutaCompleta = Path.Combine(carpeta, $"{clave}{ext}");
            if (File.Exists(rutaCompleta))
            {
                var mime     = ObtenerMime(ext);
                var descarga = string.IsNullOrEmpty(nombreOriginal) ? $"{clave}{ext}" : nombreOriginal;
                return (true, new FileStream(rutaCompleta, FileMode.Open, FileAccess.Read, FileShare.Read), mime, descarga);
            }
        }
        return (false, null, "", "");
    }

    /// <summary>Obtiene el stream del archivo por ruta relativa. Verificar acceso antes de llamar.</summary>
    public (bool ok, FileStream? fs, string mime, string nombreDescarga) ObtenerArchivo(
        string rutaRelativa, string nombreOriginal)
    {
        var nombreSeguro = rutaRelativa.Replace("..", "").TrimStart('/', '\\');
        var rutaCompleta = Path.GetFullPath(Path.Combine(_basePath, nombreSeguro));

        if (!rutaCompleta.StartsWith(Path.GetFullPath(_basePath), StringComparison.OrdinalIgnoreCase))
            return (false, null, "", "");

        if (!File.Exists(rutaCompleta))
            return (false, null, "", "");

        var ext      = Path.GetExtension(rutaCompleta).ToLowerInvariant();
        var mime     = ObtenerMime(ext);
        var descarga = string.IsNullOrEmpty(nombreOriginal) ? Path.GetFileName(rutaCompleta) : nombreOriginal;
        return (true, new FileStream(rutaCompleta, FileMode.Open, FileAccess.Read, FileShare.Read), mime, descarga);
    }

    /// <summary>Elimina el archivo por ClaveMedia (mas seguro que por ruta relativa).</summary>
    public void EliminarPorClave(string clave, string ext)
    {
        if (string.IsNullOrWhiteSpace(clave) || clave.Contains("..")) return;
        if (!ext.StartsWith('.')) ext = "." + ext;

        foreach (var carpeta in Directory.EnumerateDirectories(_basePath, "curso_*"))
        {
            var rutaCompleta = Path.Combine(carpeta, $"{clave}{ext}");
            if (File.Exists(rutaCompleta)) { File.Delete(rutaCompleta); return; }
        }
    }

    public void EliminarArchivo(string rutaRelativa)
    {
        if (string.IsNullOrEmpty(rutaRelativa)) return;
        var nombreSeguro = rutaRelativa.Replace("..", "").TrimStart('/', '\\');
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
