using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.RecursosHumanos;

[Authorize]
[Route("RecursosHumanos/HorasExtras")]
public class HorasExtrasController : OracleBaseController
{
    private readonly IHorasExtrasService _horasExtrasService;
    private readonly ILogger<HorasExtrasController> _logger;

    public HorasExtrasController(IHorasExtrasService horasExtrasService, ILogger<HorasExtrasController> logger)
    {
        _horasExtrasService = horasExtrasService;
        _logger             = logger;
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
        return View("~/Views/RecursosHumanos/Indicadores/SobreTiempoArea/Index.cshtml");
    }

    [HttpGet("Kpi")]
    public async Task<IActionResult> Kpi(int anoIni, int mesIni, int anoFin, int mesFin, string tipo = "T")
    {
        try
        {
            var vm = await _horasExtrasService.ObtenerKpiAsync(anoIni, mesIni, anoFin, mesFin, tipo);
            return PartialView("~/Views/RecursosHumanos/Indicadores/SobreTiempoArea/_KpiDashboard.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener KPI Horas Extras ({AnoIni}/{MesIni} - {AnoFin}/{MesFin} Tipo:{Tipo})",
                anoIni, mesIni, anoFin, mesFin, tipo);
            return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
        }
    }
}
