using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services.RecursosHumanos;
using FabricaHilos.Models.RecursosHumanos;

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
            else if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value > fechaHasta.Value)
            {
                // Búsqueda robusta: si el usuario invierte el rango, se corrige automáticamente
                (fechaDesde, fechaHasta) = (fechaHasta, fechaDesde);
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

        // ========== BUSCAR MASIVO (AJAX) — v2.0 ==========
        // Sin nombre, solo por rango de fechas + categoría (EMPLEADO/OBRERO/TODOS).
        // Trae TODOS los empleados/obreros con registro en el rango, sin límite.

        [HttpGet("BuscarMasivo")]
        public async Task<IActionResult> BuscarMasivo(string? desde, string? hasta, string? categoria = "TODOS")
        {
            if (!DateTime.TryParse(desde, out var fechaDesde) || !DateTime.TryParse(hasta, out var fechaHasta))
                return Json(new { ok = false, mensaje = "Debe indicar un rango de fechas válido (Desde y Hasta)." });

            if (fechaDesde.Date > fechaHasta.Date)
                (fechaDesde, fechaHasta) = (fechaHasta, fechaDesde);

            categoria = (categoria ?? "TODOS").Trim().ToUpperInvariant();
            if (categoria != "TODOS" && categoria != "EMPLEADO" && categoria != "OBRERO")
                categoria = "TODOS";

            try
            {
                var (ok, mensaje, data) = await _findEmpleadoService.BuscarMasivoAsync(fechaDesde.Date, fechaHasta.Date, categoria);
                if (!ok)
                    return Json(new { ok = false, mensaje });

                return Json(new { ok = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en FindEmpleado.BuscarMasivo: {Desde}-{Hasta} ({Categoria})", desde, hasta, categoria);
                return Json(new { ok = false, mensaje = "Error al consultar la búsqueda masiva." });
            }
        }

        // ========== EXPORTAR EXCEL — búsqueda individual ==========
        // Reutiliza BuscarAsync con los mismos parámetros de la búsqueda mostrada en
        // pantalla, para exportar exactamente lo que el usuario está viendo.

        [HttpGet("ExportarExcel")]
        public async Task<IActionResult> ExportarExcel(string? q, string? tipo = "CODIGO", string? desde = null, string? hasta = null)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest("Ingrese un valor de búsqueda.");

            tipo = (tipo ?? "CODIGO").Trim().ToUpperInvariant();
            if (!TiposValidos.Contains(tipo))
                return BadRequest("Tipo de búsqueda inválido (CODIGO/DNI/NOMBRE).");

            DateTime? fechaDesde = null;
            DateTime? fechaHasta = null;
            if (DateTime.TryParse(desde, out var d)) fechaDesde = d.Date;
            if (DateTime.TryParse(hasta, out var h)) fechaHasta = h.Date;
            if (fechaDesde.HasValue != fechaHasta.HasValue)
            {
                fechaDesde = null;
                fechaHasta = null;
            }
            else if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value > fechaHasta.Value)
            {
                (fechaDesde, fechaHasta) = (fechaHasta, fechaDesde);
            }

            try
            {
                var (ok, mensaje, data) = await _findEmpleadoService.BuscarAsync(q.Trim(), tipo, fechaDesde, fechaHasta);
                if (!ok || data == null)
                    return BadRequest(mensaje ?? "Empleado no encontrado.");

                var bytes = FindEmpleadoExcelBuilder.GenerarExcelIndividual(data);
                var fileName = $"Empleado_{(data.CodAquarius ?? data.CodSig ?? "SinCodigo")}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en FindEmpleado.ExportarExcel: {Q} ({Tipo})", q, tipo);
                return BadRequest("Error al generar el archivo Excel.");
            }
        }

        // ========== EXPORTAR EXCEL — búsqueda masiva ==========

        [HttpGet("ExportarExcelMasivo")]
        public async Task<IActionResult> ExportarExcelMasivo(string? desde, string? hasta, string? categoria = "TODOS")
        {
            if (!DateTime.TryParse(desde, out var fechaDesde) || !DateTime.TryParse(hasta, out var fechaHasta))
                return BadRequest("Debe indicar un rango de fechas válido (Desde y Hasta).");

            if (fechaDesde.Date > fechaHasta.Date)
                (fechaDesde, fechaHasta) = (fechaHasta, fechaDesde);

            categoria = (categoria ?? "TODOS").Trim().ToUpperInvariant();
            if (categoria != "TODOS" && categoria != "EMPLEADO" && categoria != "OBRERO")
                categoria = "TODOS";

            try
            {
                var (ok, mensaje, data) = await _findEmpleadoService.BuscarMasivoAsync(fechaDesde.Date, fechaHasta.Date, categoria);
                if (!ok)
                    return BadRequest(mensaje ?? "No se pudo completar la búsqueda masiva.");

                var bytes = FindEmpleadoExcelBuilder.GenerarExcelMasivo(data, fechaDesde.Date, fechaHasta.Date);
                var fileName = $"BusquedaMasiva_{fechaDesde:yyyyMMdd}_{fechaHasta:yyyyMMdd}_{DateTime.Now:HHmm}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en FindEmpleado.ExportarExcelMasivo: {Desde}-{Hasta} ({Categoria})", desde, hasta, categoria);
                return BadRequest("Error al generar el archivo Excel de la búsqueda masiva.");
            }
        }
    }
}
