using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.RecursosHumanos;

[Authorize]
[Route("RecursosHumanos/ComparativoCostoLaboral")]
public class ComparativoCostoLaboralController : OracleBaseController
{
    private readonly IComparativoCostoLaboralService _service;
    private readonly ILogger<ComparativoCostoLaboralController> _logger;

    public ComparativoCostoLaboralController(
        IComparativoCostoLaboralService service,
        ILogger<ComparativoCostoLaboralController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        // Por defecto: Año1 = año anterior (ene–dic), Año2 = año actual (ene–mes_actual).
        // Antes estaba hardcoded a 2025/2026, lo que dejaba el filtro desfasado al pasar de año.
        var hoy = DateTime.Now;
        ViewBag.Ano1 = hoy.Year - 1;
        ViewBag.MesIniAno1 = 1;
        ViewBag.MesFinAno1 = 12;
        ViewBag.Ano2 = hoy.Year;
        ViewBag.MesIniAno2 = 1;
        ViewBag.MesFinAno2 = hoy.Month; // DateTime.Now.Month siempre está en 1..12, no requiere Math.Min.
        return View("~/Views/RecursosHumanos/Indicadores/ComparativoCostoLaboral/Index.cshtml");
    }

    [HttpGet("Kpi")]
    public async Task<IActionResult> Kpi(
        int ano1, int mesIniAno1, int mesFinAno1,
        int ano2, int mesIniAno2, int mesFinAno2,
        decimal basicoManual = 0m,
        string tipo = "T",
        [FromQuery] List<string>? areas = null)
    {
        try
        {
            var vm = await _service.ObtenerKpiAsync(
                ano1, mesIniAno1, mesFinAno1,
                ano2, mesIniAno2, mesFinAno2,
                basicoManual,
                tipo,
                areas);
            return PartialView("~/Views/RecursosHumanos/Indicadores/ComparativoCostoLaboral/_KpiDashboard.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error KPI ComparativoCostoLaboral ({A1} vs {A2}) Tipo={Tipo}", ano1, ano2, tipo);
            return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
        }
    }

    [HttpGet("Areas")]
    public async Task<IActionResult> Areas(
        int ano1, int mesIniAno1, int mesFinAno1,
        int ano2, int mesIniAno2, int mesFinAno2,
        string tipo = "T")
    {
        try
        {
            var areas = await _service.ObtenerAreasAsync(
                ano1, mesIniAno1, mesFinAno1,
                ano2, mesIniAno2, mesFinAno2,
                tipo);
            return Json(areas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Areas ComparativoCostoLaboral Tipo={Tipo}", tipo);
            return Json(Array.Empty<string>());
        }
    }
}
