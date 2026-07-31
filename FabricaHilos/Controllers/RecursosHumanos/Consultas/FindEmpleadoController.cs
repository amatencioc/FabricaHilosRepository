using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services.RecursosHumanos;

namespace FabricaHilos.Controllers.RecursosHumanos.Consultas
{
    [Authorize]
    [Route("RecursosHumanos/FindEmpleado")]
    public class FindEmpleadoController : OracleBaseController
    {
        private readonly IFindEmpleadoService _findEmpleadoService;
        private readonly ILogger<FindEmpleadoController> _logger;

        private static readonly string[] TiposValidos = { "CODIGO", "DNI", "NOMBRE" };

        public FindEmpleadoController(
            IFindEmpleadoService findEmpleadoService,
            ILogger<FindEmpleadoController> logger)
        {
            _findEmpleadoService = findEmpleadoService;
            _logger              = logger;
        }

        // ========== INDEX — Vista de búsqueda ==========

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View("~/Views/RecursosHumanos/Consultas/FindEmpleado/Index.cshtml");
        }

        // ========== BUSCAR (AJAX) ==========

        [HttpGet("Buscar")]
        public async Task<IActionResult> Buscar(string? q, string? tipo = "CODIGO", string? desde = null, string? hasta = null)
        {            if (string.IsNullOrWhiteSpace(q))
                return Json(new { ok = false, mensaje = "Ingrese un valor de búsqueda." });

            tipo = (tipo ?? "CODIGO").Trim().ToUpperInvariant();
            if (!TiposValidos.Contains(tipo))
                return Json(new { ok = false, mensaje = "Tipo de búsqueda inválido (CODIGO/DNI/NOMBRE)." });

            // Rango de fechas opcional (afecta detalle Vigilancia + historial Eventos SIG).
            // Se requieren AMBAS fechas para aplicar el rango; si falta una, se ignora
            // y el SP aplica sus defaults (Vigilancia=HOY, Eventos=mes ant/actual/sig).
            DateTime? fechaDesde = null;
            DateTime? fechaHasta = null;
            if (DateTime.TryParse(desde, out var d)) fechaDesde = d.Date;
            if (DateTime.TryParse(hasta, out var h)) fechaHasta = h.Date;
            if (fechaDesde.HasValue != fechaHasta.HasValue)
            {
                fechaDesde = null;
                fechaHasta = null;
            }

            try
            {
                var (ok, mensaje, data) = await _findEmpleadoService.BuscarAsync(q.Trim(), tipo, fechaDesde, fechaHasta);
                if (!ok)
                    return Json(new { ok = false, mensaje });

                return Json(new { ok = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en FindEmpleado.Buscar: {Q} ({Tipo})", q, tipo);
                return Json(new { ok = false, mensaje = "Error al consultar el empleado." });
            }
        }

        // ========== SUGERIR NOMBRES (AJAX, autocompletado búsqueda por Nombre) — v1.7 ==========

        [HttpGet("SugerirNombres")]
        public async Task<IActionResult> SugerirNombres(string? q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
                return Json(new { ok = true, data = Array.Empty<object>() });

            try
            {
                var sugerencias = await _findEmpleadoService.SugerirNombresAsync(q.Trim());
                return Json(new { ok = true, data = sugerencias });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en FindEmpleado.SugerirNombres: {Q}", q);
                return Json(new { ok = false, mensaje = "Error al buscar sugerencias." });
            }
        }
    }
}
