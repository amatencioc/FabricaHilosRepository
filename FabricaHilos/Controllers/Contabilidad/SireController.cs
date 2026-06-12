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
using Microsoft.EntityFrameworkCore;
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
    private readonly ILogger<SireController> _logger;

    public SireController(
        ISireVentasService ventasService,
        ISireComprasService comprasService,
        ISireAuthService authService,
        ITusUploadService tusUploadService,
        TicketPollingHelper ticketPolling,
        ILazySireInitializer lazySireInitializer,
        IOptions<SireOptions> sireOptions,
        ILogger<SireController> logger)
    {
        _ventasService         = ventasService;
        _comprasService        = comprasService;
        _authService           = authService;
        _tusUploadService      = tusUploadService;
        _ticketPolling         = ticketPolling;
        _lazySireInitializer   = lazySireInitializer;
        _sireOptions           = sireOptions.Value;
        _logger                = logger;
    }

    /// <summary>
    /// Dashboard principal - Resumen ejecutivo de RVIE y RCE
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            // Asegurar que SIRE está inicializado (lazy loading)
            if (!_lazySireInitializer.IsInitialized)
            {
                _logger.LogInformation("[SIRE] Inicializando servicios SIRE en Index...");
                await _lazySireInitializer.InitializeAsync();
            }

            var ventas  = FiltrarAnioActual(await _ventasService.ObtenerPeriodosAsync(cancellationToken));
            var compras = FiltrarAnioActual(await _comprasService.ObtenerPeriodosAsync(cancellationToken));
            var model = ConstruirDashboard(ventas, compras);
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
            var todosLosPeriodos = await _ventasService.ObtenerPeriodosAsync(cancellationToken);
            var periodos = FiltrarAnioActual(todosLosPeriodos);
            var periodoSeleccionado = periodo ?? periodos.FirstOrDefault()?.Periodo ?? string.Empty;

            // ✅ FLUJO CORRECTO: No intentar obtener registros directamente (endpoint deprecated)
            // En su lugar, mostrar periodos disponibles para exportar/descargar
            var model = new SireRegistrosViewModel
            {
                Periodos = periodos,
                PeriodoSeleccionado = periodoSeleccionado,
                RegistrosVentas = Array.Empty<RegistroVenta>(), // ← Vacío, ya que endpoint es incorrecto
                EsMock = _sireOptions.UseMock,
                Tipo = TipoRegistro.Ventas,
                MensajeInfo = "Para descargar registros, use 'Descargar Propuesta' que exportará el archivo desde SUNAT.",
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
            var todosLosPeriodos = await _comprasService.ObtenerPeriodosAsync(cancellationToken);
            var periodos = FiltrarAnioActual(todosLosPeriodos);
            var periodoSeleccionado = periodo ?? periodos.FirstOrDefault()?.Periodo ?? string.Empty;

            // ✅ FLUJO CORRECTO: No intentar obtener registros directamente (endpoint deprecated)
            // En su lugar, mostrar periodos disponibles para exportar/descargar
            var model = new SireRegistrosViewModel
            {
                Periodos = periodos,
                PeriodoSeleccionado = periodoSeleccionado,
                RegistrosCompras = Array.Empty<RegistroCompra>(), // ← Vacío, ya que endpoint es incorrecto
                EsMock = _sireOptions.UseMock,
                Tipo = TipoRegistro.Compras,
                MensajeInfo = "Para descargar registros, use 'Descargar Propuesta' que exportará el archivo desde SUNAT.",
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
            Timestamp = DateTime.UtcNow,
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
            .OrderByDescending(p => p.Periodo)
            .ToList();
    }

    /// <summary>Construye el dashboard con información de RVIE y RCE</summary>
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
    /// Historial de ejecución de health checks del monitoreo SIRE
    /// Muestra últimos 500 registros ordenados por fecha descendente
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> HealthHistory(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FabricaHilos.Data.ApplicationDbContext>();

            var logs = await context.SireHealthCheckLogs
                .OrderByDescending(x => x.FechaUtc)
                .Take(500)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Health history: {Count} registros cargados", logs.Count);
            return View("~/Views/Contabilidad/Sire/Monitoreo/HealthHistory.cshtml", logs.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar historial de health checks");
            TempData["Error"] = "Error al cargar el historial de health checks";
            return View("~/Views/Contabilidad/Sire/Monitoreo/HealthHistory.cshtml", new List<FabricaHilos.Models.Sire.SireHealthCheckLog>().AsReadOnly());
        }
    }
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

// DTO para POST JSON de exportación
public class ExportarPropuestaRequest
{
    public string Periodo { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
}
