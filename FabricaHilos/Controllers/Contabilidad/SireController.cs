using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers.Contabilidad;

[Authorize]
public class SireController : OracleBaseController
{
    private readonly ISireVentasService _ventasService;
    private readonly ISireComprasService _comprasService;
    private readonly ILogger<SireController> _logger;

    public SireController(
        ISireVentasService ventasService,
        ISireComprasService comprasService,
        ILogger<SireController> logger)
    {
        _ventasService = ventasService;
        _comprasService = comprasService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var ventas = await _ventasService.ObtenerPeriodosAsync(cancellationToken);
            var compras = await _comprasService.ObtenerPeriodosAsync(cancellationToken);
            var model = ConstruirDashboard(ventas, compras);
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
            TempData["Error"] = "Debe seleccionar un archivo de reemplazo.";
            return RedirigirPorTipo(tipo, periodo);
        }

        try
        {
            await using var stream = archivo.OpenReadStream();
            var resultado = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
                ? await _ventasService.ReemplazarPropuestaAsync(periodo, stream, archivo.FileName, cancellationToken)
                : await _comprasService.ReemplazarPropuestaAsync(periodo, stream, archivo.FileName, cancellationToken);

            TempData["Success"] = $"Reemplazo procesado. Ticket: {resultado.Ticket}";
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
            var constancia = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
                ? await _ventasService.DescargarConstanciaAsync(periodo, cancellationToken)
                : await _comprasService.DescargarConstanciaAsync(periodo, cancellationToken);

            return File(constancia.Contenido, constancia.ContentType, constancia.NombreArchivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al descargar constancia SIRE {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = ex.Message;
            return RedirigirPorTipo(tipo, periodo);
        }
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
