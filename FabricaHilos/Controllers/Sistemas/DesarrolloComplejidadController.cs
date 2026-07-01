using FabricaHilos.Services.Sistemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Sistemas
{
    [Authorize]
    [Route("Sistemas/DesarrolloComplejidad")]
    public class DesarrolloComplejidadController : OracleBaseController
    {
        private readonly IDesarrolloComplejidadService            _service;
        private readonly DesarrolloComplejidadExcelService        _excelService;
        private readonly ILogger<DesarrolloComplejidadController> _logger;

        public DesarrolloComplejidadController(
            IDesarrolloComplejidadService service,
            DesarrolloComplejidadExcelService excelService,
            ILogger<DesarrolloComplejidadController> logger)
        {
            _service      = service;
            _excelService = excelService;
            _logger       = logger;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index() =>
            View("~/Views/Sistemas/Indicadores/DesarrolloComplejidad/Index.cshtml");

        // ── Endpoint principal del dashboard ──────────────────────────────────
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
                _logger.LogError(ex,
                    "Error al obtener Dashboard DesarrolloComplejidad ({FI} - {FF})",
                    fechaInicio, fechaFin);
                return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
            }
        }

        private static (DateTime fi, DateTime ff) ResolverFechas(DateTime? fi, DateTime? ff)
        {
            var f2 = ff ?? DateTime.Today;
            var f1 = fi ?? new DateTime(f2.Year, 1, 1);
            return (f1, f2);
        }

        // ── Exportar dashboard a Excel ──────────────────────────────────
        [HttpPost("ExportarExcel")]
        public async Task<IActionResult> ExportarExcel([FromBody] ExportarExcelRequest req)
        {
            try
            {
                var (fi, ff) = ResolverFechas(req.FechaInicio, req.FechaFin);
                var data     = await _service.ObtenerDashboardAsync(fi, ff);
                var bytes    = _excelService.GenerarExcel(req.Imagenes, req.Periodo, data);
                var nombre   = $"Indicador_DesarrolloComplejidad_{DateTime.Today:yyyyMMdd}.xlsx";
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    nombre);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar Excel de DesarrolloComplejidad");
                return StatusCode(500, "Error al generar el archivo Excel.");
            }
        }
    }
}
