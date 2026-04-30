using FabricaHilos.Services.Sistemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Sistemas
{
    [Authorize]
    [Route("Sistemas/Desarrollo")]
    public class DesarrolloController : OracleBaseController
    {
        private readonly IDesarrolloService            _service;
        private readonly DesarrolloExcelService        _excelService;
        private readonly ILogger<DesarrolloController> _logger;

        public DesarrolloController(
            IDesarrolloService service,
            DesarrolloExcelService excelService,
            ILogger<DesarrolloController> logger)
        {
            _service      = service;
            _excelService = excelService;
            _logger       = logger;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index() =>
            View("~/Views/Sistemas/Indicadores/Desarrollo/Index.cshtml");

        // ── Endpoint principal: devuelve todo el dashboard en una sola llamada ──
        [HttpGet("DatosDashboard")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> DatosDashboard(DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
                var data     = await _service.ObtenerDashboardAsync(fi, ff);
                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Dashboard Desarrollo ({FI} - {FF})", fechaInicio, fechaFin);
                return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
            }
        }

        private static (DateTime fi, DateTime ff) ResolverFechas(DateTime? fi, DateTime? ff)
        {
            var f2 = ff ?? DateTime.Today;
            var f1 = fi ?? new DateTime(f2.Year, 1, 1);
            return (f1, f2);
        }

        // ── Exportar dashboard a Excel plantilla ───────────────────────────────
        [HttpPost("ExportarExcel")]
        public IActionResult ExportarExcel([FromBody] ExportarExcelRequest req)
        {
            try
            {
                var bytes = _excelService.GenerarExcel(req.Imagenes, req.Periodo);
                var nombre = $"Indicador_Desarrollo_{DateTime.Today:yyyyMMdd}.xlsx";
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    nombre);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar Excel de Desarrollo");
                return StatusCode(500, "Error al generar el archivo Excel.");
            }
        }
    }

    public class ExportarExcelRequest
    {
        public List<string> Imagenes { get; set; } = new();
        public string Periodo { get; set; } = string.Empty;
    }
}
