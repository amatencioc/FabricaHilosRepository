using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using FabricaHilos.Services.Sgc;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class DashboardSgcController : Controller
    {
        private readonly IDashboardSgcService _dashService;
        private readonly ILogger<DashboardSgcController> _logger;

        public DashboardSgcController(
            IDashboardSgcService dashService,
            ILogger<DashboardSgcController> logger)
        {
            _dashService = dashService;
            _logger      = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OracleUser")))
            {
                _logger.LogWarning("Sesión Oracle expirada en DashboardSGC. Redirigiendo al login.");
                TempData["Warning"] = "Su sesión Oracle ha expirado. Por favor, inicie sesión nuevamente.";
                context.Result = RedirectToAction("Login", "Account",
                    new { returnUrl = Request.Path + Request.QueryString });
            }
        }

        public IActionResult Index()
        {
            return View();
        }

        // ─────────────────────────────────────────────────────────
        // Endpoints JSON
        // ─────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> DatosKpi(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerKpiAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosPorEstado(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerPorEstadoAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosEvolucionMensual(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerEvolucionMensualAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosTopClientes(DateTime? fechaInicio, DateTime? fechaFin, int top = 10)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerTopClientesAsync(fi, ff, top);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosTopArticulos(DateTime? fechaInicio, DateTime? fechaFin, int top = 15)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerTopArticulosAsync(fi, ff, top);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosPorVendedor(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerPorVendedorAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosPorMoneda(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerPorMonedaAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosSucursalCliente(DateTime? fechaInicio, DateTime? fechaFin, int top = 10)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerSucursalClienteAsync(fi, ff, top);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosDespachos(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerDespachosAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosRiesgo(int dias = 30)
        {
            var data = await _dashService.ObtenerPedidosEnRiesgoAsync(dias);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosTicketCliente(DateTime? fechaInicio, DateTime? fechaFin, int top = 15)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerTicketPorClienteAsync(fi, ff, top);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosCicloCierre(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerCicloCierreAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosRecompra(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerRecompraAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosConcentracion(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerConcentracionRiesgoAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosPorZona(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerPorZonaAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosMixProducto(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _dashService.ObtenerMixProductoAsync(fi, ff);
            return Json(data);
        }

        // ─────────────────────────────────────────────────────────
        // Helper
        // ─────────────────────────────────────────────────────────
        private static (DateTime fi, DateTime ff) ResolverFechas(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var ff = fechaFin    ?? DateTime.Today;
            var fi = fechaInicio ?? new DateTime(ff.Year, ff.Month, 1);
            return (fi, ff);
        }
    }
}
