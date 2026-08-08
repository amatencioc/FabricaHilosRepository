using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services.Ventas;

namespace FabricaHilos.Controllers.Ventas
{
    [Authorize]
    public class ReporteReclamosController : OracleBaseController
    {
        private readonly IReporteReclamosService _service;
        private readonly ILogger<ReporteReclamosController> _logger;

        public ReporteReclamosController(
            IReporteReclamosService service,
            ILogger<ReporteReclamosController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        public IActionResult Index()
        {
            return View("~/Views/Ventas/_ReporteReclamos/Index.cshtml");
        }

        // ────────────────────────────────────────────────────────
        // Endpoints JSON
        // ────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> DatosPorMes(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerPorMesAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosPorFamilia(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerPorFamiliaAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosPorCliente(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerPorClienteAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosIndicadores(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerIndicadoresAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosMotivos(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerMotivosAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosListado(DateTime? fechaInicio, DateTime? fechaFin, string? cliente, string? vendedor, string? estado)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerListadoAsync(fi, ff, cliente, vendedor, estado);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosParametrosCombo(string tipo)
        {
            var data = await _service.ObtenerParametrosComboAsync(tipo);
            return Json(data);
        }

        // ────────────────────────────────────────────────────────
        // Helper
        // ────────────────────────────────────────────────────────
        private static (DateTime fi, DateTime ff) ResolverFechas(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var ff = fechaFin    ?? DateTime.Today;
            var fi = fechaInicio ?? new DateTime(ff.Year, 1, 1);
            return (fi, ff);
        }
    }
}
