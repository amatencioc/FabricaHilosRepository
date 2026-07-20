using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Models.Ventas.Cotizacion;
using FabricaHilos.Services.Ventas.Cotizacion;

namespace FabricaHilos.Controllers.Ventas;

/// <summary>
/// Frontend del motor de costeo/cotización (PKG_COT):
///   - Index: listado combinado de cotizaciones reales (CT) y simulaciones (SM).
///   - Simular: Camino A — calculadora libre con línea de tiempo en vivo, guardable como simulación.
///   - Detalle: línea de tiempo (estimado por ítem) + edición por sección + historial + eliminar/duplicar.
/// Ver copilot-instructions.md (sección PKG_COT) para las reglas de negocio replicadas en el servicio.
/// </summary>
[Authorize]
[Route("Ventas/Cotizacion")]
public class CotizacionController : OracleBaseController
{
    private readonly ICotizacionService _service;
    private readonly ILogger<CotizacionController> _logger;

    public CotizacionController(ICotizacionService service, ILogger<CotizacionController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private string UsuarioActual =>
        HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "SISTEMA";

    /// <summary>
    /// Ejecuta una operación asíncrona que produce datos y envuelve el resultado en
    /// { ok, [dataKey]: data } o { ok: false, error } ante una excepción, evitando repetir
    /// el mismo bloque try/catch/log en cada endpoint JSON del controlador.
    /// </summary>
    private async Task<IActionResult> EjecutarJsonAsync<T>(Func<Task<T>> operacion, string dataKey, string mensajeError)
    {
        try
        {
            var data = await operacion();
            return Json(new Dictionary<string, object?> { ["ok"] = true, [dataKey] = data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, mensajeError);
            return Json(new { ok = false, error = ex.Message });
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LISTADO
    // ══════════════════════════════════════════════════════════════════════════

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? buscar = null, bool incluirEliminadas = false, int page = 1)
    {
        const int pageSize = 15;
        var (items, total) = await _service.ListarAsync(buscar, incluirEliminadas, page, pageSize);

        var vm = new CotizacionIndexViewModel
        {
            Cotizaciones = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Buscar = buscar,
            IncluirEliminadas = incluirEliminadas,
        };
        return View(vm);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CAMINO A — SIMULAR (calculadora libre)
    // ══════════════════════════════════════════════════════════════════════════

    [HttpGet("Simular")]
    public IActionResult Simular()
    {
        return View(new CotizacionParametros());
    }

    [HttpGet("BuscarTitulos")]
    public Task<IActionResult> BuscarTitulos(string? texto) =>
        EjecutarJsonAsync(() => _service.BuscarTitulosAsync(texto), "data", "Error al buscar títulos");

    [HttpGet("BuscarMateriaPrima")]
    public Task<IActionResult> BuscarMateriaPrima(string? texto) =>
        EjecutarJsonAsync(() => _service.BuscarMateriaPrimaAsync(texto), "data", "Error al buscar materia prima");

    /// <summary>Ficha técnica de ruta VIGENTE (mantenida por Preparatoria en Ventas/RutaTecnica) para el
    /// título+intensidad seleccionados en Simular.cshtml. Muestra siempre la última (aún no hay nada guardado).</summary>
    [HttpGet("RutaTecnicaVigente")]
    public Task<IActionResult> RutaTecnicaVigente(string? tituloCod, string? intensidadCod) =>
        EjecutarJsonAsync(() => _service.ObtenerRutaTecnicaVigenteAsync(tituloCod, intensidadCod), "data",
            "Error al obtener la ficha técnica de ruta vigente");

    /// <summary>Catálogo de referencia (COT_KB + PARAMCOS) — hojas "Auxiliares"/"Gas Natural" del
    /// Excel manual. Dato GLOBAL, no depende de los parámetros de la cotización.</summary>
    [HttpGet("Auxiliares")]
    public Task<IActionResult> Auxiliares() =>
        EjecutarJsonAsync(() => _service.ObtenerAuxiliaresAsync(), "data",
            "Error al obtener los auxiliares y servicios de referencia");

    [HttpPost("CalcularSimulacion")]
    public Task<IActionResult> CalcularSimulacion([FromBody] CotizacionParametros parametros) =>
        EjecutarJsonAsync(() => _service.CalcularSimulacionAsync(parametros), "timeline",
            "Error al calcular simulación de cotización");

    [HttpPost("DetalleCalculo")]
    public Task<IActionResult> DetalleCalculo([FromBody] CotizacionParametros parametros) =>
        EjecutarJsonAsync(() => _service.ObtenerDetalleCalculoAsync(parametros), "detalle",
            "Error al obtener el detalle del cálculo de cotización");

    /// <summary>Comparativo por tonalidad (hoja "Resumen" del Excel): evalúa las 6 tonalidades
    /// con los mismos parámetros, para el Camino A (Simular.cshtml).</summary>
    [HttpPost("CompararTonalidades")]
    public Task<IActionResult> CompararTonalidades([FromBody] CotizacionParametros parametros) =>
        EjecutarJsonAsync(() => _service.CompararTonalidadesAsync(parametros), "comparativo",
            "Error al comparar tonalidades de cotización");

    public class GuardarSimulacionRequest
    {
        public long? Numero { get; set; }
        public CotizacionParametros Parametros { get; set; } = new();
        public string? Observacion { get; set; }
    }

    [HttpPost("GuardarSimulacion")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarSimulacion([FromBody] GuardarSimulacionRequest request)
    {
        var accion = request.Numero is null or 0 ? "CREACION" : "RECALCULO";
        return EjecutarJsonAsync(
            () => _service.GuardarSimulacionAsync(request.Numero, request.Parametros, UsuarioActual, accion, request.Observacion, null),
            "numero", "Error al guardar simulación de cotización");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DETALLE (real o simulación) — línea de tiempo por ítem + historial
    // ══════════════════════════════════════════════════════════════════════════

    [HttpGet("Detalle")]
    public async Task<IActionResult> Detalle(string tipoDoc, int serie, long numero)
    {
        var vm = await _service.ObtenerDetalleCompletoAsync(tipoDoc, serie, numero);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost("RecalcularItem")]
    public Task<IActionResult> RecalcularItem([FromBody] CotizacionItemEdicionDto edicion) =>
        EjecutarJsonAsync(() => _service.RecalcularItemAsync(edicion), "timeline",
            "Error al recalcular ítem de cotización");

    /// <summary>Comparativo por tonalidad para un ítem (real o simulación) aún no guardado (Camino B, Detalle.cshtml).</summary>
    [HttpPost("CompararTonalidadesItem")]
    public Task<IActionResult> CompararTonalidadesItem([FromBody] CotizacionItemEdicionDto edicion) =>
        EjecutarJsonAsync(() => _service.CompararTonalidadesItemAsync(edicion), "comparativo",
            "Error al comparar tonalidades de ítem de cotización");

    public class GuardarEdicionItemRequest
    {
        public string TipoDoc { get; set; } = "CT";
        public int Serie { get; set; }
        public long Numero { get; set; }
        public int Item { get; set; }
        public CotizacionItemEdicionDto Edicion { get; set; } = new();
        public string? Observacion { get; set; }
    }

    [HttpPost("GuardarEdicionItem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarEdicionItem([FromBody] GuardarEdicionItemRequest request)
    {
        try
        {
            await _service.GuardarEdicionItemRealAsync(
                request.TipoDoc, request.Serie, request.Numero, request.Item,
                request.Edicion, UsuarioActual, request.Observacion);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar edición de ítem de cotización {TipoDoc}-{Serie}-{Numero}-{Item}",
                request.TipoDoc, request.Serie, request.Numero, request.Item);
            return Json(new { ok = false, error = ex.Message });
        }
    }

    public class GuardarEdicionSimulacionRequest
    {
        public long Numero { get; set; }
        public CotizacionItemEdicionDto Edicion { get; set; } = new();
        public string? Observacion { get; set; }
    }

    [HttpPost("GuardarEdicionSimulacion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarEdicionSimulacion([FromBody] GuardarEdicionSimulacionRequest request)
    {
        try
        {
            await _service.GuardarEdicionSimulacionAsync(request.Numero, request.Edicion, UsuarioActual, request.Observacion);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar edición de simulación {Numero}", request.Numero);
            return Json(new { ok = false, error = ex.Message });
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HISTORIAL
    // ══════════════════════════════════════════════════════════════════════════

    [HttpGet("Historial")]
    public async Task<IActionResult> Historial(string tipoDoc, int serie, long numero, int? item = null)
    {
        try
        {
            var historial = await _service.ObtenerHistorialAsync(tipoDoc, serie, numero, item);
            return Json(historial);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el historial de cotización {TipoDoc}-{Serie}-{Numero}", tipoDoc, serie, numero);
            return StatusCode(500, Array.Empty<object>());
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ELIMINAR / RESTAURAR / DUPLICAR
    // ══════════════════════════════════════════════════════════════════════════

    [HttpPost("Eliminar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(string tipoDoc, int serie, long numero, string? observacion = null)
    {
        try
        {
            await _service.EliminarAsync(tipoDoc, serie, numero, UsuarioActual, observacion);
            TempData["Success"] = "La cotización fue eliminada (puede restaurarla desde el historial).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar cotización {TipoDoc}-{Serie}-{Numero}", tipoDoc, serie, numero);
            TempData["Error"] = "No se pudo eliminar la cotización.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Restaurar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restaurar(string tipoDoc, int serie, long numero)
    {
        try
        {
            await _service.RestaurarAsync(tipoDoc, serie, numero, UsuarioActual);
            TempData["Success"] = "La cotización fue restaurada.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al restaurar cotización {TipoDoc}-{Serie}-{Numero}", tipoDoc, serie, numero);
            TempData["Error"] = "No se pudo restaurar la cotización.";
        }
        return RedirectToAction(nameof(Detalle), new { tipoDoc, serie, numero });
    }

    [HttpPost("Duplicar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicar(string tipoDoc, int serie, long numero, int item)
    {
        try
        {
            var nuevoNumero = await _service.DuplicarItemComoSimulacionAsync(tipoDoc, serie, numero, item, UsuarioActual);
            TempData["Success"] = $"Se creó la simulación SM-{nuevoNumero} a partir de este ítem.";
            return RedirectToAction(nameof(Detalle), new { tipoDoc = "SM", serie = 0, numero = nuevoNumero });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al duplicar ítem {TipoDoc}-{Serie}-{Numero}-{Item}", tipoDoc, serie, numero, item);
            TempData["Error"] = "No se pudo duplicar el ítem.";
            return RedirectToAction(nameof(Detalle), new { tipoDoc, serie, numero });
        }
    }
}
