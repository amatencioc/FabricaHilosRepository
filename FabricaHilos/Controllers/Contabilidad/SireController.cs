using FabricaHilos.Sire.Helpers;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using FabricaHilos.Sire.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Controllers.Contabilidad;

[Authorize]
public class SireController : OracleBaseController
{
    private readonly ISireVentasService _ventasService;
    private readonly ISireComprasService _comprasService;
    private readonly ISireAuthService _authService;
    private readonly ITusUploadService _tusUploadService;
    private readonly TicketPollingHelper _ticketPolling;
    private readonly SireOptions _sireOptions;
    private readonly ILogger<SireController> _logger;

    public SireController(
        ISireVentasService ventasService,
        ISireComprasService comprasService,
        ISireAuthService authService,
        ITusUploadService tusUploadService,
        TicketPollingHelper ticketPolling,
        IOptions<SireOptions> sireOptions,
        ILogger<SireController> logger)
    {
        _ventasService    = ventasService;
        _comprasService   = comprasService;
        _authService      = authService;
        _tusUploadService = tusUploadService;
        _ticketPolling    = ticketPolling;
        _sireOptions      = sireOptions.Value;
        _logger           = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var ventas = await _ventasService.ObtenerPeriodosAsync(cancellationToken);
            var compras = await _comprasService.ObtenerPeriodosAsync(cancellationToken);
            var model = ConstruirDashboard(ventas, compras);
            ViewBag.EsMock = _sireOptions.UseMock;
            return View("~/Views/Contabilidad/Sire/Index.cshtml", model);
        }
        catch (SireApiException ex)
        {
            _logger.LogError(ex, "Error SIRE al cargar dashboard");
            TempData["Error"] = ex.Message;
            return View("~/Views/Contabilidad/Sire/Index.cshtml", new List<SirePeriodoDashboardItem>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Ventas(string? periodo, CancellationToken cancellationToken)
    {
        try
        {
            var periodos = await _ventasService.ObtenerPeriodosAsync(cancellationToken);
            var periodoSeleccionado = periodo ?? periodos.FirstOrDefault()?.Periodo ?? string.Empty;
            var registros = string.IsNullOrWhiteSpace(periodoSeleccionado)
                ? Array.Empty<RegistroVenta>()
                : await _ventasService.ObtenerPropuestaAsync(periodoSeleccionado, cancellationToken);

            ViewBag.Periodos = periodos;
            ViewBag.Periodo = periodoSeleccionado;
            ViewBag.EsMock = _sireOptions.UseMock;
            return View("~/Views/Contabilidad/Sire/Ventas.cshtml", registros);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error SIRE RVIE al consultar periodo {Periodo}", periodo);
            TempData["Error"] = ex.Message;
            ViewBag.Periodos = Array.Empty<PropuestaDto>();
            ViewBag.Periodo = periodo ?? string.Empty;
            return View("~/Views/Contabilidad/Sire/Ventas.cshtml", Array.Empty<RegistroVenta>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Compras(string? periodo, CancellationToken cancellationToken)
    {
        try
        {
            var periodos = await _comprasService.ObtenerPeriodosAsync(cancellationToken);
            var periodoSeleccionado = periodo ?? periodos.FirstOrDefault()?.Periodo ?? string.Empty;
            var registros = string.IsNullOrWhiteSpace(periodoSeleccionado)
                ? Array.Empty<RegistroCompra>()
                : await _comprasService.ObtenerPropuestaAsync(periodoSeleccionado, cancellationToken);

            ViewBag.Periodos = periodos;
            ViewBag.Periodo = periodoSeleccionado;
            ViewBag.EsMock = _sireOptions.UseMock;
            return View("~/Views/Contabilidad/Sire/Compras.cshtml", registros);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error SIRE RCE al consultar periodo {Periodo}", periodo);
            TempData["Error"] = ex.Message;
            ViewBag.Periodos = Array.Empty<PropuestaDto>();
            ViewBag.Periodo = periodo ?? string.Empty;
            return View("~/Views/Contabilidad/Sire/Compras.cshtml", Array.Empty<RegistroCompra>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AceptarPropuesta(string periodo, string tipo, CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await EjecutarOperacionTicketAsync(periodo, tipo,
                (p, ct) => tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
                    ? _ventasService.AceptarPropuestaAsync(p, ct)
                    : _comprasService.AceptarPropuestaAsync(p, ct), cancellationToken);

            TempData["Success"] = $"Operación aceptada. Ticket: {resultado.Ticket}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al aceptar propuesta SIRE {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = ex.Message;
        }

        return RedirigirPorTipo(tipo, periodo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReemplazarPropuesta(string periodo, string tipo, IFormFile? archivo, CancellationToken cancellationToken)
    {
        if (archivo is null || archivo.Length == 0)
        {
            TempData["Error"] = "Debe seleccionar un archivo ZIP de reemplazo.";
            return RedirigirPorTipo(tipo, periodo);
        }

        if (!Path.GetExtension(archivo.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "El archivo debe tener extensión .zip (error SUNAT 1348).";
            return RedirigirPorTipo(tipo, periodo);
        }

        try
        {
            await using var stream = archivo.OpenReadStream();

            // Subida vía protocolo TUS a SUNAT
            var tusResult = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
                ? await _tusUploadService.ReemplazarPropuestaRvieAsync(stream, periodo, archivo.FileName, cancellationToken)
                : await _tusUploadService.ReemplazarPropuestaRceAsync(stream, periodo, archivo.FileName, cancellationToken);

            if (!tusResult.Exitoso)
            {
                TempData["Error"] = $"Error en la subida TUS: {tusResult.Mensaje}";
                return RedirigirPorTipo(tipo, periodo);
            }

            _logger.LogInformation("TUS reemplazo {Tipo} {Periodo}: ticket={Ticket} bytes={Bytes}",
                tipo, periodo, tusResult.NumTicket, tusResult.BytesSubidos);

            // Polling del ticket hasta que SUNAT termine de procesar
            if (!string.IsNullOrWhiteSpace(tusResult.NumTicket))
            {
                var ticketFinal = await _ticketPolling.EsperarEstadoFinalAsync(
                    ct => tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
                        ? _ventasService.ConsultarTicketAsync(tusResult.NumTicket, periodo, ct)
                        : _comprasService.ConsultarTicketAsync(tusResult.NumTicket, periodo, ct),
                    cancellationToken);

                if (ticketFinal.Estado.Equals("ERROR", StringComparison.OrdinalIgnoreCase)
                    || ticketFinal.Estado.Equals("RECHAZADO", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = $"SUNAT rechazó el archivo: {ticketFinal.Mensaje}";
                    return RedirigirPorTipo(tipo, periodo);
                }

                TempData["Success"] = $"Reemplazo procesado por SUNAT. Ticket: {tusResult.NumTicket}";
            }
            else
            {
                TempData["Success"] = $"Archivo subido. {tusResult.Mensaje}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reemplazar propuesta SIRE {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = ex.Message;
        }

        return RedirigirPorTipo(tipo, periodo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarPeriodo(string periodo, string tipo, CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await EjecutarOperacionTicketAsync(periodo, tipo,
                (p, ct) => tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
                    ? _ventasService.CerrarPeriodoAsync(p, ct)
                    : _comprasService.CerrarPeriodoAsync(p, ct), cancellationToken);

            TempData["Success"] = $"Cierre procesado. Ticket: {resultado.Ticket}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cerrar periodo SIRE {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = ex.Message;
        }

        return RedirigirPorTipo(tipo, periodo);
    }

    [HttpGet]
    public async Task<IActionResult> DescargarConstancia(string periodo, string tipo, CancellationToken cancellationToken)
    {
        try
        {
            // NOTA: El manual v25 pág 60 requiere nomArchivo para descargar constancia.
            // Este controlador actualmente solo recibe periodo. El nomArchivo debería obtenerse
            // de la respuesta previa del sistema SUNAT (ej: ticket, propuesta).
            // Como solución temporal, se construye el nomArchivo con el patrón esperado.
            // TODO: Modificar el frontend para pasar el nomArchivo real cuando esté disponible.

            var esVentas = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase);
            var codLibro = esVentas ? "140000" : "080000";
            var ruc = _sireOptions.Ruc;

            // Patrón: LE{ruc}{periodo}{codLibro}{estadoGrabacion}{indicadorOperacion}{contenidoLibro}.pdf
            // Ejemplo: LE20100096260202212001404000111112.pdf
            // Para simplificar, usamos valores genéricos para los últimos componentes
            var nomArchivo = $"LE{ruc}{periodo}{codLibro}00111112.pdf";

            var constancia = esVentas
                ? await _ventasService.DescargarConstanciaAsync(nomArchivo, cancellationToken)
                : await _comprasService.DescargarConstanciaAsync(nomArchivo, cancellationToken);

            return File(constancia.Contenido, constancia.ContentType, constancia.NombreArchivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al descargar constancia SIRE {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = ex.Message;
            return RedirigirPorTipo(tipo, periodo);
        }
    }

    [HttpGet]
    [AllowAnonymous] // Temporal: permitir acceso sin auth para pruebas de conectividad SUNAT
    public async Task<IActionResult> Diagnostico(CancellationToken cancellationToken)
    {
        var vm = new SireDiagnosticoViewModel
        {
            Ruc        = _sireOptions.Ruc,
            UsuarioSol = _sireOptions.UsuarioSol,
            AuthUrl    = _sireOptions.AuthUrl,
            ApiBaseUrl = _sireOptions.ApiBaseUrl,
            ClientId   = _sireOptions.ClientId,
            UseMock    = _sireOptions.UseMock
        };

        try
        {
            var token = await _authService.GetTokenAsync(cancellationToken);
            vm.TokenOk       = true;
            vm.TokenTipo     = token.TokenType;
            vm.TokenExpira   = token.ExpiraEnUtc;
            vm.TokenFragment = token.AccessToken.Length > 0
                ? $"{token.AccessToken[..Math.Min(40, token.AccessToken.Length)]}..."
                : "(vacío)";
        }
        catch (Exception ex)
        {
            vm.TokenOk    = false;
            vm.TokenError = ex.Message;
            _logger.LogWarning(ex, "Diagnóstico SIRE: error al obtener token");
        }

        if (vm.TokenOk)
        {
            try
            {
                var periodos = await _ventasService.ObtenerPeriodosAsync(cancellationToken);
                vm.RvieOk      = true;
                vm.RviePeriodos = periodos.Count;
            }
            catch (Exception ex)
            {
                vm.RvieOk    = false;
                vm.RvieError = ex.Message;
                _logger.LogWarning(ex, "Diagnóstico SIRE: error RVIE periodos");
            }

            try
            {
                var periodos = await _comprasService.ObtenerPeriodosAsync(cancellationToken);
                vm.RceOk      = true;
                vm.RcePeriodos = periodos.Count;
            }
            catch (Exception ex)
            {
                vm.RceOk    = false;
                vm.RceError = ex.Message;
                _logger.LogWarning(ex, "Diagnóstico SIRE: error RCE periodos");
            }
        }

        return View("~/Views/Contabilidad/Sire/Diagnostico.cshtml", vm);
    }

    [HttpGet]
    [Route("Sire/DiagnosticoJson")]
    [AllowAnonymous] // Temporal: endpoint JSON para pruebas sin layout
    public async Task<IActionResult> DiagnosticoJson(CancellationToken cancellationToken)
    {
        var resultado = new
        {
            Configuracion = new
            {
                Ruc = _sireOptions.Ruc,
                UsuarioSol = _sireOptions.UsuarioSol,
                AuthUrl = _sireOptions.AuthUrl,
                ApiBaseUrl = _sireOptions.ApiBaseUrl,
                ClientId = _sireOptions.ClientId,
                UseMock = _sireOptions.UseMock
            },
            Token = new { Ok = false, Error = "", Tipo = "", Expira = "", Fragment = "" },
            Rvie = new { Ok = false, Error = "", Periodos = 0 },
            Rce = new { Ok = false, Error = "", Periodos = 0 }
        };

        try
        {
            var token = await _authService.GetTokenAsync(cancellationToken);
            resultado = resultado with
            {
                Token = new
                {
                    Ok = true,
                    Error = "",
                    Tipo = token.TokenType,
                    Expira = token.ExpiraEnUtc.ToString("o"),
                    Fragment = token.AccessToken.Length > 0
                        ? $"{token.AccessToken[..Math.Min(40, token.AccessToken.Length)]}..."
                        : "(vacío)"
                }
            };

            try
            {
                var periodos = await _ventasService.ObtenerPeriodosAsync(cancellationToken);
                resultado = resultado with
                {
                    Rvie = new { Ok = true, Error = "", Periodos = periodos.Count }
                };
            }
            catch (Exception ex)
            {
                resultado = resultado with
                {
                    Rvie = new { Ok = false, Error = ex.Message, Periodos = 0 }
                };
                _logger.LogWarning(ex, "Diagnóstico SIRE: error RVIE periodos");
            }

            try
            {
                var periodos = await _comprasService.ObtenerPeriodosAsync(cancellationToken);
                resultado = resultado with
                {
                    Rce = new { Ok = true, Error = "", Periodos = periodos.Count }
                };
            }
            catch (Exception ex)
            {
                resultado = resultado with
                {
                    Rce = new { Ok = false, Error = ex.Message, Periodos = 0 }
                };
                _logger.LogWarning(ex, "Diagnóstico SIRE: error RCE periodos");
            }
        }
        catch (Exception ex)
        {
            resultado = resultado with
            {
                Token = new
                {
                    Ok = false,
                    Error = ex.Message,
                    Tipo = "",
                    Expira = "",
                    Fragment = ""
                }
            };
            _logger.LogError(ex, "Diagnóstico SIRE: error al obtener token");
        }

        return Json(resultado);
    }

    private static List<SirePeriodoDashboardItem> ConstruirDashboard(
        IReadOnlyList<PropuestaDto> ventas,
        IReadOnlyList<PropuestaDto> compras)
    {
        var map = new Dictionary<string, SirePeriodoDashboardItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in ventas)
        {
            map[v.Periodo] = new SirePeriodoDashboardItem(v.Periodo, v.Descripcion, v.Estado, "-");
        }

        foreach (var c in compras)
        {
            if (map.TryGetValue(c.Periodo, out var actual))
            {
                map[c.Periodo] = actual with { EstadoRce = c.Estado };
            }
            else
            {
                map[c.Periodo] = new SirePeriodoDashboardItem(c.Periodo, c.Descripcion, "-", c.Estado);
            }
        }

        return map.Values.OrderByDescending(x => x.Periodo).ToList();
    }

    private async Task<TicketEstado> EjecutarOperacionTicketAsync(
        string periodo,
        string tipo,
        Func<string, CancellationToken, Task<TicketEstado>> operacion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(periodo))
        {
            throw new ArgumentException("Periodo inválido.", nameof(periodo));
        }

        if (!tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
            && !tipo.Equals("compras", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Tipo inválido.", nameof(tipo));
        }

        return await operacion(periodo, cancellationToken);
    }

    private IActionResult RedirigirPorTipo(string tipo, string periodo)
        => tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(Ventas), new { periodo })
            : RedirectToAction(nameof(Compras), new { periodo });
}

public sealed record SirePeriodoDashboardItem(string Periodo, string Descripcion, string EstadoRvie, string EstadoRce);

public sealed class SireDiagnosticoViewModel
{
    public string Ruc        { get; set; } = string.Empty;
    public string UsuarioSol { get; set; } = string.Empty;
    public string AuthUrl    { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string ClientId   { get; set; } = string.Empty;
    public bool   UseMock    { get; set; }

    public bool      TokenOk       { get; set; }
    public string    TokenTipo     { get; set; } = string.Empty;
    public DateTime  TokenExpira   { get; set; }
    public string    TokenFragment { get; set; } = string.Empty;
    public string    TokenError    { get; set; } = string.Empty;

    public bool   RvieOk      { get; set; }
    public int    RviePeriodos { get; set; }
    public string RvieError   { get; set; } = string.Empty;

    public bool   RceOk      { get; set; }
    public int    RcePeriodos { get; set; }
    public string RceError   { get; set; } = string.Empty;
}
