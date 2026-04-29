using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.RecursosHumanos;

[Authorize]
[Route("RecursosHumanos/EvolucionMasaSalarial")]
public class EvolucionMasaSalarialController : OracleBaseController
{
    private readonly IEvolucionMasaSalarialService _service;
    private readonly ILogger<EvolucionMasaSalarialController> _logger;

    public EvolucionMasaSalarialController(
        IEvolucionMasaSalarialService service,
        ILogger<EvolucionMasaSalarialController> logger)
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
        return View("~/Views/RecursosHumanos/Indicadores/EvolucionMasaSalarial/Index.cshtml");
    }

    [HttpGet("Kpi")]
    public async Task<IActionResult> Kpi(int anoIni, int mesIni, int anoFin, int mesFin)
    {
        try
        {
            var vm = await _service.ObtenerKpiAsync(anoIni, mesIni, anoFin, mesFin);
            return PartialView("~/Views/RecursosHumanos/Indicadores/EvolucionMasaSalarial/_KpiDashboard.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener KPI EvolucionMasaSalarial ({AnoIni}/{MesIni} - {AnoFin}/{MesFin})",
                anoIni, mesIni, anoFin, mesFin);
            return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
        }
    }
}
