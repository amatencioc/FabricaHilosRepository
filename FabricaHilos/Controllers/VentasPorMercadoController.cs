using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using FabricaHilos.Filters;
using FabricaHilos.Services.Ventas;

namespace FabricaHilos.Controllers
{
    [Authorize]
    [VentasAuthorize]
    public class VentasPorMercadoController : Controller
    {
        private readonly IVentasPorMercadoService _service;
        private readonly ILogger<VentasPorMercadoController> _logger;

        public VentasPorMercadoController(
            IVentasPorMercadoService service,
            ILogger<VentasPorMercadoController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OracleUser")))
            {
                _logger.LogWarning("Sesión Oracle expirada en Ventas por Mercado. Redirigiendo al login.");
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
