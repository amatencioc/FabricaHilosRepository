using FabricaHilos.Controllers;
using FabricaHilos.Models.RecursosHumanos;
using FabricaHilos.Services.RecursosHumanos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.RecursosHumanos.Aquarius;

[Authorize]
[Route("RecursosHumanos/Aquarius/PlanillaMensual")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PlanillaMensualController : OracleBaseController
{
    private readonly IPlanillaMensualService _service;
    private readonly ILogger<PlanillaMensualController> _logger;

    public PlanillaMensualController(IPlanillaMensualService service, ILogger<PlanillaMensualController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    // ── VISTAS ───────────────────────────────────────────────────────────────

    [HttpGet("")]
    [HttpGet("Dashboard")]
    public IActionResult Dashboard() =>
        View("~/Views/RecursosHumanos/Aquarius/PlanillaMensual/Dashboard.cshtml");

    [HttpGet("Resumen")]
    public IActionResult Resumen()
    {
        ViewBag.CodEmpresaDefault = CodEmpresaAquarius;
        return View("~/Views/RecursosHumanos/Aquarius/PlanillaMensual/Resumen.cshtml");
    }

    [HttpGet("Detalle")]
    public IActionResult Detalle()
    {
        ViewBag.CodEmpresaDefault = CodEmpresaAquarius;
        return View("~/Views/RecursosHumanos/Aquarius/PlanillaMensual/Detalle.cshtml");
    }

    // ── API — MAESTROS ────────────────────────────────────────────────────────

    [HttpGet("api/Empresas")]
    public async Task<IActionResult> ApiEmpresas()
    {
        var data = await _service.ObtenerEmpresasAsync();
        return Ok(data);
    }

    [HttpGet("api/Sucursales")]
    public async Task<IActionResult> ApiSucursales([FromQuery] string codEmpresa)
    {
        if (string.IsNullOrWhiteSpace(codEmpresa)) return BadRequest("codEmpresa requerido");
        var data = await _service.ObtenerSucursalesAsync(codEmpresa);
        return Ok(data);
    }

    [HttpGet("api/TiposPlanilla")]
    public async Task<IActionResult> ApiTiposPlanilla([FromQuery] string codEmpresa)
    {
        if (string.IsNullOrWhiteSpace(codEmpresa)) return BadRequest("codEmpresa requerido");
        var data = await _service.ObtenerTiposPlanillaAsync(codEmpresa);
        return Ok(data);
    }

    [HttpGet("api/CCostos")]
    public async Task<IActionResult> ApiCCostos([FromQuery] string codEmpresa)
    {
        if (string.IsNullOrWhiteSpace(codEmpresa)) return BadRequest("codEmpresa requerido");
        var data = await _service.ObtenerCCostosAsync(codEmpresa);
        return Ok(data);
    }

    [HttpGet("api/Periodos")]
    public async Task<IActionResult> ApiPeriodos([FromQuery] string fechaInicio, [FromQuery] string fechaFinal)
    {
        if (string.IsNullOrWhiteSpace(fechaInicio) || string.IsNullOrWhiteSpace(fechaFinal))
            return BadRequest("fechaInicio y fechaFinal requeridos (DD/MM/YYYY)");
        var data = await _service.ObtenerPeriodosAsync(fechaInicio, fechaFinal);
        return Ok(data);
    }

    // ── API — REPORTES ────────────────────────────────────────────────────────

    [HttpPost("api/Resumen")]
    public async Task<IActionResult> ApiResumen([FromBody] PlanillaMensualFiltroDto filtro)
    {
        if (string.IsNullOrWhiteSpace(filtro.FechaInicio) || string.IsNullOrWhiteSpace(filtro.FechaFinal))
            return BadRequest("FechaInicio y FechaFinal requeridos");
        if (string.IsNullOrWhiteSpace(filtro.CodTipoPlanilla) || filtro.CodTipoPlanilla == "0")
            return BadRequest("Debe seleccionar un Tipo de Planilla");
        try
        {
            var data = await _service.ObtenerResumenAsync(filtro);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApiResumen error");
            return StatusCode(500, new { error = "Error al consultar el resumen. Verifique los filtros." });
        }
    }

    [HttpPost("api/Detalle")]
    public async Task<IActionResult> ApiDetalle([FromBody] PlanillaMensualFiltroDto filtro)
    {
        if (string.IsNullOrWhiteSpace(filtro.FechaInicio) || string.IsNullOrWhiteSpace(filtro.FechaFinal))
            return BadRequest("FechaInicio y FechaFinal requeridos");
        if (string.IsNullOrWhiteSpace(filtro.CodEmpresa) || filtro.CodEmpresa == "0")
            return BadRequest("Debe seleccionar una Empresa");
        try
        {
            var data = await _service.ObtenerDetalleAsync(filtro);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApiDetalle error");
            return StatusCode(500, new { error = "Error al consultar el detalle. Verifique los filtros." });
        }
    }
}
