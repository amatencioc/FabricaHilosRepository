using ClosedXML.Excel;
using FabricaHilos.Models.Sistemas;

namespace FabricaHilos.Services.Sistemas
{
    public class DesarrolloExcelService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DesarrolloExcelService> _logger;

        // Imágenes base64 de los canvas se reciben desde el cliente
        public DesarrolloExcelService(IWebHostEnvironment env, ILogger<DesarrolloExcelService> logger)
        {
            _env    = env;
            _logger = logger;
        }

        // Ruta al Excel plantilla
        private string PlantillaPath =>
            Path.Combine(_env.ContentRootPath, "Data", "Sistemas", "Indicadores",
                         "1. Sistemas - (OR) Indicador_Desarrollo.xlsx");

        /// <summary>
        /// Abre la plantilla, inserta las imágenes de gráficos a partir de la fila 18
        /// (una fila debajo de "RESULTADOS (GRÁFICOS Y TABLAS) :" en fila 17),
        /// y devuelve los bytes del archivo generado.
        /// </summary>
        /// <param name="imagenes">Lista de imágenes PNG en base64 (data:image/png;base64,...)</param>
        /// <param name="periodo">Texto del período para actualizar la celda de período</param>
        public byte[] GenerarExcel(List<string> imagenes, string periodo)
        {
            // Leer la plantilla con FileShare.ReadWrite para no fallar si está abierta en Excel.
            // File.ReadAllBytes no acepta FileShare; se usa ReadExactly para garantizar lectura completa.
            byte[] plantillaBytes;
            using (var fs = new FileStream(PlantillaPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                plantillaBytes = new byte[fs.Length];
                fs.ReadExactly(plantillaBytes, 0, plantillaBytes.Length);
            }

            using var plantillaMs = new MemoryStream(plantillaBytes);
            using var wb = new XLWorkbook(plantillaMs);
            var ws = wb.Worksheets.First();

            // Actualizar período en la celda correspondiente (fila 8 col E según plantilla)
            var celdaPeriodo = ws.Cell("E8");
            if (!string.IsNullOrWhiteSpace(periodo))
                celdaPeriodo.Value = periodo;

            // Solo exportar chartMes (índice 0) en grande dentro del recuadro RESULTADOS
            if (imagenes.Count > 0 && !string.IsNullOrWhiteSpace(imagenes[0]))
            {
                try
                {
                    var raw      = imagenes[0].Contains(',') ? imagenes[0].Split(',')[1] : imagenes[0];
                    var pngBytes = Convert.FromBase64String(raw);
                    using var ms = new MemoryStream(pngBytes);

                    var pic  = ws.AddPicture(ms);
                    int origW = pic.Width;
                    int origH = pic.Height;

                    // Escalar proporcionalmente para encajar en el recuadro RESULTADOS (B19:P19)
                    const int maxW = 1350;
                    const int maxH = 380;
                    double scale = Math.Min((double)maxW / origW, (double)maxH / origH);
                    int finalW = (int)Math.Round(origW * scale);
                    int finalH = (int)Math.Round(origH * scale);

                    pic.MoveTo(ws.Cell(19, 2), 5, 5)
                       .WithSize(finalW, finalH);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo insertar chartMes");
                }
            }

            using var outMs = new MemoryStream();
            wb.SaveAs(outMs);
            return outMs.ToArray();
        }
    }
}
