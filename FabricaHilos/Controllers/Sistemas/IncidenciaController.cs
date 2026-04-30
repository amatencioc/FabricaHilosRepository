using FabricaHilos.Services.Sistemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Sistemas
{
    [Authorize]
    [Route("Sistemas/Incidencia")]
    public class IncidenciaController : OracleBaseController
    {
        private readonly IIncidenciaService            _service;
        private readonly ILogger<IncidenciaController> _logger;

        public IncidenciaController(
            IIncidenciaService service,
            ILogger<IncidenciaController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index() =>
            View("~/Views/Sistemas/Indicadores/Incidencia/Index.cshtml");

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
                _logger.LogError(ex, "Error al obtener Dashboard Incidencias ({FI} - {FF})", fechaInicio, fechaFin);
                return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
            }
        }

        private static (DateTime fi, DateTime ff) ResolverFechas(DateTime? fi, DateTime? ff)
        {
            var f2 = ff ?? DateTime.Today;
            var f1 = fi ?? new DateTime(f2.Year, 1, 1);
            return (f1, f2);
        }
    }
}
