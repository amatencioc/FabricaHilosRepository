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
    /// Ejecuta los 4 pasos de restablecimiento en streaming (SSE).
    /// GET /Sistemas/Requerimientos/AnularDocumento/RestablecerStream
    /// </summary>
    [HttpGet("RestablecerStream")]
    public async Task RestablecerStream(
        [FromQuery] string tipoDoc,
        [FromQuery] string serie,
        [FromQuery] string numero,
        [FromQuery] string numeroBusqueda,
        [FromQuery] string voucherBusqueda,
        [FromQuery] string ano,
        [FromQuery] string mes,
        [FromQuery] string libro)
    {
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"]      = "no-cache, no-store";
        Response.Headers["X-Accel-Buffering"]  = "no";
        Response.Headers["Connection"]         = "keep-alive";

        async Task Emit(object payload)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payload,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
            await Response.WriteAsync($"data: {json}\n\n");
            await Response.Body.FlushAsync();
        }

        if (string.IsNullOrWhiteSpace(tipoDoc) || string.IsNullOrWhiteSpace(serie) ||
            string.IsNullOrWhiteSpace(numero)   || string.IsNullOrWhiteSpace(numeroBusqueda) ||
            string.IsNullOrWhiteSpace(voucherBusqueda))
        {
            await Emit(new { paso = 0, estado = "aborted", mensaje = "Faltan parámetros requeridos." });
            return;
        }

        try
        {
            // ── Paso 1: DELETE DOCUVENT ─────────────────────────────────────
            await Emit(new { paso = 1, estado = "running", mensaje = "Ejecutando DELETE en DOCUVENT..." });
            var p1 = await _service.Paso1DeleteDocumentAsync(tipoDoc, serie, numero);
            await Emit(new { paso = 1, estado = p1.Ok ? "ok" : "error", mensaje = p1.Ok ? p1.Mensaje : p1.Error, filas = p1.Filas });
            if (!p1.Ok) { await Emit(new { paso = 0, estado = "aborted" }); return; }

            // ── Paso 2: ESPERAR MOVGLOS ESTADO=9, luego DELETE MOVGLOS ──────
            await Emit(new { paso = 2, estado = "running", mensaje = "Esperando que MOVGLOS alcance ESTADO = 9 (disparadores en curso)..." });
            var p2 = await _service.Paso2EsperarYDeleteMovGlosAsync(tipoDoc, serie, numero, timeoutSegundos: 5);
            await Emit(new { paso = 2, estado = p2.Ok ? "ok" : "error", mensaje = p2.Ok ? p2.Mensaje : p2.Error, filas = p2.Filas });
            if (!p2.Ok) { await Emit(new { paso = 0, estado = "aborted" }); return; }

            // ── Paso 3: UPDATE NRODOC ───────────────────────────────────────
            await Emit(new { paso = 3, estado = "running", mensaje = $"Actualizando NRODOC.NUMERO = {numeroBusqueda}..." });
            var p3 = await _service.Paso3UpdateNroDocAsync(tipoDoc, serie, numeroBusqueda);
            await Emit(new { paso = 3, estado = p3.Ok ? "ok" : "error", mensaje = p3.Ok ? p3.Mensaje : p3.Error, filas = p3.Filas });
            if (!p3.Ok) { await Emit(new { paso = 0, estado = "aborted" }); return; }

            // ── Paso 4: UPDATE NROLIBR ──────────────────────────────────────
            await Emit(new { paso = 4, estado = "running", mensaje = $"Actualizando NROLIBR.NUMERO = {voucherBusqueda}..." });
            var p4 = await _service.Paso4UpdateNroLibrAsync(ano, mes, libro, voucherBusqueda);
            await Emit(new { paso = 4, estado = p4.Ok ? "ok" : "error", mensaje = p4.Ok ? p4.Mensaje : p4.Error, filas = p4.Filas });

            await Emit(new { paso = 0, estado = "done" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AnularDocumento/RestablecerStream");
            await Emit(new { paso = 0, estado = "aborted", mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Revierte NRODOC y NROLIBR a los valores anteriores (deshace la restauración).
    /// POST /Sistemas/Requerimientos/AnularDocumento/Revertir
    /// </summary>
    [HttpPost("Revertir")]
    public async Task<IActionResult> Revertir(
        [FromQuery] string tipoDoc,
        [FromQuery] string serie,
        [FromQuery] string numeroAnterior,
        [FromQuery] string ano,
        [FromQuery] string mes,
        [FromQuery] string libro,
        [FromQuery] string voucherAnterior)
    {
        if (string.IsNullOrWhiteSpace(tipoDoc) || string.IsNullOrWhiteSpace(serie))
            return BadRequest(new { ok = false, error = "Faltan parámetros requeridos." });

        try
        {
            var result = await _service.RevertirAsync(tipoDoc, serie, numeroAnterior, ano, mes, libro, voucherAnterior);
            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AnularDocumento/Revertir");
            return StatusCode(500, new { ok = false, error = ex.Message });
        }
    }
}
