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
        private readonly IMenuService _menuService;
        private readonly ILogger<CreditosCobranzaController> _logger;
        private readonly IWebHostEnvironment _env;

        public CreditosCobranzaController(
            INivelMorosidadService nivelMorosidadService,
            IMenuService menuService,
            ILogger<CreditosCobranzaController> logger,
            IWebHostEnvironment env)
        {
            _nivelMorosidadService = nivelMorosidadService;
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

            // Limpiar filas de datos (22 y 23) para los 12 meses
            for (int col = 4; col <= 15; col++)
            {
                ws.Cell(22, col).Value = Blank.Value;
                ws.Cell(23, col).Value = Blank.Value;
            }

            // Escribir datos: columna = mes (4=ENE, 5=FEB, ... 15=DIC)
            foreach (var d in data)
            {
                int col = d.Mes + 3; // mes 1 → col 4, mes 12 → col 15
                ws.Cell(22, col).Value = (double)d.VencSoles;
                ws.Cell(23, col).Value = (double)d.SaldoSoles;
            }

            // Ocultar filas de montos (22=vencido, 23=saldo total)
            ws.Row(22).Hide();
            ws.Row(23).Hide();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var fileName = $"NivelMorosidad_{fi:yyyyMM}_{ff:yyyyMM}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

            }
        }
