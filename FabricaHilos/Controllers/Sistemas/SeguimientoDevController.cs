using FabricaHilos.Services.Sistemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Sistemas
{
    [Authorize]
    [Route("Sistemas/SeguimientoDev")]
    public class SeguimientoDevController : OracleBaseController
    {
        private readonly ISeguimientoDevService            _service;
        private readonly ILogger<SeguimientoDevController> _logger;

        public SeguimientoDevController(
            ISeguimientoDevService service,
            ILogger<SeguimientoDevController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index() =>
            View("~/Views/Sistemas/Indicadores/SeguimientoDev/Index.cshtml");

        [HttpGet("DatosDashboard")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> DatosDashboard(DateTime? fechaInicio, DateTime? fechaFin, string? responsable = null, string? tipoMotivo = null)
        {
            try
            {
                var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
                var data     = await _service.ObtenerDashboardAsync(fi, ff, responsable, tipoMotivo);
                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Dashboard SeguimientoDev ({FI} - {FF})", fechaInicio, fechaFin);
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
