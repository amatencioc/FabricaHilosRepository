using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Services.Costos;

namespace FabricaHilos.Controllers.Costos;

[Authorize]
[Route("Costos")]
public class CostosController : OracleBaseController
{
    private readonly ICostosService _service;
    private readonly ILogger<CostosController> _logger;

    // Límites razonables de negocio para evitar cálculos absurdos (margen negativo
    // o desproporcionado) que silenciosamente producirían precios sin sentido.
    private const decimal PctMargenMin = 0m;
    private const decimal PctMargenMax = 1000m;

    public CostosController(ICostosService service, ILogger<CostosController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BUSCADOR DE COTIZACIÓN → CASCADA DE COSTEO
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(int? numero)
    {
        if (numero == null)
            return View();

        if (numero <= 0)
        {
            TempData["Error"] = "El número de cotización debe ser un valor positivo.";
            return View();
        }

        try
        {
            var resultado = await _service.BuscarCotizacionAsync(numero.Value);
            if (resultado == null)
                ViewBag.NoEncontrado = numero.Value;

            return View(resultado);
        }
        catch (Exception ex)
        {
            // Nunca dejar que un fallo transitorio de BD tumbe la pantalla con un error genérico:
            // se informa al usuario y se conserva el buscador operativo.
            _logger.LogError(ex, "[Costos] Error al buscar cotización {Numero}", numero);
            TempData["Error"] = $"No se pudo buscar la cotización {numero}: {ex.Message}";
            return View();
        }
    }

    // Dispara PKG_COS_COSTEO.SP_CALCULAR_COTIZACION (ruta vigente → proyectada → regresión →
    // mezclas, resuelto por el motor) y vuelve a Index mostrando el cálculo nuevo.
    [HttpPost("Calcular")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calcular(int numero, decimal pctMargen)
    {
        if (numero <= 0)
        {
            TempData["Error"] = "Número de cotización inválido.";
            return RedirectToAction("Index", new { numero });
        }
        if (pctMargen < PctMargenMin || pctMargen > PctMargenMax)
        {
            TempData["Error"] = $"El % de margen debe estar entre {PctMargenMin} y {PctMargenMax}.";
            return RedirectToAction("Index", new { numero });
        }

        var usuario = User.Identity?.Name ?? "APP";
        try
        {
            var (idCalc, error) = await _service.CalcularAsync(numero, pctMargen, usuario);
            if (error != null)
                TempData["Error"] = $"No se pudo calcular la cotización {numero}: {error}";
            else
                TempData["Success"] = $"Cálculo generado (ID_CALC={idCalc}).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Costos] Error inesperado al calcular cotización {Numero}", numero);
            TempData["Error"] = $"Ocurrió un error inesperado al calcular la cotización {numero}. Intente nuevamente.";
        }

        return RedirectToAction("Index", new { numero });
    }
}

