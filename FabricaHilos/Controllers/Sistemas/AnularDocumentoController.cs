using FabricaHilos.Services.Sistemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Sistemas;

[Authorize]
[Route("Sistemas/Requerimientos/AnularDocumento")]
[Route("AnularDocumento")]
public class AnularDocumentoController : OracleBaseController
{
    private readonly IAnularDocumentoService            _service;
    private readonly ILogger<AnularDocumentoController> _logger;

    // Serie fija por tipo de documento
    private static readonly Dictionary<string, string> _series = new(StringComparer.OrdinalIgnoreCase)
    {
        { "01", "F001" },
        { "03", "B001" },
    };

    public AnularDocumentoController(
        IAnularDocumentoService            service,
        ILogger<AnularDocumentoController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index() =>
        View("~/Views/Sistemas/Requerimientos/AnularDocumento/Index.cshtml");

    /// <summary>
    /// Devuelve la serie correspondiente a un tipo de documento.
    /// GET /Sistemas/Requerimientos/AnularDocumento/Serie?tipoDoc=01
    /// </summary>
    [HttpGet("Serie")]
    public IActionResult Serie([FromQuery] string tipoDoc)
    {
        if (_series.TryGetValue(tipoDoc, out var serie))
            return Json(new { serie });
        return Json(new { serie = "" });
    }

    /// <summary>
    /// Busca el documento y retorna el resultado completo en JSON.
    /// GET /Sistemas/Requerimientos/AnularDocumento/Buscar?tipoDoc=01&serie=F001&numero=00000001
    /// </summary>
    [HttpGet("Buscar")]
    public async Task<IActionResult> Buscar(
        [FromQuery] string tipoDoc,
        [FromQuery] string serie,
        [FromQuery] string numero)
    {
        if (string.IsNullOrWhiteSpace(tipoDoc) ||
            string.IsNullOrWhiteSpace(serie)   ||
            string.IsNullOrWhiteSpace(numero))
            return BadRequest(new { error = "Debe indicar TipoDoc, Serie y Número." });

        try
        {
            var result = await _service.BuscarDocumentoAsync(tipoDoc, serie, numero);
            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AnularDocumento/Buscar");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Restablece NRODOC.NUMERO al valor anterior (el número del documento buscado).
    /// POST /Sistemas/Requerimientos/AnularDocumento/Restablecer
    /// </summary>
    [HttpPost("Restablecer")]
    public async Task<IActionResult> Restablecer(
        [FromQuery] string tipoDoc,
        [FromQuery] string serie,
        [FromQuery] string numeroAnterior)
    {
        if (string.IsNullOrWhiteSpace(tipoDoc) ||
            string.IsNullOrWhiteSpace(serie)   ||
            string.IsNullOrWhiteSpace(numeroAnterior))
            return BadRequest(new { error = "Debe indicar TipoDoc, Serie y NumeroAnterior." });

        try
        {
            var result = await _service.RestablecerFacturaAsync(tipoDoc, serie, numeroAnterior);
            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AnularDocumento/Restablecer");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
