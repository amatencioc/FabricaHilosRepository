using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.RecursosHumanos;

[Authorize]
[Route("RecursosHumanos/ConcentracionSobretiempoArea")]
public class ConcentracionSobretiempoAreaController : OracleBaseController
{
    private readonly IConcentracionSobretiempoAreaService _service;
    private readonly ILogger<ConcentracionSobretiempoAreaController> _logger;

    public ConcentracionSobretiempoAreaController(
        IConcentracionSobretiempoAreaService service,
        ILogger<ConcentracionSobretiempoAreaController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        var hoy = DateTime.Today;
        ViewBag.AnoIni = hoy.Year;
        ViewBag.MesIni = hoy.Month;
        ViewBag.AnoFin = hoy.Year;
        ViewBag.MesFin = hoy.Month;
        return View("~/Views/RecursosHumanos/Indicadores/ConcentracionSobretiempoArea/Index.cshtml");
    }

    [HttpGet("Kpi")]
    public async Task<IActionResult> Kpi(int anoIni, int mesIni, int anoFin, int mesFin)
    {
        try
        {
            var vm = await _service.ObtenerKpiAsync(anoIni, mesIni, anoFin, mesFin);
            return PartialView("~/Views/RecursosHumanos/Indicadores/ConcentracionSobretiempoArea/_KpiDashboard.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener KPI ConcentraciónSobretiempoArea ({AnoIni}/{MesIni} - {AnoFin}/{MesFin})",
                anoIni, mesIni, anoFin, mesFin);
            return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
        }
    }
}
