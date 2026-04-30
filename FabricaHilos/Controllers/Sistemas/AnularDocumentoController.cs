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
    private readonly AnularDocumentoJobManager          _jobManager;
    private readonly ILogger<AnularDocumentoController> _logger;

    private static readonly Dictionary<string, string> _series = new(StringComparer.OrdinalIgnoreCase)
    {
        { "01", "F001" },
        { "03", "B001" },
    };

    public AnularDocumentoController(
        IAnularDocumentoService            service,
        AnularDocumentoJobManager          jobManager,
        ILogger<AnularDocumentoController> logger)
    {
        _service    = service;
        _jobManager = jobManager;
        _logger     = logger;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index() =>
        View("~/Views/Sistemas/Requerimientos/AnularDocumento/Index.cshtml");

    [HttpGet("Serie")]
    public IActionResult Serie([FromQuery] string tipoDoc) =>
        _series.TryGetValue(tipoDoc, out var serie)
            ? Json(new { serie })
            : Json(new { serie = "" });

    /// <summary>
    /// Busca el documento y retorna datos en JSON.
    /// GET /Sistemas/Requerimientos/AnularDocumento/Buscar
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
    /// Inicia el job de Restablecer en el servidor y devuelve el jobId.
    /// El proceso continúa aunque el navegador se cierre.
    /// POST /Sistemas/Requerimientos/AnularDocumento/IniciarRestablecer
    /// </summary>
    [HttpPost("IniciarRestablecer")]
    public IActionResult IniciarRestablecer(
        [FromQuery] string tipoDoc,
        [FromQuery] string serie,
        [FromQuery] string numero,
        [FromQuery] string numeroBusqueda,
        [FromQuery] string voucherBusqueda,
        [FromQuery] string ano,
        [FromQuery] string mes,
        [FromQuery] string libro)
    {
        if (string.IsNullOrWhiteSpace(tipoDoc)        || string.IsNullOrWhiteSpace(serie)  ||
            string.IsNullOrWhiteSpace(numero)          || string.IsNullOrWhiteSpace(numeroBusqueda) ||
            string.IsNullOrWhiteSpace(voucherBusqueda))
            return BadRequest(new { error = "Faltan parámetros requeridos." });

        // Capturar conexión y schema AHORA (mientras tenemos HttpContext y sesión activa)
        var connString = GetConnString();
        var schema     = GetSchema();

        var job = _jobManager.IniciarRestablecer(
            connString, schema,
            tipoDoc, serie, numero,
            numeroBusqueda, voucherBusqueda,
            ano ?? "", mes ?? "", libro ?? "");

        return Json(new { jobId = job.JobId });
    }

    /// <summary>
    /// Inicia el job de Revertir en el servidor y devuelve el jobId.
    /// POST /Sistemas/Requerimientos/AnularDocumento/IniciarRevertir
    /// </summary>
    [HttpPost("IniciarRevertir")]
    public IActionResult IniciarRevertir(
        [FromQuery] string tipoDoc,
        [FromQuery] string serie,
        [FromQuery] string numeroAnterior,
        [FromQuery] string ano,
        [FromQuery] string mes,
        [FromQuery] string libro,
        [FromQuery] string voucherAnterior)
    {
        if (string.IsNullOrWhiteSpace(tipoDoc) || string.IsNullOrWhiteSpace(serie))
            return BadRequest(new { error = "Faltan parámetros requeridos." });

        var connString = GetConnString();
        var schema     = GetSchema();

        var job = _jobManager.IniciarRevertir(
            connString, schema,
            tipoDoc, serie,
            numeroAnterior ?? "",
            ano ?? "", mes ?? "", libro ?? "",
            voucherAnterior ?? "");

        return Json(new { jobId = job.JobId });
    }

    /// <summary>
    /// Polling: devuelve el estado actual del job.
    /// GET /Sistemas/Requerimientos/AnularDocumento/EstadoJob?jobId=...
    /// </summary>
    [HttpGet("EstadoJob")]
    public IActionResult EstadoJob([FromQuery] string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return BadRequest(new { error = "jobId requerido." });

        var job = _jobManager.Get(jobId);
        if (job is null)
            return NotFound(new { error = "Job no encontrado." });

        return Json(new
        {
            jobId        = job.JobId,
            tipo         = job.Tipo,
            estado       = job.Estado,
            error        = job.Error,
            creadoEn     = job.CreadoEn,
            finalizadoEn = job.FinalizadoEn,
            pasos        = job.Pasos.Select(p => new
            {
                numero  = p.Numero,
                estado  = p.Estado,
                mensaje = p.Mensaje,
                error   = p.Error,
                filas   = p.Filas
            })
        });
    }

    // ── Helpers: capturar conexión y schema desde la sesión activa ─────────────

    private string GetConnString()
    {
        // Reutiliza la misma lógica de OracleServiceBase pero desde el controller,
        // ya que el servicio es Scoped y el jobManager es Singleton (sin HttpContext)
        var session  = HttpContext.Session;
        var connKey  = session.GetString("EmpresaConexion") ?? "LaColonialConnection";
        var baseConn = HttpContext.RequestServices
                           .GetRequiredService<IConfiguration>()
                           .GetConnectionString(connKey)
                       ?? HttpContext.RequestServices
                           .GetRequiredService<IConfiguration>()
                           .GetConnectionString("LaColonialConnection")!;

        var oraUser = session.GetString("OracleUser");
        var oraPass = session.GetString("OraclePass");

        if (!string.IsNullOrEmpty(oraUser) && !string.IsNullOrEmpty(oraPass))
        {
            var csb = new Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder(baseConn)
            {
                UserID   = oraUser,
                Password = oraPass
            };
            return csb.ToString();
        }

        return baseConn;
    }

    private string GetSchema()
    {
        var connKey = HttpContext.Session.GetString("EmpresaConexion") ?? "LaColonialConnection";
        return connKey switch
        {
            "ArbonaConnection" => "ARBONA.",
            "SolsaConnection"  => "SOLSA.",
            _                  => "SIG."
        };
    }
}

