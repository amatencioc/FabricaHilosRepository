using FabricaHilos.Models.Sire;
using FabricaHilos.Services.Sire;
using FabricaHilos.Sire.Constants;
using FabricaHilos.Sire.Helpers;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using FabricaHilos.Sire.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace FabricaHilos.Controllers.Contabilidad;

/// <summary>
/// Controlador SIRE: Gestión de registros de ventas (RVIE) y compras (RCE) desde SUNAT
/// Proporciona dashboard, consulta de períodos, aceptación/cierre, y gestión de propuestas
/// </summary>
[Authorize]
public class SireController : OracleBaseController
{
    private readonly ISireVentasService _ventasService;
    private readonly ISireComprasService _comprasService;
    private readonly ISireAuthService _authService;
    private readonly ITusUploadService _tusUploadService;
    private readonly TicketPollingHelper _ticketPolling;
    private readonly ILazySireInitializer _lazySireInitializer;
    private readonly SireOptions _sireOptions;
    private readonly ISireExportacionQueue _exportacionQueue;
    private readonly ISireOracleRepository _sireRepo;
    private readonly SireValidaService _validaService;
    private readonly FabricaHilos.Services.Sire.SireReporteComprasService _reporteCompras;
    private readonly IConsultaValidezService _consultaValidez;
    private readonly ILogger<SireController> _logger;
    private readonly IMemoryCache _cache;

    public SireController(
        ISireVentasService ventasService,
        ISireComprasService comprasService,
        ISireAuthService authService,
        ITusUploadService tusUploadService,
        TicketPollingHelper ticketPolling,
        ILazySireInitializer lazySireInitializer,
        IOptions<SireOptions> sireOptions,
        ISireExportacionQueue exportacionQueue,
        ISireOracleRepository sireRepo,
        SireValidaService validaService,
        FabricaHilos.Services.Sire.SireReporteComprasService reporteCompras,
        IConsultaValidezService consultaValidez,
        ILogger<SireController> logger,
        IMemoryCache cache)
    {
        _ventasService         = ventasService;
        _comprasService        = comprasService;
        _authService           = authService;
        _tusUploadService      = tusUploadService;
        _ticketPolling         = ticketPolling;
        _lazySireInitializer   = lazySireInitializer;
        _sireOptions           = sireOptions.Value;
        _exportacionQueue      = exportacionQueue;
        _sireRepo              = sireRepo;
        _validaService         = validaService;
        _reporteCompras        = reporteCompras;
        _consultaValidez       = consultaValidez;
        _logger                = logger;
        _cache                 = cache;
    }

