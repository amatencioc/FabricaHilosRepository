using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using FabricaHilos.Filters;
using FabricaHilos.Services.Ventas;

namespace FabricaHilos.Controllers
{
    [Authorize]
    [VentasAuthorize]
    public class DashboardComercialController : Controller
    {
        private readonly IDashboardComercialService _service;
        private readonly ILogger<DashboardComercialController> _logger;

        public DashboardComercialController(
            IDashboardComercialService service,
            ILogger<DashboardComercialController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OracleUser")))
            {
                _logger.LogWarning("Sesión Oracle expirada en Dashboard Comercial. Redirigiendo al login.");
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
        public async Task<IActionResult> DatosImportePorAsesor(DateTime? fechaInicio, DateTime? fechaFin, string? moneda)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerImportePorAsesorAsync(fi, ff, moneda ?? "D");
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosDetalleImporte(DateTime? fechaInicio, DateTime? fechaFin, string? moneda, string? asesor, string? mes)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            if (string.IsNullOrEmpty(asesor) || string.IsNullOrEmpty(mes))
                return Json(new List<object>());

            var data = await _service.ObtenerDetalleImportePorAsesorAsync(fi, ff, moneda ?? "D", asesor, mes);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosCantidadKgPorAsesor(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerCantidadKgPorAsesorAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosNroClientesPorAsesor(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerNroClientesPorAsesorAsync(fi, ff);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosDetalleClientes(DateTime? fechaInicio, DateTime? fechaFin, string? moneda, string? asesor, string? mes)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            if (string.IsNullOrEmpty(asesor) || string.IsNullOrEmpty(mes))
                return Json(new List<object>());

            var data = await _service.ObtenerDetalleClientesPorAsesorAsync(fi, ff, moneda ?? "D", asesor, mes);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DatosTopClientesPorAsesor(DateTime? fechaInicio, DateTime? fechaFin, string? moneda, int top = 3)
        {
            var (fi, ff) = ResolverFechas(fechaInicio, fechaFin);
            var data = await _service.ObtenerTopClientesPorAsesorAsync(fi, ff, moneda ?? "D", top);
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
