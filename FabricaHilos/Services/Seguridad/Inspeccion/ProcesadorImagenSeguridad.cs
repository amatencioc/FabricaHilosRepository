using FabricaHilos.Services.Archivos;

namespace FabricaHilos.Services.Seguridad.Inspeccion;

/// <summary>
/// Adaptador de Seguridad/Inspeccion sobre el servicio centralizado de archivos.
/// Mantiene la misma API publica para no romper los controladores existentes.
/// </summary>
public class ProcesadorImagenSeguridad
{
    private readonly IProcesadorArchivoService _procesador;
    private readonly string _rutaSeguridad;

    /// <summary>Perfil especifico de Seguridad: solo imagenes, max 20 MB</summary>
    public static readonly PerfilArchivo Perfil = PerfilArchivo.SoloImagen;

    public ProcesadorImagenSeguridad(
        IProcesadorArchivoService procesador,
        string rutaSeguridad,
        ILogger? logger = null)
    {
        _procesador    = procesador;
        _rutaSeguridad = rutaSeguridad;
    }

    /// <summary>
    /// Valida, redimensiona (max. 1600 px de lado), comprime a JPEG 75 % y guarda la imagen.
    /// </summary>
    public async Task<string> GuardarYOptimizarImagenAsync(IFormFile archivo, string nombreArchivo)
    {
        var nombreBase = Path.GetFileNameWithoutExtension(nombreArchivo);
        var resultado  = await _procesador.GuardarAsync(archivo, _rutaSeguridad, nombreBase, Perfil);
        if (!resultado.Ok)
            throw new InvalidOperationException(resultado.Error ?? "Error al procesar la imagen.");
        return resultado.NombreArchivo;
    }

    /// <summary>
    /// Sobreescritura que acepta un Stream.
    /// </summary>
    public async Task<string> GuardarYOptimizarImagenAsync(Stream imagenStream, string nombreArchivo)
    {
        // Copiar a MemoryStream para garantizar seekability y Length correcto.
        // El stream original puede ser non-seekable o ya estar consumido (Task.Run en InspeccionCom).
        using var ms = new MemoryStream();
        if (imagenStream.CanSeek) imagenStream.Position = 0;
        await imagenStream.CopyToAsync(ms);
        ms.Position = 0;

        var nombreBase = Path.GetFileNameWithoutExtension(nombreArchivo);
        var formFile   = new StreamFormFile(ms, nombreArchivo);
        var resultado  = await _procesador.GuardarAsync(formFile, _rutaSeguridad, nombreBase, Perfil);
        if (!resultado.Ok)
            throw new InvalidOperationException(resultado.Error ?? "Error al procesar la imagen.");
        return resultado.NombreArchivo;
    }

    // IFormFile mínimo para envolver un MemoryStream (siempre seekable)
    private sealed class StreamFormFile : IFormFile
    {
        private readonly MemoryStream _stream;
        public StreamFormFile(MemoryStream stream, string fileName)
        {
            _stream      = stream;
            FileName     = fileName;
            ContentType  = "image/jpeg";
            Name         = "file";
            Length       = stream.Length;   // siempre disponible en MemoryStream
            Headers      = new HeaderDictionary();
            ContentDisposition = $"form-data; name=\"file\"; filename=\"{fileName}\"";
        }
        public string ContentType        { get; }
        public string ContentDisposition { get; }
        public IHeaderDictionary Headers { get; }
        public long Length               { get; }
        public string Name               { get; }
        public string FileName           { get; }
        public void CopyTo(Stream target)
        {
            _stream.Position = 0;
            _stream.CopyTo(target);
        }
        public Task CopyToAsync(Stream target, CancellationToken ct)
        {
            _stream.Position = 0;
            return _stream.CopyToAsync(target, ct);
        }
        public Stream OpenReadStream()
        {
            // Devuelve SIEMPRE una copia independiente. El pipeline de validación
            // (magic bytes) abre y cierra ("await using") el stream antes de que
            // GuardarImagenAsync vuelva a llamar OpenReadStream(); si devolviéramos
            // la misma instancia de _stream, quedaría cerrada (ObjectDisposedException)
            // en la segunda lectura.
            _stream.Position = 0;
            return new MemoryStream(_stream.ToArray(), writable: false);
        }
    }
}