    /// <summary>
    /// Garantiza que los servicios SIRE están inicializados antes de ejecutar cualquier action.
    /// Se dispara al hacer click en Contabilidad → SIRE desde el sidebar.
    /// </summary>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!_lazySireInitializer.IsInitialized)
        {
            _logger.LogInformation("[SIRE] Inicializando servicios SIRE (acción: {Action})...", context.ActionDescriptor.DisplayName);
            await _lazySireInitializer.InitializeAsync();
        }
        await next();
    }

    /// <summary>
    /// Dashboard principal - Resumen ejecutivo de RVIE y RCE
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            if (!_cache.TryGetValue("sire:periodos:ventas:all", out IReadOnlyList<PropuestaDto>? ventas))
            {
                ventas = (await _ventasService.ObtenerPeriodosAsync(cancellationToken))
                    .OrderByDescending(p => p.Periodo).ToList();
                _cache.Set("sire:periodos:ventas:all", ventas, TimeSpan.FromMinutes(5));
            }
            if (!_cache.TryGetValue("sire:periodos:compras:all", out IReadOnlyList<PropuestaDto>? compras))
            {
                compras = (await _comprasService.ObtenerPeriodosAsync(cancellationToken))
                    .OrderByDescending(p => p.Periodo).ToList();
                _cache.Set("sire:periodos:compras:all", compras, TimeSpan.FromMinutes(5));
            }
            var model = ConstruirDashboard(ventas ?? Array.Empty<PropuestaDto>(), compras ?? Array.Empty<PropuestaDto>());
            return View("~/Views/Contabilidad/Sire/Index.cshtml", model);
        }
        catch (SireApiException ex)
        {
            _logger.LogError(ex, "Error SIRE al cargar dashboard");
            TempData["Error"] = $"Error cargando SIRE: {ex.Message}";
            return View("~/Views/Contabilidad/Sire/Index.cshtml", new List<SirePeriodoDashboardItem>());
        }
    }

    /// <summary>
    /// Listado de períodos RVIE (Registro de Ventas e Ingresos)
    /// Muestra tabla con períodos disponibles, estado y acciones
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Ventas(string? periodo, CancellationToken cancellationToken)
    {
        try
        {
            if (!_cache.TryGetValue("sire:periodos:ventas", out IReadOnlyList<PropuestaDto>? todosLosPeriodosVentas))
            {
                todosLosPeriodosVentas = await _ventasService.ObtenerPeriodosAsync(cancellationToken);
                _cache.Set("sire:periodos:ventas", todosLosPeriodosVentas, TimeSpan.FromMinutes(5));
            }
            var periodos = FiltrarAnioActual(todosLosPeriodosVentas!);
            // Si período es null (primer acceso) → usar el último período (más reciente en ASC).
            // Si período es string.Empty (usuario eligió "— seleccionar —") → respetar vacío.
            var periodoSeleccionado = periodo is null
                ? (periodos.LastOrDefault()?.Periodo ?? string.Empty)
                : periodo;

            var todasPropuestas = await _sireRepo.GetPropuestasResumenAsync("ventas", cancellationToken);
            // Filtrar resumen al período seleccionado; si no hay período, lista vacía.
            var propuestasResumen = string.IsNullOrWhiteSpace(periodoSeleccionado)
                ? new List<FabricaHilos.Models.Sire.PropuestaPeriodoResumen>()
                : todasPropuestas.Where(r => r.Periodo.ToString() == periodoSeleccionado).ToList();

            List<FabricaHilos.Models.Sire.SireValidaRegistro>   registros;
            List<FabricaHilos.Models.Sire.SireLegacyRegistro>   registrosLegacy;
            List<FabricaHilos.Models.Sire.SireConcilDetalle>    registrosConcil;
            List<FabricaHilos.Models.Sire.SireExportacionJob>   todosJobs;
            FabricaHilos.Models.Sire.SireConcilResumen?         concilResumen;

            if (string.IsNullOrWhiteSpace(periodoSeleccionado))
            {
                registros       = new List<FabricaHilos.Models.Sire.SireValidaRegistro>();
                registrosLegacy = new List<FabricaHilos.Models.Sire.SireLegacyRegistro>();
                registrosConcil = new List<FabricaHilos.Models.Sire.SireConcilDetalle>();
                todosJobs       = new List<FabricaHilos.Models.Sire.SireExportacionJob>();
                concilResumen   = null;
            }
            else
            {
                var tPropuesta  = _sireRepo.GetRegistrosPropuestaAsync("ventas", periodoSeleccionado, cancellationToken);
                var tLegacy     = _sireRepo.GetLegacyAsync("ventas", periodoSeleccionado, cancellationToken);
                var tConcil     = _sireRepo.GetConcilDetalleAsync("ventas", periodoSeleccionado, cancellationToken);
                var tJobs       = _sireRepo.GetJobsRecientesAsync(100, cancellationToken);
                var tResumen    = _sireRepo.GetConcilResumenAsync("ventas", periodoSeleccionado, cancellationToken);
                await Task.WhenAll(tPropuesta, tLegacy, tConcil, tJobs, tResumen);
                registros       = await tPropuesta;
                registrosLegacy = await tLegacy;
                registrosConcil = await tConcil;
                todosJobs       = await tJobs;
                concilResumen   = await tResumen;
            }
            var jobsPeriodo = todosJobs
                .Where(j => j.TipoRegistro == "ventas" && j.Periodo == periodoSeleccionado)
                .OrderByDescending(j => j.FechaCreacion)
                .ToList();
            var zipJob = jobsPeriodo
                .FirstOrDefault(j => !string.IsNullOrWhiteSpace(j.RutaArchivo)
                                     && System.IO.File.Exists(j.RutaArchivo));
            var constanciaJob = jobsPeriodo
                .FirstOrDefault(j => j.Estado == EstadoJob.Completado
                                     && !string.IsNullOrWhiteSpace(j.NombreArchivo));

            var model = new SireRegistrosViewModel
            {
                Periodos                 = periodos,
                PeriodoSeleccionado      = periodoSeleccionado,
                RegistrosVentas          = Array.Empty<RegistroVenta>(),
                RegistrosPropuesta       = registros,
                RegistrosLegacy          = registrosLegacy,
                RegistrosConcil          = registrosConcil,
                PropuestasResumen        = propuestasResumen,
                EsMock                   = _sireOptions.UseMock,
                Tipo                     = TipoRegistro.Ventas,
                TieneZipLocal            = zipJob is not null,
                NombreZipLocal           = zipJob is not null ? System.IO.Path.GetFileName(zipJob.RutaArchivo) : null,
                NombreArchivoConstancia  = constanciaJob?.NombreArchivo,
                ConcilResumen            = concilResumen,
                MensajeInfo              = (registros.Count > 0 || propuestasResumen.Count > 0) ? null
                    : "Use 'Exportar Propuesta' para descargar los registros desde SUNAT.",
                Ruc = _sireOptions.Ruc
            };

            return View("~/Views/Contabilidad/Sire/Ventas/Index.cshtml", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error SIRE RVIE al consultar periodo {Periodo}", periodo);
            TempData["Error"] = $"Error consultando RVIE: {ex.Message}";

            return View("~/Views/Contabilidad/Sire/Ventas/Index.cshtml", new SireRegistrosViewModel
            {
                Periodos = Array.Empty<PropuestaDto>(), 
                PeriodoSeleccionado = periodo ?? string.Empty,
                RegistrosVentas = Array.Empty<RegistroVenta>(),
                EsMock = _sireOptions.UseMock,
                Tipo = TipoRegistro.Ventas,
                Ruc = _sireOptions.Ruc
            });
        }
    }

    /// <summary>
    /// Listado de períodos RCE (Registro de Compras y Gastos)
    /// Muestra tabla con períodos disponibles, estado y acciones
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Compras(string? periodo, CancellationToken cancellationToken)
    {
        try
        {
            if (!_cache.TryGetValue("sire:periodos:compras", out IReadOnlyList<PropuestaDto>? todosLosPeriodosCompras))
            {
                todosLosPeriodosCompras = await _comprasService.ObtenerPeriodosAsync(cancellationToken);
                _cache.Set("sire:periodos:compras", todosLosPeriodosCompras, TimeSpan.FromMinutes(5));
            }
            var periodos = FiltrarAnioActual(todosLosPeriodosCompras!);
            // Si período es null (primer acceso) → usar el último período (más reciente en ASC).
            // Si período es string.Empty (usuario eligió "— seleccionar —") → respetar vacío.
            var periodoSeleccionado = periodo is null
                ? (periodos.LastOrDefault()?.Periodo ?? string.Empty)
                : periodo;

            var todasPropuestasC = await _sireRepo.GetPropuestasResumenAsync("compras", cancellationToken);
            var propuestasResumen = string.IsNullOrWhiteSpace(periodoSeleccionado)
                ? new List<FabricaHilos.Models.Sire.PropuestaPeriodoResumen>()
                : todasPropuestasC.Where(r => r.Periodo.ToString() == periodoSeleccionado).ToList();

            List<FabricaHilos.Models.Sire.SireValidaRegistro>   registros;
            List<FabricaHilos.Models.Sire.SireLegacyRegistro>   registrosLegacy;
            List<FabricaHilos.Models.Sire.SireConcilDetalle>    registrosConcil;
            List<FabricaHilos.Models.Sire.SireExportacionJob>   todosJobsC;
            FabricaHilos.Models.Sire.SireConcilResumen?         concilResumenC;

            if (string.IsNullOrWhiteSpace(periodoSeleccionado))
            {
                registros       = new List<FabricaHilos.Models.Sire.SireValidaRegistro>();
                registrosLegacy = new List<FabricaHilos.Models.Sire.SireLegacyRegistro>();
                registrosConcil = new List<FabricaHilos.Models.Sire.SireConcilDetalle>();
                todosJobsC      = new List<FabricaHilos.Models.Sire.SireExportacionJob>();
                concilResumenC  = null;
            }
            else
            {
                var tPropuesta  = _sireRepo.GetRegistrosPropuestaAsync("compras", periodoSeleccionado, cancellationToken);
                var tLegacy     = _sireRepo.GetLegacyAsync("compras", periodoSeleccionado, cancellationToken);
                var tConcil     = _sireRepo.GetConcilDetalleAsync("compras", periodoSeleccionado, cancellationToken);
                var tJobs       = _sireRepo.GetJobsRecientesAsync(100, cancellationToken);
                var tResumen    = _sireRepo.GetConcilResumenAsync("compras", periodoSeleccionado, cancellationToken);
                await Task.WhenAll(tPropuesta, tLegacy, tConcil, tJobs, tResumen);
                registros       = await tPropuesta;
                registrosLegacy = await tLegacy;
                registrosConcil = await tConcil;
                todosJobsC      = await tJobs;
                concilResumenC  = await tResumen;
            }
            var jobsPeriodoC = todosJobsC
                .Where(j => j.TipoRegistro == "compras" && j.Periodo == periodoSeleccionado)
                .OrderByDescending(j => j.FechaCreacion)
                .ToList();
            var zipJobC = jobsPeriodoC
                .FirstOrDefault(j => !string.IsNullOrWhiteSpace(j.RutaArchivo)
                                     && System.IO.File.Exists(j.RutaArchivo));
            var constanciaJobC = jobsPeriodoC
                .FirstOrDefault(j => j.Estado == EstadoJob.Completado
                                     && !string.IsNullOrWhiteSpace(j.NombreArchivo));

            var model = new SireRegistrosViewModel
            {
                Periodos                 = periodos,
                PeriodoSeleccionado      = periodoSeleccionado,
                RegistrosCompras         = Array.Empty<RegistroCompra>(),
                RegistrosPropuesta       = registros,
                RegistrosLegacy          = registrosLegacy,
                RegistrosConcil          = registrosConcil,
                PropuestasResumen        = propuestasResumen,
                EsMock                   = _sireOptions.UseMock,
                Tipo                     = TipoRegistro.Compras,
                TieneZipLocal            = zipJobC is not null,
                NombreZipLocal           = zipJobC is not null ? System.IO.Path.GetFileName(zipJobC.RutaArchivo) : null,
                NombreArchivoConstancia  = constanciaJobC?.NombreArchivo,
                ConcilResumen            = concilResumenC,
                MensajeInfo              = (registros.Count > 0 || propuestasResumen.Count > 0) ? null
                    : "Use 'Exportar Propuesta' para descargar los registros desde SUNAT.",
                Ruc = _sireOptions.Ruc
            };

            return View("~/Views/Contabilidad/Sire/Compras/Index.cshtml", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error SIRE RCE al consultar periodo {Periodo}", periodo);
            TempData["Error"] = $"Error consultando RCE: {ex.Message}";

            return View("~/Views/Contabilidad/Sire/Compras/Index.cshtml", new SireRegistrosViewModel
            {
                Periodos = Array.Empty<PropuestaDto>(), 
                PeriodoSeleccionado = periodo ?? string.Empty,
                RegistrosCompras = Array.Empty<RegistroCompra>(),
                EsMock = _sireOptions.UseMock,
                Tipo = TipoRegistro.Compras,
                Ruc = _sireOptions.Ruc
            });
        }
    }

    // ── Eliminar propuesta descargada ──────────────────────────────────────────

    /// <summary>Elimina todos los registros SIRE_PROPUESTA del período indicado.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarPropuesta(string tipo, int periodo, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo.ToString(), tipo);
            var borrados = await _sireRepo.EliminarPropuestaAsync(tipo, periodo, cancellationToken);
            _cache.Remove($"sire:periodos:{tipo}:all");
            _cache.Remove($"sire:periodos:{tipo}");
            TempData["Success"] = $"Propuesta {periodo} eliminada ({borrados} registros borrados).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando propuesta {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = $"Error al eliminar: {ex.Message}";
        }
        return RedirigirPorTipo(tipo, periodo.ToString());
    }

    // ── Enviar reporte Solo SUNAT (Compras) ──────────────────────────────────

    /// <summary>
    /// Genera el Excel con los documentos "Solo SUNAT" del período y lo envía
    /// al correo configurado en SireReporteCompras (appsettings.json).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarReporteCompras(string periodo, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo, "compras");
            var registros     = await _sireRepo.GetConcilDetalleAsync("compras", periodo, cancellationToken);
            var usuarioActual = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "Sistema";
            var (ok, mensaje) = await _reporteCompras.EnviarReporteAsync(periodo, registros, usuarioActual, cancellationToken);
            return Json(new { ok, mensaje });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando reporte compras {Periodo}", periodo);
            return Json(new { ok = false, mensaje = $"Error al enviar reporte: {ex.Message}" });
        }
    }

    // ── Validación de comprobantes (API Consulta Integrada SUNAT) ─────────────

    /// <summary>
    /// Valida todos los comprobantes del período contra la API Consulta Integrada de SUNAT.
    // ── Obtener lista de IDs a validar (para progreso fila a fila desde el cliente) ──────────
    /// <summary>
    /// Devuelve la lista de {idConcil, serie, numero, ruc, nombre} de todos los registros
    /// del período/tipo que pueden validarse contra SUNAT. El cliente los procesa uno a uno
    /// llamando a ValidarFilaSunat para mostrar progreso en tiempo real.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetIdsParaValidar(string tipo, string periodo, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo, tipo);
            var todos = await _sireRepo.GetConcilTodosParaValidarAsync(tipo, periodo, cancellationToken);
            return Json(new
            {
                total = todos.Count,
                items = todos.Select(d => new
                {
                    idConcil = d.IdConcil,
                    serie    = d.Serie,
                    numero   = d.Numero,
                    ruc      = d.Ruc,
                    nombre   = d.Nombre != null && d.Nombre.Length > 30
                                   ? d.Nombre[..30] + "…"
                                   : d.Nombre,
                }).ToArray()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetIdsParaValidar] tipo={Tipo} periodo={Periodo}", tipo, periodo);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// Procesa en paralelo (hasta 4 concurrentes) con delay entre requests para evitar throttling.
    /// Retorna JSON: { total, validados, errores, resultados: [{idConcil, ruc, serie, numero, estadoCp, estadoRuc, condDomiRuc}] }
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidarCpeBatch(
        [FromBody] ValidarCpeBatchRequest req, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(req.Periodo, req.Tipo);

            var pendientes = await _sireRepo.GetConcilPendientesValidezAsync(
                req.Tipo, req.Periodo, cancellationToken);

            if (pendientes.Count == 0)
            {
                _logger.LogInformation("[ValidarCpe] {Tipo} {Periodo}: sin comprobantes pendientes de validar.", req.Tipo, req.Periodo);
                return Json(new { total = 0, validados = 0, errores = 0, resultados = Array.Empty<object>() });
            }

            var resultados = new List<object>();
            var validados  = 0;
            var errores    = 0;

            // Procesar secuencialmente con delay para respetar rate limit de SUNAT
            foreach (var doc in pendientes)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (doc.FEmision is null || doc.Ruc is null || doc.Tipdoc is null) { errores++; continue; }

                // SIRE_PROPUESTA almacena TOTAL_CP siempre en soles (PEN), incluso para facturas
                // en moneda extranjera. La API SUNAT validarcomprobante exige el monto en la
                // moneda original del comprobante → para ME dividir entre CAMBIO.
                var montoApi = (doc.SunatMoneda is not null &&
                                !doc.SunatMoneda.Equals("PEN", StringComparison.OrdinalIgnoreCase) &&
                                doc.CambioMoneda > 0)
                    ? Math.Round(doc.SunatTotal / doc.CambioMoneda, 2)
                    : doc.SunatTotal;

                var result = await _consultaValidez.ValidarAsync(
                    doc.Ruc, doc.Tipdoc, doc.Serie ?? "", doc.Numero ?? "",
                    doc.FEmision.Value, montoApi, cancellationToken);

                if (result is not null)
                {
                    await _sireRepo.GuardarValidezAsync(
                        doc.IdConcil, result.EstadoCp, result.EstadoRuc, result.CondDomiRuc,
                        cancellationToken);

                    resultados.Add(new {
                        idConcil    = doc.IdConcil,
                        ruc         = doc.Ruc,
                        serie       = doc.Serie,
                        numero      = doc.Numero,
                        estadoCp    = result.EstadoCp,
                        estadoRuc   = result.EstadoRuc,
                        condDomiRuc = result.CondDomiRuc,
                        badgeCss    = result.EstadoCp switch
                        {
                            "1" => "badge bg-success",
                            "2" => "badge bg-danger",
                            "0" => "badge bg-warning text-dark",
                            "3" => "badge bg-info text-dark",
                            "4" => "badge bg-danger",
                            _   => "badge bg-secondary"
                        },
                        estadoCpLabel = result.EstadoCp switch
                        {
                            "1" => "ACEPTADO",
                            "2" => "ANULADO",
                            "0" => "NO EXISTE",
                            "3" => "AUTORIZADO",
                            "4" => "NO AUTOR.",
                            _   => $"CP:{result.EstadoCp}"
                        },
                        estadoRucLabel = result.EstadoRuc switch
                        {
                            "00" => "ACTIVO",
                            "01" => "BAJA PROV.",
                            "02" => "BAJA DEFI.",
                            "03" => "BAJA MULT.",
                            "10" => "BAJA OFIC.",
                            "15" => "SUSPENSION",
                            "16" => "BAJA VOLUN.",
                            _    => result.EstadoRuc ?? "-"
                        },
                        condDomiLabel = result.CondDomiRuc switch
                        {
                            "00" => "HABIDO",
                            "09" => "PENDIENTE",
                            "11" => "NO HALLADO",
                            "12" => "NO HALLADO",
                            "20" => "NO HABIDO",
                            "21" => "NO HABIDO",
                            _    => result.CondDomiRuc ?? "-"
                        },
                    });
                    validados++;
                }
                else { errores++; }

                await Task.Delay(200, cancellationToken); // 200 ms entre requests
            }

            _logger.LogInformation("[ValidarCpe] {Tipo} {Periodo}: {V} validados, {E} errores de {T} pendientes.",
                req.Tipo, req.Periodo, validados, errores, pendientes.Count);

            return Json(new { total = pendientes.Count, validados, errores, resultados = resultados.ToArray() });
        }
        catch (OperationCanceledException) { return Json(new { cancelado = true }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ValidarCpe] Error batch {Tipo} {Periodo}", req.Tipo, req.Periodo);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Valida un único comprobante (fila de SIRE_CONCIL) contra la API de SUNAT.
    /// Llamado desde el botón de re-validación por fila en la tabla de conciliación.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidarFilaSunat(long idConcil, CancellationToken cancellationToken)
    {
        try
        {
            var doc = await _sireRepo.GetConcilFilaParaValidezAsync(idConcil, cancellationToken);
            if (doc is null)
                return NotFound(new { error = "Fila no encontrada." });

            if (doc.FEmision is null || doc.Ruc is null || doc.Tipdoc is null)
                return BadRequest(new { error = "Datos incompletos para validar (fecha, RUC o tipo de comprobante nulos)." });

            // SIRE_PROPUESTA almacena TOTAL_CP siempre en soles (PEN), incluso para facturas
            // en moneda extranjera. La API SUNAT validarcomprobante exige el monto en la
            // moneda original del comprobante → para ME dividir entre CAMBIO.
            var montoApi = (doc.SunatMoneda is not null &&
                            !doc.SunatMoneda.Equals("PEN", StringComparison.OrdinalIgnoreCase) &&
                            doc.CambioMoneda > 0)
                ? Math.Round(doc.SunatTotal / doc.CambioMoneda, 2)
                : doc.SunatTotal;

            var result = await _consultaValidez.ValidarAsync(
                doc.Ruc, doc.Tipdoc, doc.Serie ?? "", doc.Numero ?? "",
                doc.FEmision.Value, montoApi, cancellationToken);

            if (result is null)
            {
                _logger.LogWarning("[ValidarFilaSunat] Sin respuesta de SUNAT para idConcil={Id}", idConcil);
                return Json(new { ok = false, error = "Sin respuesta de SUNAT." });
            }

            await _sireRepo.GuardarValidezAsync(
                doc.IdConcil, result.EstadoCp, result.EstadoRuc, result.CondDomiRuc,
                cancellationToken);

            _logger.LogInformation(
                "[ValidarFilaSunat] idConcil={Id} {Serie}/{Num} → CP={Cp} RUC={Ruc} DOM={Dom}",
                idConcil, doc.Serie, doc.Numero, result.EstadoCp, result.EstadoRuc, result.CondDomiRuc);

            return Json(new
            {
                ok          = true,
                idConcil    = doc.IdConcil,
                estadoCp    = result.EstadoCp,
                estadoRuc   = result.EstadoRuc,
                condDomiRuc = result.CondDomiRuc,
                badgeCss    = result.EstadoCp switch
                {
                    "1" => "badge bg-success",
                    "2" => "badge bg-danger",
                    "0" => "badge bg-warning text-dark",
                    "3" => "badge bg-info text-dark",
                    "4" => "badge bg-danger",
                    _   => "badge bg-secondary"
                },
                estadoCpLabel = result.EstadoCp switch
                {
                    "1" => "ACEPTADO",
                    "2" => "ANULADO",
                    "0" => "NO EXISTE",
                    "3" => "AUTORIZADO",
                    "4" => "NO AUTOR.",
                    _   => $"CP:{result.EstadoCp}"
                },
                estadoRucLabel = result.EstadoRuc switch
                {
                    "00" => "ACTIVO",
                    "01" => "BAJA PROV.",
                    "02" => "BAJA DEFI.",
                    "03" => "BAJA MULT.",
                    "10" => "BAJA OFIC.",
                    "15" => "SUSPENSION",
                    "16" => "BAJA VOLUN.",
                    _    => result.EstadoRuc ?? "-"
                },
                condDomiLabel = result.CondDomiRuc switch
                {
                    "00" => "HABIDO",
                    "09" => "PENDIENTE",
                    "11" => "NO HALLADO",
                    "12" => "NO HALLADO",
                    "20" => "NO HABIDO",
                    "21" => "NO HABIDO",
                    _    => result.CondDomiRuc ?? "-"
                },
            });
        }
        catch (OperationCanceledException) { return Json(new { cancelado = true }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ValidarFilaSunat] Error idConcil={Id}", idConcil);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── Conciliar propuesta contra ERP ───────────────────────────────────────

    /// <summary>Ejecuta SP_SIRE_CARGA_LEGACY + SP_SIRE_CONCILIAR para el período.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConciliarPropuesta(string tipo, int periodo, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo.ToString(), tipo);
            var resultado = await _sireRepo.ConciliarPropuestaAsync(tipo, periodo, cancellationToken);
            _cache.Remove($"sire:periodos:{tipo}:all");
            _cache.Remove($"sire:periodos:{tipo}");
            TempData["Success"] = $"Conciliación {periodo} completada — {resultado}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error conciliando {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = $"Error al conciliar: {ex.Message}";
        }
        return RedirigirPorTipo(tipo, periodo.ToString());
    }

    /// <summary>
    /// Inicia la exportación de propuesta RVIE o RCE
    /// Retorna JSON con numTicket para monitorear progreso
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportarPropuesta([FromBody] ExportarPropuestaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Periodo) || string.IsNullOrWhiteSpace(request.Tipo))
                return Json(new { exitoso = false, error = "Parámetros inválidos" });

            ValidarParametrosOperacion(request.Periodo, request.Tipo);

            var resultado = await EjecutarOperacionTicketAsync(request.Periodo, request.Tipo,
                (p, ct) => request.Tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
                    ? _ventasService.ExportarPropuestaAsync(p, ct)
                    : _comprasService.ExportarPropuestaAsync(p, ct), cancellationToken);

            _logger.LogInformation("Exportación iniciada: tipo={Tipo} periodo={Periodo} ticket={Ticket}", 
                request.Tipo, request.Periodo, resultado.Ticket);

            return Json(new
            {
                exitoso = true,
                numTicket = resultado.Ticket,
                estado = resultado.Estado,
                mensaje = "Exportación iniciada. Use 'Consultar Estado' para monitorear."
            });
        }
        catch (FabricaHilos.Sire.Services.SireApiException sireEx) when (sireEx.StatusCode == System.Net.HttpStatusCode.InternalServerError)
        {
            _logger.LogWarning(sireEx, "SUNAT 500 al exportar propuesta {Tipo} {Periodo} — puede ser período sin propuesta generada o permisos insuficientes", request?.Tipo, request?.Periodo);
            return Json(new
            {
                exitoso = false,
                error = "SUNAT devolvió error 500. Posibles causas: (1) SUNAT aún no generó propuesta para este período — el RUC puede ser nuevo en SIRE; (2) el período ya fue procesado y no requiere exportación; (3) la aplicación no tiene habilitada la operación en el portal SUNAT API. Verifique en el portal SIRE web si el período tiene propuesta disponible.",
                mensaje = "Error 500 de SUNAT — propuesta no disponible para exportar"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar propuesta SIRE {Tipo} {Periodo}", request?.Tipo, request?.Periodo);
            return Json(new
            {
                exitoso = false,
                error = ex.Message,
                mensaje = $"Error al iniciar exportación: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Consulta el estado de un ticket de exportación/procesamiento
    /// Retorna JSON con estado actual y mensaje
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ConsultarEstadoExportacion(string numTicket, string periodo, string tipo, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo, tipo);

            if (string.IsNullOrWhiteSpace(numTicket))
                throw new ArgumentException("Número de ticket no puede estar vacío.", nameof(numTicket));

            var resultado = await (tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
                ? _ventasService.ConsultarTicketAsync(numTicket, periodo, cancellationToken)
                : _comprasService.ConsultarTicketAsync(numTicket, periodo, cancellationToken));

            _logger.LogInformation("Consulta estado: tipo={Tipo} periodo={Periodo} ticket={Ticket} estado={Estado} archivo={Archivo}", 
                tipo, periodo, numTicket, resultado.Estado, resultado.ArchivoReporte?.NomArchivoReporte);

            return Json(new
            {
                exitoso = true,
                numTicket = resultado.NumTicket,
                estado = resultado.Estado,
                codEstadoProceso = resultado.CodEstadoProceso,
                codProceso = resultado.CodProceso,
                perTributario = resultado.PerTributario,
                mensaje = resultado.Mensaje,
                nomArchivoReporte = resultado.ArchivoReporte?.NomArchivoReporte,
                codTipoArchivoReporte = resultado.ArchivoReporte?.CodTipoArchivoReporte,
                esFinal = resultado.EsFinal
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar estado SIRE {Tipo} {Periodo} {Ticket}", tipo, periodo, numTicket);
            return Json(new
            {
                exitoso = false,
                error = ex.Message,
                mensaje = $"Error al consultar estado: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Descarga el archivo ZIP de la propuesta exportada (servicio 5.17).
    /// Los parámetros nomArchivo, codTipoArchivo, codProceso y numTicket deben obtenerse
    /// del resultado de ConsultarEstadoExportacion cuando el ticket llega a COMPLETADO.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DescargarArchivoExportado(
        string periodo,
        string tipo,
        string nomArchivo,
        string? codTipoArchivo,
        string? codProceso,
        string? numTicket,
        CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo, tipo);

            if (string.IsNullOrWhiteSpace(nomArchivo))
                return Json(new { exitoso = false, error = "El nombre del archivo no está disponible. Espere a que el ticket finalice (COMPLETADO) antes de descargar." });

            var esVentas = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase);
            var codLibro = esVentas ? "140000" : "080000";

            var url = SireEndpoints.DescargarArchivo(
                nomArchivo,
                codTipoArchivo,
                codLibro,
                periodo,
                codProceso ?? string.Empty,
                numTicket ?? string.Empty);

            var constancia = esVentas
                ? await _ventasService.DescargarArchivoReporteAsync(url, nomArchivo, cancellationToken)
                : await _comprasService.DescargarArchivoReporteAsync(url, nomArchivo, cancellationToken);

            _logger.LogInformation("Archivo exportado descargado: tipo={Tipo} periodo={Periodo} archivo={Archivo} bytes={Bytes}",
                tipo, periodo, constancia.NombreArchivo, constancia.Contenido.Length);

            return File(constancia.Contenido, constancia.ContentType, constancia.NombreArchivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al descargar archivo exportado SIRE {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = ex.Message;
            return RedirigirPorTipo(tipo, periodo);
        }
    }

    /// <summary>
    /// Acepta una propuesta RVIE o RCE
    /// </summary>
    /// <param name="periodo">Período YYYYMM</param>
    /// <param name="tipo">Tipo de registro: 'ventas' (RVIE) o 'compras' (RCE)</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AceptarPropuesta(string periodo, string tipo, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo, tipo);

            var resultado = await EjecutarOperacionTicketAsync(periodo, tipo,
                (p, ct) => tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
                    ? _ventasService.AceptarPropuestaAsync(p, ct)
                    : _comprasService.AceptarPropuestaAsync(p, ct), cancellationToken);

            TempData["Success"] = $"Propuesta aceptada. Ticket: {resultado.Ticket}";
            _logger.LogInformation("Propuesta aceptada: tipo={Tipo} periodo={Periodo} ticket={Ticket}", 
                tipo, periodo, resultado.Ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al aceptar propuesta SIRE {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = ex.Message;
        }

        return RedirigirPorTipo(tipo, periodo);
    }

    /// <summary>
    /// Reemplaza una propuesta RVIE o RCE con un nuevo archivo ZIP
    /// </summary>
    /// <param name="periodo">Período YYYYMM</param>
    /// <param name="tipo">Tipo: 'ventas' (RVIE) o 'compras' (RCE)</param>
    /// <param name="archivo">Archivo ZIP reemplazo validado por SUNAT</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReemplazarPropuesta(string periodo, string tipo, IFormFile? archivo, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo, tipo);

            if (archivo is null || archivo.Length == 0)
            {
                TempData["Error"] = "Debe seleccionar un archivo ZIP de reemplazo.";
                return RedirigirPorTipo(tipo, periodo);
            }

            if (!Path.GetExtension(archivo.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "El archivo debe tener extensión .zip";
                return RedirigirPorTipo(tipo, periodo);
            }

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

            _logger.LogInformation("TUS reemplazo iniciado: tipo={Tipo} periodo={Periodo} ticket={Ticket} bytes={Bytes}",
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

    /// <summary>
    /// Cierra un período RVIE o RCE en SUNAT
    /// </summary>
    /// <param name="periodo">Período YYYYMM</param>
    /// <param name="tipo">Tipo: 'ventas' (RVIE) o 'compras' (RCE)</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarPeriodo(string periodo, string tipo, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo, tipo);

            var resultado = await EjecutarOperacionTicketAsync(periodo, tipo,
                (p, ct) => tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
                    ? _ventasService.CerrarPeriodoAsync(p, ct)
                    : _comprasService.CerrarPeriodoAsync(p, ct), cancellationToken);

            TempData["Success"] = $"Período cerrado. Ticket: {resultado.Ticket}";
            _logger.LogInformation("Período cerrado: tipo={Tipo} periodo={Periodo} ticket={Ticket}", 
                tipo, periodo, resultado.Ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cerrar periodo SIRE {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = ex.Message;
        }

        return RedirigirPorTipo(tipo, periodo);
    }

    /// <summary>
    /// Descarga la constancia de cierre de un período RVIE o RCE.
    /// El parámetro nomArchivo es OBLIGATORIO y debe obtenerse del campo archivoReporte
    /// que devuelve ConsultarEstadoExportacion cuando el ticket llega a estado COMPLETADO.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DescargarConstancia(string periodo, string tipo, string? nomArchivo, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo, tipo);

            // Si nomArchivo no viene en la URL, buscarlo en el job completado más reciente
            if (string.IsNullOrWhiteSpace(nomArchivo))
            {
                var jobs = await _sireRepo.GetJobsRecientesAsync(50, cancellationToken);
                nomArchivo = jobs
                    .Where(j => j.TipoRegistro.Equals(tipo, StringComparison.OrdinalIgnoreCase)
                             && j.Periodo == periodo
                             && j.Estado == EstadoJob.Completado
                             && !string.IsNullOrWhiteSpace(j.NombreArchivo))
                    .OrderByDescending(j => j.FechaCreacion)
                    .Select(j => j.NombreArchivo)
                    .FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(nomArchivo))
            {
                TempData["Error"] = "No se puede descargar la constancia: el nombre del archivo no está disponible. " +
                    "Primero use 'Exportar Propuesta' y espere a que el ticket finalice (estado COMPLETADO). " +
                    "El nombre del archivo se obtiene automáticamente al consultar el estado del ticket.";
                return RedirigirPorTipo(tipo, periodo);
            }

            var esVentas = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase);

            var constancia = esVentas
                ? await _ventasService.DescargarConstanciaAsync(nomArchivo, cancellationToken)
                : await _comprasService.DescargarConstanciaAsync(nomArchivo, cancellationToken);

            _logger.LogInformation("Constancia descargada: tipo={Tipo} periodo={Periodo} archivo={Archivo}", 
                tipo, periodo, constancia.NombreArchivo);

            return File(constancia.Contenido, constancia.ContentType, constancia.NombreArchivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al descargar constancia SIRE {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = ex.Message;
            return RedirigirPorTipo(tipo, periodo);
        }
    }

    /// <summary>
    /// Descarga una propuesta (registros) de un período RVIE o RCE como CSV
    /// ⚠️ NOTA: El endpoint de obtención directa de registros es deprecated y retorna HTTP 500.
    /// Esta acción renderiza un error con instrucciones para usar el flujo de exportación correcto.
    /// </summary>
    [HttpGet]
    public IActionResult DescargarPropuesta(string periodo, string tipo, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo, tipo);

            // ❌ Endpoint deprecated: "/libros/rvie/propuesta/web/registroslibros/{periodo}/cabecera"
            // El flujo correcto requiere:
            // 1. Exportar propuesta (ExportarPropuestaAsync) → ticket
            // 2. Esperar ticket (TicketPollingHelper)
            // 3. Descargar archivo ZIP (DescargarConstanciaAsync)
            // 4. Procesar archivo plano

            TempData["Warning"] = "El endpoint de descarga de propuesta ha sido actualizado. " +
                "Para descargar registros, use la opción 'Descargar Archivo Plano' que extrae datos del archivo ZIP exportado desde SUNAT.";

            return RedirigirPorTipo(tipo, periodo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al descargar propuesta SIRE {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = ex.Message;
            return RedirigirPorTipo(tipo, periodo);
        }
    }

    /// <summary>
    /// Descarga los registros de un período en formato de archivo plano (pipe-delimited) estándar de SUNAT
    /// ⚠️ NOTA: El endpoint de obtención directa de registros es deprecated y retorna HTTP 500.
    /// Esta acción renderiza un error con instrucciones para usar el flujo de exportación correcto.
    /// </summary>
    /// <param name="periodo">Período YYYYMM</param>
    /// <param name="tipo">Tipo de registro: 'ventas' (RVIE) o 'compras' (RCE)</param>
    [HttpGet]
    public IActionResult DescargarArchivoPlano(string periodo, string tipo, CancellationToken cancellationToken)
    {
        try
        {
            ValidarParametrosOperacion(periodo, tipo);

            // ❌ Endpoint deprecated: "/libros/rvie/propuesta/web/registroslibros/{periodo}/cabecera"
            // El flujo correcto requiere:
            // 1. Exportar propuesta (ExportarPropuestaAsync) → ticket
            // 2. Esperar ticket (TicketPollingHelper)
            // 3. Descargar archivo ZIP (DescargarConstanciaAsync)
            // 4. Procesar archivo plano

            TempData["Warning"] = "El endpoint de descarga directa de registros ha sido actualizado. " +
                "Use el botón 'Descargar Propuesta' para iniciar un flujo de exportación que descargará el archivo ZIP desde SUNAT.";

            return RedirigirPorTipo(tipo, periodo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al descargar archivo plano SIRE {Tipo} {Periodo}", tipo, periodo);
            TempData["Error"] = ex.Message;
            return RedirigirPorTipo(tipo, periodo);
        }
    }

    /// <summary>
    /// Panel de diagnóstico técnico para verificar conectividad SUNAT
    /// Muestra estado del token, RVIE, RCE y configuración actual
    /// </summary>
    [HttpGet]
    [AllowAnonymous] // Para pruebas de conectividad sin autenticación
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

    /// <summary>
    /// Endpoint JSON para diagnóstico SIRE (sin layout HTML)
    /// Útil para automatización y monitoreo
    /// </summary>
    [HttpGet]
    [Route("Sire/DiagnosticoJson")]
    [AllowAnonymous]
    public async Task<IActionResult> DiagnosticoJson(CancellationToken cancellationToken)
    {
        var resultado = new
        {
            Timestamp = DateTime.Now,
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
                _logger.LogWarning(ex, "Diagnóstico JSON: error RVIE");
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
                _logger.LogWarning(ex, "Diagnóstico JSON: error RCE");
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
            _logger.LogError(ex, "Diagnóstico JSON: error token");
        }

        return Json(resultado);
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Filtra períodos al año en curso. Formato YYYYMM → compara los 4 primeros caracteres.</summary>
    private static IReadOnlyList<PropuestaDto> FiltrarAnioActual(IReadOnlyList<PropuestaDto> periodos)
    {
        var anio = DateTime.Now.Year.ToString();
        return periodos
            .Where(p => p.Periodo.Length >= 4 && p.Periodo.StartsWith(anio))
            .OrderBy(p => p.Periodo)
            .ToList();
    }

    /// <summary>
    /// Mapea el código de estado SUNAT (campo codEstado del endpoint Consultar Períodos)
    /// a un valor interno estándar para badges y colores.
    ///   "01" = Propuesta disponible  (ya generada, pendiente de aceptar/reemplazar)
    ///   "02" = En proceso / vigente
    ///   "03" = Sin información / No presentado
    ///   "04" = Presentado / Cerrado
    /// El mock ya envía los valores internos directamente (PROPUESTA_DISPONIBLE, etc.).
    /// </summary>
    private static string MapearEstadoSunat(string codEstado) => codEstado switch
    {
        // — Valores mock (pasan directo) —
        "PROPUESTA_DISPONIBLE" => "PROPUESTA_DISPONIBLE",
        "EN_PROCESO"           => "EN_PROCESO",
        "CERRADO"              => "CERRADO",
        "SIN_INFORMACION"      => "SIN_INFORMACION",
        // — Códigos reales SUNAT SIRE —
        "01" => "PROPUESTA_DISPONIBLE",
        "02" => "EN_PROCESO",
        "03" => "SIN_INFORMACION",
        "04" => "CERRADO",
        // — Vacío / desconocido —
        ""   => "SIN_INFORMACION",
        _    => codEstado   // fallback: mostrar tal cual
    };

    /// <summary>Construye el dashboard con información de RVIE y RCE</summary>
    private static List<SirePeriodoDashboardItem> ConstruirDashboard(
        IReadOnlyList<PropuestaDto> ventas,
        IReadOnlyList<PropuestaDto> compras)
    {
        var map = new Dictionary<string, SirePeriodoDashboardItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in ventas)
        {
            map[v.Periodo] = new SirePeriodoDashboardItem(
                Periodo: v.Periodo,
                Descripcion: v.Descripcion,
                EstadoRvie: MapearEstadoSunat(v.Estado),
                EstadoRce: "-",
                DescRvie: v.Descripcion,
                DescRce: "-");
        }

        foreach (var c in compras)
        {
            if (map.TryGetValue(c.Periodo, out var actual))
            {
                map[c.Periodo] = actual with
                {
                    EstadoRce = MapearEstadoSunat(c.Estado),
                    DescRce   = c.Descripcion
                };
            }
            else
            {
                map[c.Periodo] = new SirePeriodoDashboardItem(
                    Periodo: c.Periodo,
                    Descripcion: c.Descripcion,
                    EstadoRvie: "-",
                    EstadoRce: MapearEstadoSunat(c.Estado),
                    DescRvie: "-",
                    DescRce: c.Descripcion);
            }
        }

        return map.Values.OrderByDescending(x => x.Periodo).ToList();
    }



    /// <summary>Valida que período y tipo sean válidos</summary>
    private static void ValidarParametrosOperacion(string periodo, string tipo)
    {
        if (string.IsNullOrWhiteSpace(periodo))
            throw new ArgumentException("Período no puede estar vacío.", nameof(periodo));

        if (!tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
            && !tipo.Equals("compras", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Tipo debe ser 'ventas' o 'compras'.", nameof(tipo));
    }

    /// <summary>Ejecuta una operación que retorna TicketEstado</summary>
    private async Task<TicketEstado> EjecutarOperacionTicketAsync(
        string periodo,
        string tipo,
        Func<string, CancellationToken, Task<TicketEstado>> operacion,
        CancellationToken cancellationToken)
    {
        ValidarParametrosOperacion(periodo, tipo);
        return await operacion(periodo, cancellationToken);
    }

    /// <summary>Redirige a Ventas o Compras según el tipo</summary>
    private IActionResult RedirigirPorTipo(string tipo, string periodo)
        => tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(Ventas), new { periodo })
            : RedirectToAction(nameof(Compras), new { periodo });

    /// <summary>Genera CSV a partir de una lista de objetos dinámicos</summary>
    private string GenerarCSV(List<object> registros, bool esVentas)
    {
        if (!registros.Any()) return string.Empty;

        var sb = new System.Text.StringBuilder();
        var firstRecord = registros.First();
        var propiedades = firstRecord.GetType().GetProperties();

        // Header
        sb.AppendLine(string.Join(",", propiedades.Select(p => $"\"{p.Name}\"")));

        // Rows
        foreach (var registro in registros)
        {
            var valores = propiedades.Select(p => 
            {
                var valor = p.GetValue(registro);
                var str = valor?.ToString() ?? string.Empty;
                // Escapar comillas y envolver si contiene comas
                if (str.Contains(",") || str.Contains("\"") || str.Contains("\n"))
                {
                    str = $"\"{str.Replace("\"", "\"\"")}\"";
                }
                return str;
            });
            sb.AppendLine(string.Join(",", valores));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Vista de monitoreo SIRE: Log de auditoría HTTP.
    /// Acepta filtros opcionales para pre-seleccionar tab y filtrar el log.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Monitoreo(
        string? jobId       = null,
        string? operacion   = null,
        string  tab         = "log",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var apiLogs = await _sireRepo.GetApiLogsAsync(500, jobId, operacion, cancellationToken);

            var vm = new FabricaHilos.Models.Sire.SireMonitoreoViewModel
            {
                ApiLogs         = apiLogs.AsReadOnly(),
                FiltroJobId     = jobId,
                FiltroOperacion = operacion,
                TabActivo       = tab,
            };

            _logger.LogInformation("[SIRE] Monitoreo: {A} api-logs (tab={T})",
                apiLogs.Count, tab);

            return View("~/Views/Contabilidad/Sire/Monitoreo/Monitoreo.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar vista Monitoreo SIRE");
            TempData["Error"] = "Error al cargar el monitoreo SIRE";
            return View("~/Views/Contabilidad/Sire/Monitoreo/Monitoreo.cshtml",
                new FabricaHilos.Models.Sire.SireMonitoreoViewModel());
        }
    }

    /// <summary>
    /// Retorna los N jobs más recientes (cualquier estado) para el panel de actividad del Index.
    /// Incluye logs recientes para el job activo si lo hay.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> JobsRecientes(CancellationToken cancellationToken)
    {
        try
        {
            var jobs = await _sireRepo.GetJobsRecientesAsync(30, cancellationToken);

            // Obtener logs para CADA job activo (RVIE y RCE pueden correr simultáneamente)
            static bool esActivo(string e) =>
                e == EstadoJob.Pendiente || e == EstadoJob.EnProceso || e == EstadoJob.EsperandoTicket;

            var jobRvie = jobs.FirstOrDefault(j => j.TipoRegistro == "ventas"  && esActivo(j.Estado));
            var jobRce  = jobs.FirstOrDefault(j => j.TipoRegistro == "compras" && esActivo(j.Estado));

            static object MapLog(SireApiLog l) => new
            {
                l.Operacion,
                l.Exito,
                l.Mensaje,
                l.HttpStatus,
                l.DuracionMs,
                Fecha = l.Fecha.ToString("dd/MM/yyyy HH:mm:ss"),
            };

            var logsRvie = jobRvie is not null
                ? (await _sireRepo.GetApiLogsAsync(20, jobRvie.JobId, null, cancellationToken)).Select(MapLog)
                : null;
            var logsRce  = jobRce  is not null
                ? (await _sireRepo.GetApiLogsAsync(20, jobRce.JobId,  null, cancellationToken)).Select(MapLog)
                : null;

            return Json(new
            {
                exitoso = true,
                jobs = jobs.Select(j => new
                {
                    j.JobId,
                    j.TipoRegistro,
                    j.Periodo,
                    j.Estado,
                    j.NumTicket,
                    j.NombreArchivo,
                    j.RegistrosInsertados,
                    j.RegistrosDuplicados,
                    j.MensajeError,
                    j.UsuarioId,
                    FechaCreacion      = j.FechaCreacion.ToString("dd/MM/yyyy HH:mm:ss"),
                    FechaActualizacion = j.FechaActualizacion.ToString("dd/MM/yyyy HH:mm:ss"),
                    FechaFinalizacion  = j.FechaFinalizacion?.ToString("dd/MM/yyyy HH:mm:ss"),
                    ProximaConsulta    = j.ProximaConsulta?.ToString("dd/MM/yyyy HH:mm:ss"),
                    esFinal            = j.Estado == EstadoJob.Completado || j.Estado == EstadoJob.Error,
                    puedeReintentar    = j.Estado == EstadoJob.Error && !string.IsNullOrWhiteSpace(j.NumTicket),
                }),
                logsRvie,
                logsRce,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener jobs recientes SIRE");
            return Json(new { exitoso = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Crea un job de exportación asíncrona y lo encola para el BackgroundService.
    /// Si ya existe un job activo (Pendiente o EnProceso) para el mismo período y tipo,
    /// retorna ese job en lugar de crear uno nuevo.
    /// Retorna inmediatamente con el jobId; el front puede usar EstadoExportacion para consultar.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IniciarExportacion([FromBody] ExportarPropuestaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Periodo) || string.IsNullOrWhiteSpace(request.Tipo))
                return Json(new { exitoso = false, error = "Parámetros inválidos" });

            ValidarParametrosOperacion(request.Periodo, request.Tipo);

            // Solo puede existir UN job activo (Pendiente|EnProceso) por tipo.
            // GetJobActivoAsync ya NO filtra por período — garantiza unicidad por tipo.
            var tipo = request.Tipo.ToLowerInvariant();
            var jobExistente = await _sireRepo.GetJobActivoAsync(tipo, cancellationToken);

            if (jobExistente is not null)
            {
                var minutosParado = (int)(DateTime.Now - jobExistente.FechaActualizacion).TotalMinutes;

                // ── Caso A: mismo período → reconectar al proceso activo ──────────────
                if (jobExistente.Periodo == request.Periodo)
                {
                    // EsperandoTicket: el WatcherWorker lo gestiona — NO re-encolar en el worker principal
                    // porque el ticket todavía no está listo. Solo reconectar el front al job.
                    if (jobExistente.Estado != EstadoJob.EsperandoTicket)
                        _exportacionQueue.Encolar(jobExistente.Id);

                    _logger.LogInformation("[SIRE] Job activo reconectado {Tipo}/{Periodo}: jobId={JobId} estado={Estado} minutosParado={Min}",
                        tipo, request.Periodo, jobExistente.JobId, jobExistente.Estado, minutosParado);

                    return Json(new
                    {
                        exitoso        = true,
                        jobId          = jobExistente.JobId,
                        yaExiste       = true,
                        mismoPeriodo   = true,
                        estado         = jobExistente.Estado,
                        numTicket      = jobExistente.NumTicket,
                        minutosParado  = minutosParado,
                        fechaCreacion  = jobExistente.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                        proximaConsulta = jobExistente.ProximaConsulta?.ToString("dd/MM/yyyy HH:mm"),
                        mensaje        = $"Proceso existente para {request.Periodo} ({jobExistente.Estado}, {minutosParado} min). Reconectado."
                    });
                }

                // ── Caso B: período diferente → bloquear, no crear uno nuevo ─────────
                // Un tipo solo procesa de a un período a la vez.
                _logger.LogWarning("[SIRE] Intento de iniciar {Tipo}/{PeriodoNuevo} bloqueado: ya existe job activo para {PeriodoActivo} ({Estado})",
                    tipo, request.Periodo, jobExistente.Periodo, jobExistente.Estado);

                return Json(new
                {
                    exitoso          = false,
                    bloqueadoPorOtro = true,
                    jobId            = jobExistente.JobId,
                    estado           = jobExistente.Estado,
                    periodoActivo    = jobExistente.Periodo,
                    numTicket        = jobExistente.NumTicket,
                    proximaConsulta  = jobExistente.ProximaConsulta?.ToString("dd/MM/yyyy HH:mm"),
                    minutosParado    = minutosParado,
                    fechaCreacion    = jobExistente.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                    error            = $"Hay un proceso de {tipo} activo para el período {jobExistente.Periodo} ({jobExistente.Estado}). " +
                                       $"Espere que termine o cancélelo antes de iniciar uno nuevo."
                });
            }

            var job = new SireExportacionJob
            {
                TipoRegistro = tipo,
                Periodo      = request.Periodo,
                UsuarioId    = User.Identity?.Name ?? string.Empty,
                Estado       = EstadoJob.Pendiente,
            };

            await _sireRepo.InsertJobAsync(job, cancellationToken);

            _exportacionQueue.Encolar(job.Id);

            _logger.LogInformation("[SIRE] Job encolado: jobId={JobId} tipo={Tipo} periodo={Periodo} usuario={Usuario}",
                job.JobId, job.TipoRegistro, job.Periodo, job.UsuarioId);

            return Json(new
            {
                exitoso  = true,
                jobId    = job.JobId,
                yaExiste = false,
                mensaje  = "Exportación iniciada en segundo plano. Use 'Estado' para consultar el progreso."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al iniciar exportación asíncrona {Tipo} {Periodo}", request?.Tipo, request?.Periodo);
            return Json(new { exitoso = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Consulta el estado actual de un job de exportación asíncrona.
    /// El front llama esto periódicamente (polling ligero, solo lee SQLite).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EstadoExportacion(string jobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return Json(new { exitoso = false, error = "jobId inválido" });

        var job = await _sireRepo.GetJobByJobIdAsync(jobId, cancellationToken);

        if (job is null)
            return Json(new { exitoso = false, error = "Job no encontrado" });

        var minutosParado = (int)(DateTime.Now - job.FechaActualizacion).TotalMinutes;

        // Auto-sanar job en EsperandoTicket que quedó huérfano por shutdown del servidor:
        // si el último log TICKET registrado dice "Terminado" (estado final de SUNAT) pero el
        // worker fue interrumpido antes de persistir el estado Error, lo corregimos aquí.
        if (job.Estado == EstadoJob.EsperandoTicket)
        {
            var logs = await _sireRepo.GetApiLogsAsync(top: 10, jobId: job.JobId, ct: cancellationToken);
            var ultimoTicket = logs.FirstOrDefault(l => l.Operacion == SireOperacion.Ticket && l.Exito);
            if (ultimoTicket is not null &&
                ultimoTicket.Mensaje is not null &&
                ultimoTicket.Mensaje.Contains("Terminado", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "[SIRE] Job {JobId} en EsperandoTicket con último log TICKET=Terminado. " +
                    "Auto-saneando a Error (shutdown interrumpió el UpdateJob del Watcher).",
                    job.JobId);
                job.Estado             = EstadoJob.Error;
                job.MensajeError       = $"SUNAT finalizó el ticket {job.NumTicket} sin archivo (Terminado). " +
                                         "El servidor fue interrumpido antes de registrar el resultado. " +
                                         "Puede cancelar y volver a exportar.";
                job.ProximaConsulta    = null;
                job.FechaActualizacion = DateTime.Now;
                job.FechaFinalizacion  = DateTime.Now;
                await _sireRepo.UpdateJobAsync(job, cancellationToken);
            }
        }

        // Si el job está Pendiente, verificar si hay un job del otro tipo bloqueándolo
        string? bloqueadorTipo    = null;
        string? bloqueadorPeriodo = null;
        if (job.Estado == EstadoJob.Pendiente)
        {
            var otroTipo   = job.TipoRegistro.Equals("compras", StringComparison.OrdinalIgnoreCase) ? "ventas" : "compras";
            var bloqueador = await _sireRepo.GetJobActivoAsync(otroTipo, cancellationToken);
            if (bloqueador?.Estado == EstadoJob.EnProceso)
            {
                bloqueadorTipo    = otroTipo;
                bloqueadorPeriodo = bloqueador.Periodo;
            }
        }

        return Json(new
        {
            exitoso             = true,
            jobId               = job.JobId,
            estado              = job.Estado,
            tipoRegistro        = job.TipoRegistro,
            periodo             = job.Periodo,
            numTicket           = job.NumTicket,
            registrosInsertados = job.RegistrosInsertados,
            mensajeError        = job.MensajeError,
            fechaCreacion       = job.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
            fechaActualizacion  = job.FechaActualizacion.ToString("dd/MM/yyyy HH:mm"),
            fechaFinalizacion   = job.FechaFinalizacion?.ToString("dd/MM/yyyy HH:mm"),
            minutosParado       = minutosParado,
            bloqueadorTipo      = bloqueadorTipo,
            bloqueadorPeriodo   = bloqueadorPeriodo,
            esFinal             = job.Estado == EstadoJob.Completado || job.Estado == EstadoJob.Error,
            puedeReintentar     = job.Estado == EstadoJob.Error && !string.IsNullOrWhiteSpace(job.NumTicket),
            // Huérfano: Pendiente >2 min sin avanzar = el ítem del Channel se perdió por restart
            posibleHuerfano     = job.Estado == EstadoJob.Pendiente && minutosParado >= 2,
            puedeCancel         = job.Estado != EstadoJob.Completado,
            proximaConsulta     = job.ProximaConsulta?.ToString("dd/MM/yyyy HH:mm")
        });
    }

    /// <summary>
    /// Cancela un job (lo marca como Error) para liberar el período y poder crear uno nuevo.
    /// Útil cuando el job lleva mucho tiempo parado o el ticket SUNAT expiró.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarJob([FromBody] string jobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return Json(new { exitoso = false, error = "jobId inválido" });

        var job = await _sireRepo.GetJobByJobIdAsync(jobId, cancellationToken);
        if (job is null)
            return Json(new { exitoso = false, error = "Job no encontrado" });

        if (job.Estado == EstadoJob.Completado)
            return Json(new { exitoso = false, error = "El job ya está completado, no se puede cancelar." });

        job.Estado             = EstadoJob.Error;
        job.MensajeError       = $"Cancelado manualmente por {User.Identity?.Name} el {DateTime.Now:dd/MM/yyyy HH:mm}";
        job.FechaActualizacion = DateTime.Now;
        job.FechaFinalizacion  = DateTime.Now;
        await _sireRepo.UpdateJobAsync(job, cancellationToken);

        await _sireRepo.InsertApiLogAsync(new SireApiLog
        {
            JobId     = job.JobId,
            Operacion = "CANCEL",
            Exito     = false,
            Mensaje   = job.MensajeError,
            Fecha     = DateTime.Now,
        }, cancellationToken);

        _logger.LogWarning("[SIRE] Job cancelado manualmente: {JobId} tipo={Tipo} periodo={Periodo} por {User}",
            jobId, job.TipoRegistro, job.Periodo, User.Identity?.Name);

        return Json(new { exitoso = true, mensaje = "Job cancelado. Puede iniciar una nueva exportación." });
    }

    /// <summary>
    /// Retorna los últimos registros de SIRE_LOG para un job específico.
    /// Permite al modal de progreso mostrar qué operaciones se ejecutaron.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> LogsJob(string jobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return Json(new { exitoso = false, error = "jobId inválido" });

        var logs = await _sireRepo.GetApiLogsAsync(30, jobId, null, cancellationToken, ordenAscendente: true);
        return Json(new
        {
            exitoso = true,
            logs = logs.Select(l => new
            {
                operacion  = l.Operacion,
                exito      = l.Exito,
                httpStatus = l.HttpStatus,
                duracionMs = l.DuracionMs,
                metodo     = l.MetodoHttp,
                url        = l.Url,
                mensaje    = l.Mensaje,
                fecha      = l.Fecha.ToString("dd/MM/yyyy HH:mm:ss")
            })
        });
    }

    /// <summary>
    /// Re-procesa el ZIP local más reciente (ya descargado en disco) para un período+tipo,
    /// sin contactar SUNAT. Útil cuando el parser fue corregido y los jobs "Completados"
    /// tienen REG_INSERTADOS=0 por el bug anterior.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReprocesarZipLocal([FromBody] ReprocesarZipRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Tipo) || string.IsNullOrWhiteSpace(request.Periodo))
            return Json(new { exitoso = false, error = "Tipo y Periodo son obligatorios" });

        // Buscar el job más reciente con RUTA_ARCHIVO para ese tipo+período
        var jobs = await _sireRepo.GetJobsRecientesAsync(50, cancellationToken);
        var job = jobs
            .Where(j => j.TipoRegistro == request.Tipo
                     && j.Periodo == request.Periodo
                     && !string.IsNullOrWhiteSpace(j.RutaArchivo)
                     && System.IO.File.Exists(j.RutaArchivo))
            .OrderByDescending(j => j.FechaCreacion)
            .FirstOrDefault();

        if (job is null)
        {
            await _sireRepo.InsertApiLogAsync(new SireApiLog
            {
                JobId        = null,
                Operacion    = SireOperacion.Reprocesar,
                Exito        = false,
                TipoRegistro = request.Tipo,
                Mensaje      = $"ZIP no encontrado: tipo={request.Tipo} periodo={request.Periodo}. Sin archivo local.",
                Fecha        = DateTime.Now,
            }, cancellationToken);
            return Json(new { exitoso = false, error = $"No se encontró ZIP local para tipo={request.Tipo} periodo={request.Periodo}. Use 'Exportar Propuesta' para descargar desde SUNAT." });
        }

        var usuario = User.Identity?.Name ?? "desconocido";
        var archivo = System.IO.Path.GetFileName(job.RutaArchivo);

        _logger.LogInformation("[SIRE] ReprocesarZipLocal: tipo={Tipo} periodo={Periodo} archivo={Archivo}",
            request.Tipo, request.Periodo, job.RutaArchivo);

        // LOG: inicio del reproceso
        await _sireRepo.InsertApiLogAsync(new SireApiLog
        {
            JobId        = job.JobId,
            Operacion    = SireOperacion.Reprocesar,
            Exito        = true,
            TipoRegistro = request.Tipo,
            Mensaje      = $"tipo={request.Tipo} periodo={request.Periodo} usuario={usuario} archivo={archivo}",
            Fecha        = DateTime.Now,
        }, cancellationToken);

        byte[] contenido;
        try   { contenido = await System.IO.File.ReadAllBytesAsync(job.RutaArchivo!, cancellationToken); }
        catch (Exception ex)
        {
            await _sireRepo.InsertApiLogAsync(new SireApiLog
            {
                JobId        = job.JobId,
                Operacion    = SireOperacion.Cargar,
                Exito        = false,
                TipoRegistro = request.Tipo,
                Mensaje      = $"Error leyendo archivo: {ex.Message}",
                Fecha        = DateTime.Now,
            }, cancellationToken);
            return Json(new { exitoso = false, error = $"No se pudo leer el archivo: {ex.Message}" });
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var resultado = await _validaService.CargarDesdeZipAsync(
            contenido, request.Tipo, request.Periodo, job.JobId, cancellationToken);
        sw.Stop();

        // LOG: resultado del parseo/carga en Oracle
        await _sireRepo.InsertApiLogAsync(new SireApiLog
        {
            JobId        = job.JobId,
            Operacion    = SireOperacion.Cargar,
            DuracionMs   = sw.ElapsedMilliseconds,
            Exito        = resultado.Errores == 0 || resultado.Insertados > 0,
            TipoRegistro = request.Tipo,
            Mensaje      = $"Oracle SIRE_VALIDA actualizado: {resultado.Insertados} insertados, {resultado.Duplicados} duplicados, {resultado.Errores} errores",
            Fecha        = DateTime.Now,
        }, cancellationToken);

        // Actualizar REG_INSERTADOS en el job
        job.RegistrosInsertados = resultado.Insertados;
        job.RegistrosDuplicados = resultado.Duplicados;
        await _sireRepo.UpdateJobAsync(job, cancellationToken);

        var exitoso = resultado.Errores == 0 || resultado.Insertados > 0;

        // Invalidar la conciliación previa — el cruce SUNAT vs ERP ya no es válido
        // El usuario deberá ejecutar "Conciliar" nuevamente para regenerarlo.
        if (exitoso && int.TryParse(request.Periodo, out var periodoNr))
        {
            try { await _sireRepo.InvalidarConciliacionAsync(request.Tipo, periodoNr, cancellationToken); }
            catch (Exception exInv)
            {
                _logger.LogWarning(exInv, "[SIRE] No se pudo invalidar conciliación tras reprocesar: {Msg}", exInv.Message);
            }
        }

        // Invalidar caché de períodos para que el dashboard refleje los nuevos datos
        _cache.Remove($"sire:periodos:{request.Tipo.ToLowerInvariant()}");
        _cache.Remove($"sire:periodos:{request.Tipo.ToLowerInvariant()}:all");

        // LOG: completar
        await _sireRepo.InsertApiLogAsync(new SireApiLog
        {
            JobId        = job.JobId,
            Operacion    = SireOperacion.Completar,
            Exito        = exitoso,
            TipoRegistro = request.Tipo,
            Mensaje      = exitoso
                ? $"Re-proceso completado exitosamente: {resultado.Insertados} reg. insertados, {resultado.Duplicados} duplicados"
                : $"Re-proceso con errores: {resultado.Errores} errores de parseo",
            Fecha        = DateTime.Now,
        }, cancellationToken);

        _logger.LogInformation("[SIRE] ReprocesarZipLocal completado: {Ins} insertados, {Dup} dup, {Err} errores",
            resultado.Insertados, resultado.Duplicados, resultado.Errores);

        return Json(new
        {
            exitoso,
            insertados          = resultado.Insertados,
            duplicados          = resultado.Duplicados,
            errores             = resultado.Errores,
            archivoUsado        = archivo,
            observaciones       = resultado.Observaciones.Take(5).ToList()
        });
    }

    /// <summary>
    /// Reencola un job en estado Error que tiene NumTicket guardado.
    /// Permite reanudar el polling sin volver a llamar a SUNAT para exportar.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReintentarJob([FromBody] string jobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return Json(new { exitoso = false, error = "jobId inválido" });

        var job = await _sireRepo.GetJobByJobIdAsync(jobId, cancellationToken);

        if (job is null)
            return Json(new { exitoso = false, error = "Job no encontrado" });

        if (job.Estado != EstadoJob.Error)
            return Json(new { exitoso = false, error = $"El job no está en estado Error (estado actual: {job.Estado})" });

        if (string.IsNullOrWhiteSpace(job.NumTicket))
            return Json(new { exitoso = false, error = "El job no tiene ticket SUNAT guardado. Use 'Exportar Propuesta' para iniciar desde cero." });

        job.Estado             = EstadoJob.EnProceso;
        job.MensajeError       = null;
        job.FechaActualizacion = DateTime.Now;
        job.FechaFinalizacion  = null;
        await _sireRepo.UpdateJobAsync(job, cancellationToken);

        _exportacionQueue.Encolar(job.Id);

        _logger.LogInformation("[SIRE] Job reencolado manualmente: jobId={JobId} ticket={Ticket} tipo={Tipo} periodo={Periodo}",
            job.JobId, job.NumTicket, job.TipoRegistro, job.Periodo);

        return Json(new
        {
            exitoso = true,
            jobId   = job.JobId,
            mensaje = $"Job reencolado. Retomará el polling del ticket {job.NumTicket}."
        });
    }

    /// <summary>
    /// Reencola un job Pendiente huérfano (perdido del Channel por restart del servidor).
    /// Solo aplica a jobs en estado Pendiente; no requiere ticket SUNAT.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReencolarJobHuerfano([FromBody] string jobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return Json(new { exitoso = false, error = "jobId inválido" });

        var job = await _sireRepo.GetJobByJobIdAsync(jobId, cancellationToken);

        if (job is null)
            return Json(new { exitoso = false, error = "Job no encontrado" });

        if (job.Estado != EstadoJob.Pendiente)
            return Json(new { exitoso = false, error = $"El job no está en estado Pendiente (estado actual: {job.Estado}). Use 'Reintentar' para jobs en Error." });

        _exportacionQueue.Encolar(job.Id);

        _logger.LogWarning("[SIRE] Job huérfano reencolado manualmente: jobId={JobId} tipo={Tipo} periodo={Periodo} por {User}",
            job.JobId, job.TipoRegistro, job.Periodo, User.Identity?.Name);

        return Json(new
        {
            exitoso = true,
            jobId   = job.JobId,
            mensaje = "Job reencolado. El worker lo procesará en breve."
        });
    }

    // ── Exclusiones manuales ──────────────────────────────────────────────────

    /// <summary>
    /// AJAX-POST: excluye manualmente los registros SOLO_SUNAT seleccionados.
    /// Body JSON: { tipo, periodo, idsConcil: [long], obs? }
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExcluirManual(
        [FromBody] ExcluirManualRequest request, CancellationToken ct)
    {
        if (request is null || request.IdsConcil is null || request.IdsConcil.Count == 0)
            return Json(new { exitoso = false, error = "Sin registros seleccionados" });

        try
        {
            ValidarParametrosOperacion(request.Periodo, request.Tipo);
            if (!int.TryParse(request.Periodo, out var periodoNr))
                return Json(new { exitoso = false, error = "Período inválido" });

            var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "SIG";
            var excluidos = await _sireRepo.ExcluirManualAsync(
                request.Tipo, periodoNr, request.IdsConcil, usuario, request.Obs, ct);

            return Json(new {
                exitoso   = true,
                excluidos,
                mensaje   = $"{excluidos} registro(s) excluido(s)",
                usuario,
                obs       = request.Obs,
                fechaHora = DateTime.Now.ToString("dd/MM/yy HH:mm")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIRE] ExcluirManual error: tipo={Tipo} periodo={Periodo}", request.Tipo, request.Periodo);
            return Json(new { exitoso = false, error = ex.Message });
        }
    }

    /// <summary>
    /// AJAX-POST: restaura un excluido (y su par vinculado si existe)
    /// devolviéndolo a SOLO_SUNAT.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestaurarExcluido(
        [FromBody] RestaurarExcluidoRequest request, CancellationToken ct)
    {
        if (request is null || request.IdConcil <= 0)
            return Json(new { exitoso = false, error = "ID inválido" });

        try
        {
            var usuario = HttpContext.Session.GetString("OracleUser") ?? User.Identity?.Name ?? "SIG";
            await _sireRepo.RestaurarExcluidoAsync(request.IdConcil, usuario, ct);
            return Json(new { exitoso = true, mensaje = "Registro restaurado a SOLO_SUNAT" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIRE] RestaurarExcluido error: idConcil={Id}", request.IdConcil);
            return Json(new { exitoso = false, error = ex.Message });
        }
    }
}

public sealed record SirePeriodoDashboardItem(string Periodo, string Descripcion, string EstadoRvie, string EstadoRce, string DescRvie = "-", string DescRce = "-");
public sealed record ReprocesarZipRequest(string Tipo, string Periodo);
public sealed record ExcluirManualRequest(string Tipo, string Periodo, List<long> IdsConcil, string? Obs);
public sealed record RestaurarExcluidoRequest(long IdConcil);



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

// DTO para POST JSON de exportación
public class ExportarPropuestaRequest
{
    public string Periodo { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
}

// DTO para validación masiva contra API Consulta Integrada SUNAT
public class ValidarCpeBatchRequest
{
    public string Tipo    { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
}
