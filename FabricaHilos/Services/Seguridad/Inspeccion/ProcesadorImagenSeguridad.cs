using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace FabricaHilos.Services.Seguridad.Inspeccion
{
    public class ProcesadorImagenSeguridad
    {
        private readonly string _rutaSeguridad;
        private readonly ILogger? _logger;

        private static readonly string[] _extensionesPermitidas = { ".jpg", ".jpeg", ".png", ".webp" };

        public ProcesadorImagenSeguridad(string rutaSeguridad, ILogger? logger = null)
        {
            _rutaSeguridad = rutaSeguridad;
            _logger = logger;
        }

        /// <summary>
        /// Valida, redimensiona (máx. 1600 px de lado), comprime a JPEG 75 % y guarda la imagen
        /// en la ruta de red configurada con el nombre especificado.
        /// </summary>
        /// <param name="archivo">Archivo de imagen a procesar</param>
        /// <param name="nombreArchivo">Nombre personalizado del archivo (ej: "123-H.jpg")</param>
        /// <returns>Nombre del archivo guardado</returns>
        public async Task<string> GuardarYOptimizarImagenAsync(IFormFile archivo, string nombreArchivo)
        {
            if (archivo == null || archivo.Length == 0)
                throw new ArgumentException("No se recibió ningún archivo.");

            var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (!_extensionesPermitidas.Contains(ext))
                throw new InvalidOperationException(
                    $"Formato no permitido. Use: {string.Join(", ", _extensionesPermitidas)}.");

            using var stream = archivo.OpenReadStream();
            return await GuardarYOptimizarImagenAsync(stream, nombreArchivo);
        }

        /// <summary>
        /// Sobreescritura que acepta un Stream (útil para Task.Run donde IFormFile puede no ser thread-safe).
        /// </summary>
        public async Task<string> GuardarYOptimizarImagenAsync(Stream imagenStream, string nombreArchivo)
        {
            const int maxLado = 1600;
            const int calidad = 75;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            _logger?.LogWarning("▶▶ IMG: Cargando imagen desde stream ({Nombre})...", nombreArchivo);
            using var image = await Image.LoadAsync(imagenStream);
            _logger?.LogWarning("▶▶ IMG: Imagen cargada {W}x{H} ({Ms}ms)", image.Width, image.Height, sw.ElapsedMilliseconds);

            // Aplicar rotación real de píxeles según metadata EXIF y eliminar la etiqueta Orientation.
            // Oracle Forms no interpreta EXIF, así que sin esto las fotos de celular se ven giradas.
            image.Mutate(x => x.AutoOrient());
            _logger?.LogWarning("▶▶ IMG: AutoOrient aplicado {W}x{H} ({Ms}ms)", image.Width, image.Height, sw.ElapsedMilliseconds);

            // Redimensionar manteniendo relación de aspecto si supera el máximo
            if (image.Width > maxLado || image.Height > maxLado)
            {
                _logger?.LogWarning("▶▶ IMG: Redimensionando (max {Max}px)...", maxLado);
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxLado, maxLado)
                }));
                _logger?.LogWarning("▶▶ IMG: Redimensionado a {W}x{H} ({Ms}ms)", image.Width, image.Height, sw.ElapsedMilliseconds);
            }

            // Asegurar que el nombre tiene extensión .jpg
            if (!nombreArchivo.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                nombreArchivo = Path.ChangeExtension(nombreArchivo, "jpg");
            }

            var rutaDestino = Path.Combine(_rutaSeguridad, nombreArchivo);

            _logger?.LogWarning("▶▶ IMG: Creando directorio '{Ruta}'...", _rutaSeguridad);
            Directory.CreateDirectory(_rutaSeguridad);
            _logger?.LogWarning("▶▶ IMG: Directorio OK ({Ms}ms)", sw.ElapsedMilliseconds);

            var encoder = new JpegEncoder { Quality = calidad };

            _logger?.LogWarning("▶▶ IMG: Escribiendo archivo '{Destino}'...", rutaDestino);
            await using var outputStream = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write);
            await image.SaveAsJpegAsync(outputStream, encoder);
            _logger?.LogWarning("▶▶ IMG: Archivo escrito OK ({Ms}ms)", sw.ElapsedMilliseconds);

            return nombreArchivo;
        }
    }
}
