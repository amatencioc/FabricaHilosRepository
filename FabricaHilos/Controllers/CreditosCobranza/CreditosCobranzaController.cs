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
        private readonly IMenuService _menuService;
        private readonly ILogger<CreditosCobranzaController> _logger;
        private readonly IWebHostEnvironment _env;

        public CreditosCobranzaController(
            INivelMorosidadService nivelMorosidadService,
            INivelTiempoService    nivelTiempoService,
            IMenuService menuService,
            ILogger<CreditosCobranzaController> logger,
            IWebHostEnvironment env)
        {
            _nivelMorosidadService = nivelMorosidadService;
            _nivelTiempoService    = nivelTiempoService;
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
    }
}
