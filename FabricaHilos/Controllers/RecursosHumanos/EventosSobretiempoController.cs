using ClosedXML.Excel;
using FabricaHilos.Models.RecursosHumanos;
using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace FabricaHilos.Controllers.RecursosHumanos;

[Authorize]
[Route("RecursosHumanos/EventosSobretiempo")]
public class EventosSobretiempoController : OracleBaseController
{
    private readonly IReporteEventosSobretiempoService _service;
    private readonly ILogger<EventosSobretiempoController> _logger;

    public EventosSobretiempoController(IReporteEventosSobretiempoService service, ILogger<EventosSobretiempoController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var hoy = DateTime.Today;
        ViewBag.AnoIni = hoy.Year - 1;
        ViewBag.MesIni = hoy.Month;
        ViewBag.AnoFin = hoy.Year;
        ViewBag.MesFin = hoy.Month;
        ViewBag.GranCcostoOptions  = await _service.GetGranCcostoOptionsAsync();
        ViewBag.CentroCostoOptions = await _service.GetCentroCostoOptionsAsync();
        return View("~/Views/RecursosHumanos/Indicadores/EventosSobretiempo/Index.cshtml");
    }

    [HttpGet("Kpi")]
    public async Task<IActionResult> Kpi(int anoIni, int mesIni, int anoFin, int mesFin, string tipo = "T",
        [FromQuery] List<string>? granCcosto = null, string? centroCosto = null)
    {
        if (mesIni < 1 || mesIni > 12 || mesFin < 1 || mesFin > 12)
        {
            return BadRequest("El mes debe estar entre 1 y 12.");
        }
        if (anoIni < 2000 || anoIni > 2099 || anoFin < 2000 || anoFin > 2099)
        {
            return BadRequest("El año debe estar entre 2000 y 2099.");
        }
        if (new DateTime(anoIni, mesIni, 1) > new DateTime(anoFin, mesFin, 1))
        {
            return BadRequest("El período de inicio no puede ser posterior al período de fin.");
        }

        try
        {
            var vm = await _service.ObtenerKpiAsync(CodEmpresaAquarius, anoIni, mesIni, anoFin, mesFin, tipo, granCcosto, centroCosto);
            return PartialView("~/Views/RecursosHumanos/Indicadores/EventosSobretiempo/_KpiDashboard.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener KPI Eventos vs Sobretiempo ({AnoIni}/{MesIni} - {AnoFin}/{MesFin} Tipo:{Tipo} GranCcosto:{GranCcosto} CentroCosto:{CentroCosto})",
                anoIni, mesIni, anoFin, mesFin, tipo, string.Join(",", granCcosto ?? new List<string>()), centroCosto);
            return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
        }
    }

    // ── EXPORTAR A EXCEL ────────────────────────────────────────────────
    // Replica en Excel exactamente lo que se ve en el dashboard (mismos filtros,
    // mismas tablas: Resumen Comparativo, Detalle por Área, Detalle por Centro de
    // Costo, Detalle por Empleado, Consolidado de Eventos y las 2 Proyecciones de
    // Bolsa de HE), cada una en su propia hoja.

    [HttpGet("ExportarExcel")]
    public async Task<IActionResult> ExportarExcel(int anoIni, int mesIni, int anoFin, int mesFin, string tipo = "T",
        [FromQuery] List<string>? granCcosto = null, string? centroCosto = null)
    {
        if (mesIni < 1 || mesIni > 12 || mesFin < 1 || mesFin > 12)
        {
            return BadRequest("El mes debe estar entre 1 y 12.");
        }
        if (anoIni < 2000 || anoIni > 2099 || anoFin < 2000 || anoFin > 2099)
        {
            return BadRequest("El año debe estar entre 2000 y 2099.");
        }
        if (new DateTime(anoIni, mesIni, 1) > new DateTime(anoFin, mesFin, 1))
        {
            return BadRequest("El período de inicio no puede ser posterior al período de fin.");
        }

        EventosSobretiempoKpiViewModel vm;
        try
        {
            vm = await _service.ObtenerKpiAsync(CodEmpresaAquarius, anoIni, mesIni, anoFin, mesFin, tipo, granCcosto, centroCosto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener datos para exportar Eventos vs Sobretiempo ({AnoIni}/{MesIni} - {AnoFin}/{MesFin} Tipo:{Tipo})",
                anoIni, mesIni, anoFin, mesFin, tipo);
            return StatusCode(500, "Error al obtener los datos. Intente nuevamente.");
        }

        try
        {
            using var wb = EventosSobretiempoExcelBuilder.Construir(vm);
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            string fileName = $"EventosVsSobretiempo_{anoIni}{mesIni:D2}_{anoFin}{mesFin:D2}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar Excel de Eventos vs Sobretiempo");
            return StatusCode(500, $"Error al generar el archivo Excel: {ex.Message}");
        }
    }
}
