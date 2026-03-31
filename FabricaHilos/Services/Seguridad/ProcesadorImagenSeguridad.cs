using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace FabricaHilos.Services.Seguridad
{
    public class ProcesadorImagenSeguridad
    {
        private readonly string _rutaSeguridad;

        private static readonly string[] _extensionesPermitidas = { ".jpg", ".jpeg", ".png", ".webp" };

        public ProcesadorImagenSeguridad(string rutaSeguridad)
        {
            _rutaSeguridad = rutaSeguridad;
        }

        /// <summary>
        /// Valida, redimensiona (máx. 1600 px de lado), comprime a JPEG 75 % y guarda la imagen
        /// en la ruta de red configurada. Devuelve el nombre de archivo generado.
        /// </summary>
        public async Task<string> GuardarYOptimizarImagenAsync(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0)
                throw new ArgumentException("No se recibió ningún archivo.");

            var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (!_extensionesPermitidas.Contains(ext))
                throw new InvalidOperationException(
                    $"Formato no permitido. Use: {string.Join(", ", _extensionesPermitidas)}.");

            const int maxLado = 1600;
            const int calidad = 75;

            using var stream = archivo.OpenReadStream();
            using var image = await Image.LoadAsync(stream);

            // Redimensionar manteniendo relación de aspecto si supera el máximo
            if (image.Width > maxLado || image.Height > maxLado)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxLado, maxLado)
                }));
            }

            // Nombre único con extensión .jpg
            var nombreArchivo = $"{Guid.NewGuid():N}.jpg";
            var rutaDestino = Path.Combine(_rutaSeguridad, nombreArchivo);

            var encoder = new JpegEncoder { Quality = calidad };

            await using var outputStream = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write);
            await image.SaveAsJpegAsync(outputStream, encoder);

            return nombreArchivo;
        }
    }
}
