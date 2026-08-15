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

        // v1.16: cliente/vendedor/estado ya llegaban en el query string desde el front
        // (getFiltros()/buildQuery(f) los env\u00eda siempre) pero se ignoraban aqu\u00ed \u2014
        // los KPIs y los gr\u00e1ficos nunca filtraban por esos 3 combos, solo el Listado.
        [HttpGet]
        public async Task<IActionResult> DatosPorMes(DateTime? fechaInicio, DateTime? fechaFin, string? cliente, string? vendedor, string? estado)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerPorMesAsync(fi, ff, cliente, vendedor, estado);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosPorFamilia(DateTime? fechaInicio, DateTime? fechaFin, string? cliente, string? vendedor, string? estado)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerPorFamiliaAsync(fi, ff, cliente, vendedor, estado);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosPorCliente(DateTime? fechaInicio, DateTime? fechaFin, string? cliente, string? vendedor, string? estado)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerPorClienteAsync(fi, ff, cliente, vendedor, estado);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosIndicadores(DateTime? fechaInicio, DateTime? fechaFin, decimal? kgAtendidos, string? cliente, string? vendedor, string? estado)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerIndicadoresAsync(fi, ff, kgAtendidos, cliente, vendedor, estado);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosMotivos(DateTime? fechaInicio, DateTime? fechaFin, string? cliente, string? vendedor, string? estado)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerMotivosAsync(fi, ff, cliente, vendedor, estado);
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
        public async Task<IActionResult> DatosParametrosCombo(string tipo, DateTime? fechaInicio, DateTime? fechaFin)
        {
            // CLIENTE/VENDEDOR se acotan al mismo rango de fechas del Listado
            // (v1.7 — evita ofrecer opciones sin ningún reclamo en el periodo visible).
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerParametrosComboAsync(tipo, fi, ff);
            return Json(data);
        }

        // ────────────────────────────────────────────────────────
        // Helper
        // ────────────────────────────────────────────────────────
        private static (DateTime fi, DateTime ff) ResolverFechas(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var ff = fechaFin    ?? DateTime.Today;
            var fi = fechaInicio ?? new DateTime(DateTime.Today.Year, 1, 1);
            return (fi, ff);
        }
    }
}
