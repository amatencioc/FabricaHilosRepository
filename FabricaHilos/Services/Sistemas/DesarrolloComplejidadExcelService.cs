using ClosedXML.Excel;
using FabricaHilos.Models.Sistemas;
using System.Globalization;

namespace FabricaHilos.Services.Sistemas
{
    public class DesarrolloComplejidadExcelService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DesarrolloComplejidadExcelService> _logger;

        // Meta del indicador Complejidad (80 %)
        private const double META = 0.80;

        // Colores semáforo — se instancian dentro de AplicarSemaforo (igual que NivelMorosidad)
        // para evitar problemas de caché de XLColor con patrones de plantilla.

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

        // Columna de inicio de datos (D = col 4 → mes 1 = ENE)
        private const int COL_DATOS_INI = 4;   // D
        private const int COL_PROMEDIO  = 16;  // P

        // Filas de datos
        private const int FILA_ENTREGADAS = 22;
        private const int FILA_TOTAL      = 23;
        private const int FILA_RESULTADO  = 24;
        private const int FILA_META       = 25;

        public DesarrolloComplejidadExcelService(
            IWebHostEnvironment env,
            ILogger<DesarrolloComplejidadExcelService> logger)
        {
            _env    = env;
            _logger = logger;
        }

        private string PlantillaPath =>
            Path.Combine(_env.ContentRootPath, "Data", "Sistemas", "Indicadores",
                         "1. Sistemas - (OR) Indicador_Desarrollo.xlsx");

        /// <summary>
        /// Genera el Excel del indicador KPI Complejidad:
        ///  • Actualiza etiquetas de filas al contexto de requerimientos.
        ///  • Escribe datos mensuales: entregados en fecha / total / % resultado.
        ///  • Colorea RESULTADO MENSUAL: verde ≥ 80 %, rojo &lt; 80 %.
        ///  • Ajusta la META al 80 % y el semáforo de colores.
        ///  • Actualiza el mes actual en O8.
        ///  • Inserta el gráfico principal a partir de la fila 19.
        /// </summary>
        public byte[] GenerarExcel(List<string> imagenes, string periodo, DevCompDashboardDto data)
        {
            byte[] plantillaBytes;
            using (var fs = new FileStream(PlantillaPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                plantillaBytes = new byte[fs.Length];
                _ = fs.Read(plantillaBytes, 0, plantillaBytes.Length);
            }

            using var plantillaMs = new MemoryStream(plantillaBytes);
            using var wb = new XLWorkbook(plantillaMs);
            var ws = wb.Worksheets.First();

            // ── 1. Período ────────────────────────────────────────────────────
            // O8  (fila 8,  col 15) = rango del filtro, p.ej. "Ene-26 a Jun-26"
            // O10 (fila 10, col 15) = fecha fin del filtro (como fecha)
            // E8 NO se toca — la plantilla ya tiene el área correcta.
            var culture = CultureInfo.GetCultureInfo("es-PE");
            string fmtIni = data.FechaInicio.ToString("MMM-yy", culture);
            string fmtFin = data.FechaFin.ToString("MMM-yy", culture);
            fmtIni = char.ToUpper(fmtIni[0]) + fmtIni.Substring(1);
            fmtFin = char.ToUpper(fmtFin[0]) + fmtFin.Substring(1);
            ws.Cell(8,  15).Value = $"{fmtIni} a {fmtFin}";
            ws.Cell(10, 15).Value = data.FechaFin;

            // ── 2. Etiquetas propias del indicador Complejidad ─────────────────
            ws.Cell("B22").Value = "Requerimientos entregadas en fecha";
            ws.Cell("B23").Value = "Total de requerimientos";

            // ── 3. Indicador / objetivo → adaptar al 80 % ─────────────────────
            ws.Cell("L12").Value = "80%";
            ws.Cell("E12").Value = "Medir el nivel de atención de requerimientos de desarrollo";

            // ── 4. Semáforo de colores → umbral 80 % ──────────────────────────
            ws.Cell("G16").Value = "80%";
            ws.Cell("N16").Value = "80%";

            // ── 5. Datos mensuales ────────────────────────────────────────────────
            // Eliminar formatos condicionales de la plantilla que sobrescriben
            // los colores que aplicamos por código (mismo patrón que NivelMorosidad).
            ws.ConditionalFormats.RemoveAll();

            int totalEntregadas = 0;
            int totalRequerimientos = 0;

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

                totalEntregadas     += mes.AtMismoMes;
                totalRequerimientos += mes.Recibidos;
            }

            // ── 6. Promedio anual (columna P) ─────────────────────────────────
            if (totalRequerimientos > 0)
            {
                ws.Cell(FILA_ENTREGADAS, COL_PROMEDIO).Value = totalEntregadas;
                ws.Cell(FILA_ENTREGADAS, COL_PROMEDIO).Style.NumberFormat.Format = "0";

                ws.Cell(FILA_TOTAL, COL_PROMEDIO).Value = totalRequerimientos;
                ws.Cell(FILA_TOTAL, COL_PROMEDIO).Style.NumberFormat.Format = "0";

                double pctAnual   = (double)totalEntregadas / totalRequerimientos;
                var celdaAnual    = ws.Cell(FILA_RESULTADO, COL_PROMEDIO);
                celdaAnual.Value  = pctAnual;
                celdaAnual.Style.NumberFormat.Format = "0%";
                AplicarSemaforo(celdaAnual, pctAnual, META);

                ws.Cell(FILA_META, COL_PROMEDIO).Value = META;
                ws.Cell(FILA_META, COL_PROMEDIO).Style.NumberFormat.Format = "0%";
            }

            // ── 7. Imagen del gráfico mensual ─────────────────────────────────
            if (imagenes.Count > 0 && !string.IsNullOrWhiteSpace(imagenes[0]))
            {
                try
                {
                    var raw      = imagenes[0].Contains(',') ? imagenes[0].Split(',')[1] : imagenes[0];
                    var pngBytes = Convert.FromBase64String(raw);
                    using var ms = new MemoryStream(pngBytes);

                    var pic   = ws.AddPicture(ms);
                    int origW = pic.Width;
                    int origH = pic.Height;

                    const int maxW  = 1350;
                    const int maxH  = 380;
                    double scale    = Math.Min((double)maxW / origW, (double)maxH / origH);
                    int finalW      = (int)Math.Round(origW * scale);
                    int finalH      = (int)Math.Round(origH * scale);

                    pic.MoveTo(ws.Cell(19, 2), 5, 5).WithSize(finalW, finalH);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo insertar chartMesCompl");
                }
            }

            using var outMs = new MemoryStream();
            wb.SaveAs(outMs);
            return outMs.ToArray();
        }
    }
}

