using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using FabricaHilos.Services.CreditosCobranza;
using FabricaHilos.Services;
using Microsoft.AspNetCore.Hosting;

namespace FabricaHilos.Controllers.CreditosCobranza
{
    [Authorize]
    public class CreditosCobranzaController : OracleBaseController
    {
        private readonly INivelMorosidadService _nivelMorosidadService;
        private readonly INivelTiempoService    _nivelTiempoService;
        private readonly IValorizadoNoVendidoService _valorizadoNoVendidoService;
        private readonly IMenuService _menuService;
        private readonly ILogger<CreditosCobranzaController> _logger;
        private readonly IWebHostEnvironment _env;

        public CreditosCobranzaController(
            INivelMorosidadService nivelMorosidadService,
            INivelTiempoService    nivelTiempoService,
            IValorizadoNoVendidoService valorizadoNoVendidoService,
            IMenuService menuService,
            ILogger<CreditosCobranzaController> logger,
            IWebHostEnvironment env)
        {
            _nivelMorosidadService = nivelMorosidadService;
            _nivelTiempoService    = nivelTiempoService;
            _valorizadoNoVendidoService = valorizadoNoVendidoService;
            _menuService = menuService;
            _logger = logger;
            _env = env;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult NivelMorosidad()
        {
            return View("~/Views/CreditosCobranza/NivelMorosidad/Index.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> DatosNivelMorosidad(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var fi = fechaInicio ?? new DateTime(DateTime.Today.Year, 1, 1);
            var ff = fechaFin    ?? DateTime.Today;
            var data = await _nivelMorosidadService.ObtenerNivelMorosidadAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> ExportarNivelMorosidad(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var fi = fechaInicio ?? new DateTime(DateTime.Today.Year, 1, 1);
            var ff = fechaFin    ?? DateTime.Today;
            var data = await _nivelMorosidadService.ObtenerNivelMorosidadAsync(fi, ff);

            var plantillaPath = Path.Combine(_env.ContentRootPath, "Data", "CreditoCobranza",
                "17. Creditos y Cobranzas - (GC) Nivel de morosidad 2026.xlsx");

            using var wb = new XLWorkbook(plantillaPath);
            var ws = wb.Worksheets.First();

            // Fechas del periodo
            ws.Cell(8, 15).Value  = fi;
            ws.Cell(10, 15).Value = ff;

            // Plantilla confirmada:
            //   Fila 22 = Partidas vencidas (datos auxiliares)
            //   Fila 23 = Total partidas    (datos auxiliares)
            //   Fila 24 = RESULTADO MENSUAL (indicador coloreado)
            //   Columnas: 4=ENE, 5=FEB, ..., 15=DIC, 16=Promedio Anual
            const int FILA_VENC    = 22;
            const int FILA_SALDO   = 23;
            const int FILA_RESULT  = 24;

            // Limpiar las tres filas para los 12 meses
            for (int col = 4; col <= 15; col++)
            {
                ws.Cell(FILA_VENC,   col).Value = Blank.Value;
                ws.Cell(FILA_SALDO,  col).Value = Blank.Value;
                ws.Cell(FILA_RESULT, col).Value = Blank.Value;
                ws.Cell(FILA_RESULT, col).Style.Fill.PatternType = XLFillPatternValues.None;
            }

            var colorVerde = XLColor.FromHtml("#2e7d32");
            var colorRojo  = XLColor.FromHtml("#c62828");
            var colorTexto = XLColor.White;
            const double META_MOROSIDAD = 10.0;

            // Eliminar formatos condicionales que sobreescriben nuestros colores
            ws.ConditionalFormats.RemoveAll();

            foreach (var d in data)
            {
                int col = d.Mes + 3; // mes 1 → col 4, mes 12 → col 15
                ws.Cell(FILA_VENC,  col).Value = (double)d.VencSoles;
                ws.Cell(FILA_SALDO, col).Value = (double)d.SaldoSoles;

                // Escribir y colorear el indicador en la fila RESULTADO MENSUAL
                var indCell = ws.Cell(FILA_RESULT, col);
                indCell.Value = (double)d.IndSoles / 100.0;
                indCell.Style.NumberFormat.Format = "0.00%";
                var cumple = (double)d.IndSoles <= META_MOROSIDAD;
                indCell.Style.Fill.PatternType = XLFillPatternValues.Solid;
                indCell.Style.Fill.SetBackgroundColor(cumple ? colorVerde : colorRojo);
                indCell.Style.Font.SetFontColor(colorTexto);
                indCell.Style.Font.Bold            = true;
                indCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Ocultar filas de montos auxiliares
            ws.Row(FILA_VENC).Hide();
            ws.Row(FILA_SALDO).Hide();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var fileName = $"NivelMorosidad_{fi:yyyyMM}_{ff:yyyyMM}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ── Nivel de Tiempo Promedio ──────────────────────────────────────────────

        public IActionResult NivelTiempo()
        {
            return View("~/Views/CreditosCobranza/NivelTiempo/Index.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> DatosNivelTiempo(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var fi = fechaInicio ?? new DateTime(DateTime.Today.Year, 1, 1);
            var ff = fechaFin    ?? DateTime.Today;
            var data = await _nivelTiempoService.ObtenerNivelTiempoAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> ExportarNivelTiempo(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var fi = fechaInicio ?? new DateTime(DateTime.Today.Year, 1, 1);
            var ff = fechaFin    ?? DateTime.Today;
            var data = await _nivelTiempoService.ObtenerNivelTiempoAsync(fi, ff);

            var plantillaPath = Path.Combine(_env.ContentRootPath, "Data", "CreditoCobranza",
                "18. Creditos y Cobranzas - (OR) Tiempo promedio de cuentas por cobrar 2026.xlsx");

            using var wb = new XLWorkbook(plantillaPath);
            var ws = wb.Worksheets.First();

            // Fechas del periodo
            ws.Cell(8,  15).Value = fi;
            ws.Cell(10, 15).Value = ff;

            // Plantilla confirmada:
            //   Fila 22 = CxC total       (datos auxiliares)
            //   Fila 23 = Ventas a crédito (datos auxiliares)
            //   Fila 24 = RESULTADO MENSUAL (indicador coloreado)
            //   Columnas: 4=ENE, 5=FEB, ..., 15=DIC
            const int FILA_CXC_T      = 22;
            const int FILA_VTA_T      = 23;
            const int FILA_RESULT_T   = 24;

            // Limpiar las tres filas para los 12 meses
            for (int col = 4; col <= 15; col++)
            {
                ws.Cell(FILA_CXC_T,    col).Value = Blank.Value;
                ws.Cell(FILA_VTA_T,    col).Value = Blank.Value;
                ws.Cell(FILA_RESULT_T, col).Value = Blank.Value;
                ws.Cell(FILA_RESULT_T, col).Style.Fill.PatternType = XLFillPatternValues.None;
            }

            // Escribir datos: columna = mes (4=ENE, 5=FEB, ... 15=DIC)
            var colorVerdeT = XLColor.FromHtml("#2e7d32");
            var colorRojoT  = XLColor.FromHtml("#c62828");
            var colorTextoT = XLColor.White;
            const double META_TIEMPO = 45.0;

            // Eliminar formatos condicionales que sobreescriben nuestros colores
            ws.ConditionalFormats.RemoveAll();

            foreach (var d in data)
            {
                int col = d.Mes + 3; // mes 1 → col 4, mes 12 → col 15
                ws.Cell(FILA_CXC_T, col).Value = (double)d.SaldoSoles;
                ws.Cell(FILA_VTA_T, col).Value = (double)d.VtaSoles;

                // Escribir y colorear el indicador en la fila RESULTADO MENSUAL
                var indCell = ws.Cell(FILA_RESULT_T, col);
                indCell.Value = (double)d.IndSoles;
                indCell.Style.NumberFormat.Format = "0";
                var cumpleT = (double)d.IndSoles <= META_TIEMPO;
                indCell.Style.Fill.PatternType = XLFillPatternValues.Solid;
                indCell.Style.Fill.SetBackgroundColor(cumpleT ? colorVerdeT : colorRojoT);
                indCell.Style.Font.SetFontColor(colorTextoT);
                indCell.Style.Font.Bold            = true;
                indCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Ocultar filas de montos auxiliares
            ws.Row(FILA_CXC_T).Hide();
            ws.Row(FILA_VTA_T).Hide();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var fileName = $"NivelTiempo_{fi:yyyyMM}_{ff:yyyyMM}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ── Valorizado No Vendido ─────────────────────────────────────────────────────

        public IActionResult ValorizadoNoVendido()
        {
            return View("~/Views/CreditosCobranza/ValorizadoNoVendido/Index.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> DatosValorizadoNoVendido(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var fi = fechaInicio ?? new DateTime(DateTime.Today.Year, 1, 1);
            var ff = fechaFin    ?? DateTime.Today;
            var data = await _valorizadoNoVendidoService.ObtenerValorizadoNoVendidoAsync(fi, ff);
            return Json(data);
        }

        [HttpPost]
        public async Task<IActionResult> ExportarValorizadoNoVendido(DateTime? fechaInicio, DateTime? fechaFin, [FromBody] ExportarValorizadoNoVendidoRequest? req)
        {
            var fi = fechaInicio ?? new DateTime(DateTime.Today.Year, 1, 1);
            var ff = fechaFin    ?? DateTime.Today;
            var data = await _valorizadoNoVendidoService.ObtenerValorizadoNoVendidoAsync(fi, ff);

            var plantillaPath = Path.Combine(_env.ContentRootPath, "Data", "CreditoCobranza",
                "19 - Creditos y Cobranzas - No valorizado 2026.xlsx");

            byte[] plantillaBytes;
            using (var fs = new FileStream(plantillaPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                plantillaBytes = new byte[fs.Length];
                fs.ReadExactly(plantillaBytes, 0, plantillaBytes.Length);
            }

            using var plantillaMs = new MemoryStream(plantillaBytes);
            using var wb = new XLWorkbook(plantillaMs);
            var ws = wb.Worksheets.First();

            // Fechas del periodo
            ws.Cell(8,  15).Value = fi;
            ws.Cell(10, 15).Value = ff;

            // Llenar filas "Kg No Vendidos" (22) y "Kg Vendidos" (23), columnas D (ENE) a O (DIC)
            const int FILA_KG_NO_VENDIDOS = 22;
            const int FILA_KG_VENDIDOS    = 23;

            for (int col = 4; col <= 15; col++)
            {
                ws.Cell(FILA_KG_NO_VENDIDOS, col).Value = Blank.Value;
                ws.Cell(FILA_KG_VENDIDOS,    col).Value = Blank.Value;
            }

            foreach (var d in data)
            {
                int col = d.Mes + 3; // mes 1 → col 4 (D), mes 12 → col 15 (O)
                ws.Cell(FILA_KG_NO_VENDIDOS, col).Value = (double)d.DiferenciaKg;
                ws.Cell(FILA_KG_VENDIDOS,    col).Value = (double)d.KgVendidos;
            }

            // Insertar la imagen del gráfico (capturada desde la vista) exactamente
            // en el recuadro "RESULTADOS (GRÁFICOS Y TABLAS)": fila 19, columnas B a P.
            var imagenGrafico = req?.ImagenGrafico;
            if (!string.IsNullOrWhiteSpace(imagenGrafico))
            {
                try
                {
                    var raw      = imagenGrafico.Contains(',') ? imagenGrafico.Split(',')[1] : imagenGrafico;
                    var pngBytes = Convert.FromBase64String(raw);
                    using var picMs = new MemoryStream(pngBytes);

                    var pic = ws.AddPicture(picMs);
                    int origW = pic.Width;
                    int origH = pic.Height;

                    // Ancho disponible: columnas B (2) a P (16); alto: 1 fila alta (fila 19)
                    const int maxW = 1900;
                    const int maxH = 620;
                    double scale = Math.Min((double)maxW / origW, (double)maxH / origH);
                    int finalW = (int)Math.Round(origW * scale);
                    int finalH = (int)Math.Round(origH * scale);

                    pic.MoveTo(ws.Cell(19, 2), 5, 5)
                       .WithSize(finalW, finalH);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo insertar la imagen del gráfico de Valorizado No Vendido");
                }
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var fileName = $"ValorizadoNoVendido_{fi:yyyyMM}_{ff:yyyyMM}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }

    public class ExportarValorizadoNoVendidoRequest
    {
        public string? ImagenGrafico { get; set; }
    }
}
