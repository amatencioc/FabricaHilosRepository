using ClosedXML.Excel;
using FabricaHilos.Models.Sistemas;

namespace FabricaHilos.Services.Sistemas
{
    public class DesarrolloExcelService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DesarrolloExcelService> _logger;

        // Meta del indicador Desarrollo (80 %)
        private const double META = 0.80;

        // Columna de inicio de datos (D = col 4 → mes 1 = ENE)
        private const int COL_DATOS_INI = 4;   // D
        private const int COL_PROMEDIO  = 16;  // P

        // Filas de datos
        private const int FILA_ENTREGADAS = 22;
        private const int FILA_TOTAL      = 23;
        private const int FILA_RESULTADO  = 24;
        private const int FILA_META       = 25;

        private static void AplicarSemaforo(IXLCell celda, double pct, double meta)
        {
            var colorVerde = XLColor.FromHtml("#2e7d32");
            var colorRojo  = XLColor.FromHtml("#c62828");
            celda.Style.Fill.PatternType = XLFillPatternValues.Solid;
            celda.Style.Fill.SetBackgroundColor(pct >= meta ? colorVerde : colorRojo);
            celda.Style.Font.SetFontColor(XLColor.White);
            celda.Style.Font.Bold            = true;
            celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

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
        /// escribe los datos mensuales (filas 22-25) y devuelve los bytes del archivo generado.
        /// </summary>
        /// <param name="imagenes">Lista de imágenes PNG en base64 (data:image/png;base64,...)</param>
        /// <param name="periodo">Texto del período para actualizar la celda de período</param>
        /// <param name="data">Datos del dashboard, incluyendo la atención mes a mes</param>
        public byte[] GenerarExcel(List<string> imagenes, string periodo, DevDashboardDto? data = null)
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

            // ── Datos mensuales (filas 22-25) ──────────────────────────────
            if (data is not null && data.AtencionMes.Count > 0)
            {
                // Eliminar formatos condicionales de la plantilla que sobrescriben
                // los colores que aplicamos por código.
                ws.ConditionalFormats.RemoveAll();

                int totalEntregadas = 0;
                int totalRecibidos  = 0;

                foreach (var mes in data.AtencionMes)
                {
                    int col = COL_DATOS_INI + (mes.Mes - 1);   // D + offset
                    if (col < COL_DATOS_INI || col >= COL_PROMEDIO) continue;

                    // Fila 22 — Entregadas en fecha (mismo mes)
                    ws.Cell(FILA_ENTREGADAS, col).Value = mes.AtMismoMes;
                    ws.Cell(FILA_ENTREGADAS, col).Style.NumberFormat.Format = "0";

                    // Fila 23 — Total
                    ws.Cell(FILA_TOTAL, col).Value = mes.Recibidos;
                    ws.Cell(FILA_TOTAL, col).Style.NumberFormat.Format = "0";

                    // Fila 24 — Resultado % con semáforo
                    double pct = mes.Recibidos > 0 ? (double)mes.AtMismoMes / mes.Recibidos : 0;
                    var celdaRes = ws.Cell(FILA_RESULTADO, col);
                    celdaRes.Value = pct;
                    celdaRes.Style.NumberFormat.Format = "0%";
                    AplicarSemaforo(celdaRes, pct, META);

                    // Fila 25 — META
                    var celdaMeta = ws.Cell(FILA_META, col);
                    celdaMeta.Value = META;
                    celdaMeta.Style.NumberFormat.Format = "0%";

                    totalEntregadas += mes.AtMismoMes;
                    totalRecibidos  += mes.Recibidos;
                }

                // Promedio anual (columna P)
                if (totalRecibidos > 0)
                {
                    ws.Cell(FILA_ENTREGADAS, COL_PROMEDIO).Value = totalEntregadas;
                    ws.Cell(FILA_ENTREGADAS, COL_PROMEDIO).Style.NumberFormat.Format = "0";

                    ws.Cell(FILA_TOTAL, COL_PROMEDIO).Value = totalRecibidos;
                    ws.Cell(FILA_TOTAL, COL_PROMEDIO).Style.NumberFormat.Format = "0";

                    double pctAnual  = (double)totalEntregadas / totalRecibidos;
                    var celdaAnual   = ws.Cell(FILA_RESULTADO, COL_PROMEDIO);
                    celdaAnual.Value = pctAnual;
                    celdaAnual.Style.NumberFormat.Format = "0%";
                    AplicarSemaforo(celdaAnual, pctAnual, META);

                    ws.Cell(FILA_META, COL_PROMEDIO).Value = META;
                    ws.Cell(FILA_META, COL_PROMEDIO).Style.NumberFormat.Format = "0%";
                }
            }

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
