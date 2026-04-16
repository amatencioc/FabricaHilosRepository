using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Filters;
using FabricaHilos.Services.Ventas;

namespace FabricaHilos.Controllers
{
    [Authorize]
    [VentasAuthorize]
    public class DashboardGerencialController : OracleBaseController
    {
        private readonly IDashboardGerencialService _service;
        private readonly ILogger<DashboardGerencialController> _logger;

        public DashboardGerencialController(
            IDashboardGerencialService service,
            ILogger<DashboardGerencialController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        // ─────────────────────────────────────────────────────────
        // Endpoints JSON
        // ─────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> DatosVentasPorMercado(DateTime? fechaInicio, DateTime? fechaFin, string? moneda)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerVentasPorMercadoAsync(fi, ff, moneda ?? "D");
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosDetallePorPais(DateTime? fechaInicio, DateTime? fechaFin, string? moneda, string? mercado)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerDetallePorPaisAsync(fi, ff, moneda ?? "D", mercado);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosDetallePorDepartamento(DateTime? fechaInicio, DateTime? fechaFin, string? moneda)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerDetallePorDepartamentoAsync(fi, ff, moneda ?? "D");
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosDetallePorDistrito(DateTime? fechaInicio, DateTime? fechaFin, string? moneda, string? departamento)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerDetallePorDistritoAsync(fi, ff, moneda ?? "D", departamento ?? "");
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosCiudadesPorPais(DateTime? fechaInicio, DateTime? fechaFin, string? moneda, string? codigoPais)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerCiudadesPorPaisAsync(fi, ff, moneda ?? "D", codigoPais ?? "");
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosEvolucionMensual(DateTime? fechaInicio, DateTime? fechaFin, string? moneda)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerEvolucionMensualAsync(fi, ff, moneda ?? "D");
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosPaisesISO()
        {
            var data = await _service.ObtenerPaisesIsoAsync();
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosKgMensual(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerKgMensualAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosTopHiladosImporte(DateTime? fechaInicio, DateTime? fechaFin, string? moneda, int top = 5)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerTopHiladosImporteAsync(fi, ff, moneda ?? "D", top);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosVentasPorGiro(DateTime? fechaInicio, DateTime? fechaFin, string? moneda)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerVentasPorGiroAsync(fi, ff, moneda ?? "D");
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosTopHiladosKg(DateTime? fechaInicio, DateTime? fechaFin, int top = 5)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerTopHiladosKgAsync(fi, ff, top);
            return Json(data);
        }

        // ─────────────────────────────────────────────────────────
        // Helper
        // ─────────────────────────────────────────────────────────
        private static (DateTime fi, DateTime ff) ResolverFechas(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var ff = fechaFin    ?? DateTime.Today;
            var fi = fechaInicio ?? new DateTime(ff.Year, 1, 1);
            return (fi, ff);
        }
    }
}
