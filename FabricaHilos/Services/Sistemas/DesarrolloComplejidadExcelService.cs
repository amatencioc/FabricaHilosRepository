using ClosedXML.Excel;

namespace FabricaHilos.Services.Sistemas
{
    public class DesarrolloComplejidadExcelService
    {
        private readonly ILogger<DesarrolloComplejidadExcelService> _logger;

        public DesarrolloComplejidadExcelService(ILogger<DesarrolloComplejidadExcelService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Genera un archivo Excel con las imágenes de los gráficos capturados desde el dashboard.
        /// </summary>
        public byte[] GenerarExcel(List<string> imagenes, string periodo)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("KPI Desarrollo - Complejidad");

            // Encabezado
            ws.Cell("B2").Value = "INDICADOR KPI DESARROLLO - COMPLEJIDAD";
            ws.Cell("B2").Style.Font.Bold = true;
            ws.Cell("B2").Style.Font.FontSize = 14;
            ws.Cell("B2").Style.Font.FontColor = XLColor.FromHtml("#1B4D3E");

            ws.Cell("B3").Value = "Área de Sistemas — Requerimientos ponderados por complejidad";
            ws.Cell("B3").Style.Font.FontSize = 10;
            ws.Cell("B3").Style.Font.FontColor = XLColor.Gray;

            if (!string.IsNullOrWhiteSpace(periodo))
            {
                ws.Cell("B5").Value = "Período:";
                ws.Cell("B5").Style.Font.Bold = true;
                ws.Cell("C5").Value = periodo;
            }

            // Insertar imágenes de gráficos
            int fila = 8;
            for (int i = 0; i < imagenes.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(imagenes[i])) continue;
                try
                {
                    var raw      = imagenes[i].Contains(',') ? imagenes[i].Split(',')[1] : imagenes[i];
                    var pngBytes = Convert.FromBase64String(raw);
                    using var ms = new MemoryStream(pngBytes);
                    ws.AddPicture(ms)
                      .MoveTo(ws.Cell(fila, 2))
                      .WithSize(1100, 480);
                    fila += 28;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo insertar imagen {Index}", i);
                }
            }

            ws.Columns().AdjustToContents();

            using var outMs = new MemoryStream();
            wb.SaveAs(outMs);
            return outMs.ToArray();
        }
    }
}
