using FabricaHilos.Models.Sire;
using FabricaHilos.Sire.Constants;
using FabricaHilos.Sire.Helpers;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using FabricaHilos.Sire.Services;
using Microsoft.Extensions.Options;
using System.Net;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// BackgroundService que procesa jobs de exportación SIRE (Fase 1).
/// Flujo por job:
///   1. Llama ExportarPropuestaAsync() → obtiene numTicket de SUNAT.
///   2. Polling ConsultarTicketAsync() hasta estado final O hasta TicketMaxRetries.
///   3a. Si ticket listo → descarga ZIP, guarda en red, carga en Oracle SIRE_VALIDA.
///   3b. Si TIMEOUT (SUNAT tarda > 10 min) → guarda estado EsperandoTicket + ProximaConsulta
///       y libera el worker. SireTicketWatcherWorker retoma en Fase 2 (cada 15 min).
/// </summary>
public sealed class SireExportacionWorker : BackgroundService
{
    private readonly IServiceScopeFactory           _scopeFactory;
    private readonly ISireExportacionQueue          _queue;
    private readonly IConfiguration                 _configuration;
    private readonly ISireOracleRepository          _repo;
    private readonly ILogger<SireExportacionWorker> _logger;
    private readonly ILazySireInitializer           _lazySireInitializer;
    private readonly SireOptions                    _sireOptions;

    public SireExportacionWorker(
        IServiceScopeFactory            scopeFactory,
        ISireExportacionQueue           queue,
        IConfiguration                  configuration,
        ISireOracleRepository           repo,
        ILogger<SireExportacionWorker>  logger,
        ILazySireInitializer            lazySireInitializer,
        IOptions<SireOptions>           sireOptions)
    {
        _scopeFactory        = scopeFactory;
        _queue               = queue;
        _configuration       = configuration;
        _repo                = repo;
        _logger              = logger;
        _lazySireInitializer = lazySireInitializer;
        _sireOptions         = sireOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SIRE-WORKER] Servicio de exportación asíncrona iniciado.");

        // Esperar inicialización lazy de SIRE.
        // Si nadie navega a Contabilidad → SIRE en 2 minutos, se auto-inicializa
        // para soportar operación desatendida (reinicios nocturnos, jobs en cola).
        await EnsureSireInitializedAsync(stoppingToken);
        if (stoppingToken.IsCancellationRequested) return;

        // Al arrancar, reencolar jobs que quedaron en estado Pendiente o EnProceso
        await ReencolarJobsInterrumpidosAsync(stoppingToken);

        // Watchdog: cada 2 minutos verifica si hay jobs Pendiente huérfanos
        // (jobs que se perdieron del Channel en memoria por restart de la app)
        var watchdogTimer = new System.Diagnostics.Stopwatch();
        watchdogTimer.Start();
        const int watchdogIntervalMs = 120_000; // 2 minutos

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Esperar ítem en cola con timeout de 30s para poder ejecutar el watchdog
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(30_000);
                try
                {
                    var jobId = await _queue.DequeueAsync(cts.Token);
                    await ProcesarJobAsync(jobId, stoppingToken);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Timeout de 30s: no hay jobs en cola — verificar huérfanos si es momento
                }

                // Watchdog: reencolar jobs Pendiente huérfanos cada 2 minutos
                if (watchdogTimer.ElapsedMilliseconds >= watchdogIntervalMs)
                {
                    watchdogTimer.Restart();
                    await ReencolarJobsHuerfanosAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIRE-WORKER] Error inesperado en el loop principal del worker.");
                try { await Task.Delay(5000, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("[SIRE-WORKER] Servicio de exportación asíncrona detenido.");
    }

    /// <summary>Al reiniciar la app, re-encola los jobs que no terminaron.</summary>
    private async Task ReencolarJobsInterrumpidosAsync(CancellationToken cancellationToken)
    {
        var jobs = await _repo.GetJobsInterrumpidosAsync(cancellationToken);
        foreach (var job in jobs)
        {
            _queue.Encolar(job.Id);
            _logger.LogInformation("[SIRE-WORKER] Job {Id} reencolado (estado interrumpido).", job.Id);
        }
    }

    /// <summary>
    /// Watchdog periódico: reencola jobs Pendiente que llevan más de 2 min sin procesar.
    /// Esto recupera jobs cuyo ítem del Channel se perdió por restart de la aplicación.
    /// Solo re-encola si el job NO está ya en el Channel (verifica que lleva tiempo parado).
    /// </summary>
    private async Task ReencolarJobsHuerfanosAsync(CancellationToken cancellationToken)
    {
        try
        {
            var jobs = await _repo.GetJobsInterrumpidosAsync(cancellationToken);
            var yaReencolados = new HashSet<int>();
            foreach (var job in jobs)
            {
                // Solo reencola jobs Pendiente/EnProceso con más de 2 min de antigüedad
                // EsperandoTicket lo gestiona el WatcherWorker, no este watchdog
                if (job.Estado == EstadoJob.EsperandoTicket) continue;
                // Evitar reencolado doble si el mismo job aparece dos veces en la consulta
                if (!yaReencolados.Add(job.Id)) continue;

                var minutosParado = (DateTime.Now - job.FechaActualizacion).TotalMinutes;
                if (minutosParado >= 2)
                {
                    _queue.Encolar(job.Id);
                    _logger.LogWarning(
                        "[SIRE-WORKER] Watchdog: Job huérfano reencolado — {JobId} tipo={Tipo} periodo={Periodo} " +
                        "estado={Estado} parado={Min:F1} min.",
                        job.JobId, job.TipoRegistro, job.Periodo, job.Estado, minutosParado);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIRE-WORKER] Error en watchdog de jobs huérfanos.");
        }
    }

    private async Task ProcesarJobAsync(int jobId, CancellationToken stoppingToken)
    {
        using var scope          = _scopeFactory.CreateScope();
        var ventasService        = scope.ServiceProvider.GetRequiredService<ISireVentasService>();
        var comprasService       = scope.ServiceProvider.GetRequiredService<ISireComprasService>();
        var validaService        = scope.ServiceProvider.GetRequiredService<SireValidaService>();
        var ticketPolling        = scope.ServiceProvider.GetRequiredService<TicketPollingHelper>();

        var job = await _repo.GetJobByIdAsync(jobId, stoppingToken);
        if (job is null)
        {
            _logger.LogWarning("[SIRE-WORKER] Job {Id} no encontrado en Oracle.", jobId);
            return;
        }

        // Guard: si el job fue cancelado mientras esperaba en cola, omitirlo.
        if (job.Estado != EstadoJob.Pendiente && job.Estado != EstadoJob.EnProceso)
        {
            _logger.LogInformation("[SIRE-WORKER] Job {Id} ({JobId}) omitido — fue cancelado/completado mientras estaba en cola (estado: {Estado}).",
                jobId, job.JobId, job.Estado);
            return;
        }

        _logger.LogInformation("[SIRE-WORKER] Procesando job {JobId} | tipo={Tipo} periodo={Periodo}",
            job.JobId, job.TipoRegistro, job.Periodo);

        var esVentas = job.TipoRegistro.Equals("ventas", StringComparison.OrdinalIgnoreCase);

        // Helper local: inserta un log de auditoría
        async Task LogFinAsync(string operacion, long duracionMs, bool exito, string? mensaje, int? httpStatus = null)
        {
            var entry = new SireApiLog
            {
                JobId      = job.JobId,
                Operacion  = operacion,
                DuracionMs = duracionMs,
                HttpStatus = httpStatus,
                Exito      = exito,
                Mensaje    = mensaje?[..Math.Min(mensaje.Length, 2000)],
                Fecha      = DateTime.Now,
            };
            await _repo.InsertApiLogAsync(entry, stoppingToken);
        }

        try
        {
            // ── PASO 1: Marcar en proceso ──────────────────────────────────────────
            job.Estado             = EstadoJob.EnProceso;
            job.FechaActualizacion = DateTime.Now;
            await _repo.UpdateJobAsync(job, stoppingToken);
            await LogFinAsync(SireOperacion.Iniciar, 0, true,
                $"tipo={job.TipoRegistro} periodo={job.Periodo} usuario={job.UsuarioId}");

            // ── PASO 2: Exportar propuesta → obtener ticket
            // Si el job ya tiene NumTicket (reencole tras 504/interrupción) se salta este paso.
            if (string.IsNullOrWhiteSpace(job.NumTicket))
            {
                _logger.LogInformation("[SIRE-WORKER] [{JobId}] Solicitando exportación a SUNAT...", job.JobId);
                var swExportar = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var ticketInicial = esVentas
                        ? await ventasService.ExportarPropuestaAsync(job.Periodo, stoppingToken)
                        : await comprasService.ExportarPropuestaAsync(job.Periodo, stoppingToken);

                    swExportar.Stop();
                    job.NumTicket          = ticketInicial.NumTicket;
                    job.FechaActualizacion = DateTime.Now;
                    await _repo.UpdateJobAsync(job, stoppingToken);
                    await LogFinAsync(SireOperacion.Exportar, swExportar.ElapsedMilliseconds, true,
                        $"Ticket obtenido: {ticketInicial.NumTicket}");
                    _logger.LogInformation("[SIRE-WORKER] [{JobId}] Ticket obtenido: {Ticket}", job.JobId, job.NumTicket);
                }
                catch (SireApiException sae422) when (sae422.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
                {
                    swExportar.Stop();
                    var msgNegocio = ExtraerMensajeSunat(sae422.Message);
                    await LogFinAsync(SireOperacion.Exportar, swExportar.ElapsedMilliseconds, false, msgNegocio, 422);
                    _logger.LogWarning("[SIRE-WORKER] [{JobId}] SUNAT rechazó la exportación (422): {Msg}", job.JobId, msgNegocio);
                    job.Estado             = EstadoJob.Error;
                    job.MensajeError       = msgNegocio;
                    job.FechaActualizacion = DateTime.Now;
                    job.FechaFinalizacion  = DateTime.Now;
                    await _repo.UpdateJobAsync(job, stoppingToken);
                    await LogFinAsync(EstadoJob.Error, 0, false, msgNegocio);
                    return; // fin limpio, no es un error del sistema
                }
                catch
                {
                    swExportar.Stop();
                    await LogFinAsync(SireOperacion.Exportar, swExportar.ElapsedMilliseconds, false,
                        "Error al solicitar exportación a SUNAT");
                    throw;
                }
            }
            else
            {
                _logger.LogInformation("[SIRE-WORKER] [{JobId}] Reutilizando ticket existente: {Ticket}", job.JobId, job.NumTicket);
            }

            // ── PASO 3: Polling hasta estado final ────────────────────────────────
            _logger.LogInformation("[SIRE-WORKER] [{JobId}] Esperando estado final del ticket...", job.JobId);
            var swTicket = System.Diagnostics.Stopwatch.StartNew();
            TicketEstado ticketFinal;
            try
            {
                ticketFinal = await ticketPolling.EsperarEstadoFinalAsync(
                    ct => esVentas
                        ? ventasService.ConsultarTicketAsync(job.NumTicket!, job.Periodo, ct)
                        : comprasService.ConsultarTicketAsync(job.NumTicket!, job.Periodo, ct),
                    stoppingToken);
                swTicket.Stop();
                await LogFinAsync(SireOperacion.Ticket, swTicket.ElapsedMilliseconds,
                    !string.Equals(ticketFinal.Estado, "TIMEOUT", StringComparison.OrdinalIgnoreCase),
                    $"Estado SUNAT: {ticketFinal.Estado} — {ticketFinal.Mensaje}");
            }
            catch (SireApiException sae422) when (sae422.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            {
                swTicket.Stop();
                var msgNegocio = ExtraerMensajeSunat(sae422.Message);
                await LogFinAsync(SireOperacion.Ticket, swTicket.ElapsedMilliseconds, false, msgNegocio, 422);
                _logger.LogWarning("[SIRE-WORKER] [{JobId}] SUNAT rechazó el ticket (422): {Msg}", job.JobId, msgNegocio);
                job.Estado             = EstadoJob.Error;
                job.MensajeError       = msgNegocio;
                job.FechaActualizacion = DateTime.Now;
                job.FechaFinalizacion  = DateTime.Now;
                await _repo.UpdateJobAsync(job, stoppingToken);
                await LogFinAsync(EstadoJob.Error, 0, false, msgNegocio);
                return; // fin limpio, no es un error del sistema
            }
            catch
            {
                swTicket.Stop();
                await LogFinAsync(SireOperacion.Ticket, swTicket.ElapsedMilliseconds, false, "Error en polling de ticket");
                throw;
            }

            job.CodProceso         = ticketFinal.CodProceso     ?? job.CodProceso;
            job.NombreArchivo      = ticketFinal.ArchivoReporte?.NomArchivoReporte ?? job.NombreArchivo;
            job.CodTipoArchivo     = ticketFinal.ArchivoReporte?.CodTipoArchivoReporte ?? job.CodTipoArchivo;
            job.FechaActualizacion = DateTime.Now;
            await _repo.UpdateJobAsync(job, stoppingToken);

            // Caso 1: SUNAT no terminó en el tiempo de la Fase 1 → delegar a SireTicketWatcherWorker.
            // El worker queda libre para procesar otros jobs.
            if (string.Equals(ticketFinal.Estado, "TIMEOUT", StringComparison.OrdinalIgnoreCase))
            {
                var proxima = DateTime.Now.AddMinutes(_sireOptions.WatcherIntervalMin);
                job.Estado           = EstadoJob.EsperandoTicket;
                job.ProximaConsulta  = proxima;
                job.FechaActualizacion = DateTime.Now;
                await _repo.UpdateJobAsync(job, stoppingToken);
                await LogFinAsync(SireOperacion.Ticket, 0, true,
                    $"TIMEOUT Fase 1. Delegado a WatcherWorker. PRÓXIMA CONSULTA: {proxima:dd/MM/yyyy HH:mm}");
                _logger.LogInformation(
                    "[SIRE-WORKER] [{JobId}] SUNAT aún procesa. Job pasa a EsperandoTicket. "
                    + "WatcherWorker consultará a las {Proxima:HH:mm}.",
                    job.JobId, proxima);
                return; // libera el worker
            }

            // Caso 2: SUNAT finalizó pero con error o rechazo (sin archivo adjunto).
            if (string.IsNullOrWhiteSpace(ticketFinal.ArchivoReporte?.NomArchivoReporte))
                throw new InvalidOperationException(
                    $"SUNAT finalizó el ticket {job.NumTicket} con estado '{ticketFinal.Estado}' "
                    + $"sin generar archivo. {ticketFinal.Mensaje}".TrimEnd());

            _logger.LogInformation("[SIRE-WORKER] [{JobId}] Archivo disponible: {Archivo}",
                job.JobId, job.NombreArchivo);

            // ── PASO 4: Descargar el ZIP ──────────────────────────────────────────
            var nomArchivo     = ticketFinal.ArchivoReporte!.NomArchivoReporte!;
            var codTipoArchivo = ticketFinal.ArchivoReporte.CodTipoArchivoReporte ?? string.Empty;
            var codLibro       = esVentas ? "140000" : "080000";

            var urlDescarga = SireEndpoints.DescargarArchivo(
                nomArchivo,
                codTipoArchivo,
                codLibro,
                ticketFinal.PerTributario.Length > 0 ? ticketFinal.PerTributario : job.Periodo,
                ticketFinal.CodProceso ?? string.Empty,
                job.NumTicket!);

            var swDescarga = System.Diagnostics.Stopwatch.StartNew();
            ConstanciaCierre archivo;
            try
            {
                archivo = esVentas
                    ? await ventasService.DescargarArchivoReporteAsync(urlDescarga, nomArchivo, stoppingToken)
                    : await comprasService.DescargarArchivoReporteAsync(urlDescarga, nomArchivo, stoppingToken);
                swDescarga.Stop();
                await LogFinAsync(SireOperacion.Descargar, swDescarga.ElapsedMilliseconds, true,
                    $"ZIP descargado: {archivo.NombreArchivo} ({archivo.Contenido.Length} bytes)", 200);
            }
            catch
            {
                swDescarga.Stop();
                await LogFinAsync(SireOperacion.Descargar, swDescarga.ElapsedMilliseconds, false, $"Error al descargar ZIP: {nomArchivo}");
                throw;
            }

            _logger.LogInformation("[SIRE-WORKER] [{JobId}] ZIP descargado: {Bytes} bytes", job.JobId, archivo.Contenido.Length);

            // ── PASO 5: Guardar en ruta de red ────────────────────────────────────
            var rutaBase   = _configuration["RutaSireExportacion"]
                ?? @"\\10.0.7.14\FabricaHilos\Contabilidad\Sire";
            var subcarpeta = esVentas ? "Ventas" : "Compras";
            var rutaDest   = Path.Combine(rutaBase, subcarpeta);

            await GuardarEnRedAsync(rutaDest, archivo.NombreArchivo, archivo.Contenido, stoppingToken);
            job.RutaArchivo        = Path.Combine(rutaDest, archivo.NombreArchivo);
            job.FechaActualizacion = DateTime.Now;
            await _repo.UpdateJobAsync(job, stoppingToken);
            await LogFinAsync(SireOperacion.Guardar, 0, true,
                $"Archivo guardado: {job.RutaArchivo}");

            _logger.LogInformation("[SIRE-WORKER] [{JobId}] Archivo guardado en: {Ruta}", job.JobId, job.RutaArchivo);

            // ── PASO 6:
            _logger.LogInformation("[SIRE-WORKER] [{JobId}] Cargando propuesta en Oracle SIRE_PROPUESTA...", job.JobId);
            var swCargar = System.Diagnostics.Stopwatch.StartNew();
            var resultado = await validaService.CargarDesdeZipAsync(
                archivo.Contenido, job.TipoRegistro, job.Periodo, job.JobId, stoppingToken);
            swCargar.Stop();

            job.RegistrosInsertados  = resultado.Insertados;
            job.RegistrosDuplicados  = resultado.Duplicados;
            await LogFinAsync(SireOperacion.Cargar, swCargar.ElapsedMilliseconds, true,
                $"SIRE_PROPUESTA actualizado: {resultado.Insertados} insertados, {resultado.Duplicados} duplicados, {resultado.Errores} errores");

            // ── PASO 7: Completado ────────────────────────────────────────────────
            job.Estado              = EstadoJob.Completado;
            job.FechaActualizacion  = DateTime.Now;
            job.FechaFinalizacion   = DateTime.Now;
            await _repo.UpdateJobAsync(job, stoppingToken);
            await LogFinAsync(SireOperacion.Completar, 0, true,
                $"Job completado exitosamente: {resultado.Insertados} reg. insertados, {resultado.Duplicados} duplicados");

            _logger.LogInformation(
                "[SIRE-WORKER] [{JobId}] ✓ Completado: {Ins} registros insertados, {Dup} duplicados, {Err} errores.",
                job.JobId, resultado.Insertados, resultado.Duplicados, resultado.Errores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIRE-WORKER] [{JobId}] Error procesando job.", job.JobId);
            try
            {
                var jobDb = await _repo.GetJobByIdAsync(jobId, stoppingToken) ?? job;

                // Errores transitorios de infraestructura SUNAT (502/503/504) o timeout de red:
                // se deja el job en EnProceso para que se reencole al reiniciar la app.
                var esTransitorio = ex is SireApiException sae && sae.StatusCode is
                    HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                    || ex is TaskCanceledException { CancellationToken.IsCancellationRequested: false }
                    || ex is HttpRequestException;

                if (esTransitorio)
                {
                    _logger.LogWarning("[SIRE-WORKER] [{JobId}] Error transitorio — job queda EnProceso para reencole al reiniciar.", job.JobId);
                    jobDb.Estado             = EstadoJob.EnProceso;
                    jobDb.MensajeError       = ex.Message[..Math.Min(500, ex.Message.Length)];
                    jobDb.FechaActualizacion = DateTime.Now;
                }
                else
                {
                    jobDb.Estado             = EstadoJob.Error;
                    jobDb.MensajeError       = ex.Message[..Math.Min(1000, ex.Message.Length)];
                    jobDb.FechaActualizacion = DateTime.Now;
                    jobDb.FechaFinalizacion  = DateTime.Now;
                }

                await _repo.UpdateJobAsync(jobDb, stoppingToken);
                await _repo.InsertApiLogAsync(new SireApiLog
                {
                    JobId     = job.JobId,
                    Operacion = EstadoJob.Error,
                    Exito     = false,
                    Mensaje   = ex.Message[..Math.Min(2000, ex.Message.Length)],
                    Fecha     = DateTime.Now,
                }, stoppingToken);
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "[SIRE-WORKER] [{JobId}] No se pudo persistir estado de error.", job.JobId);
            }
        }
    }

    /// <summary>
    /// Extrae el mensaje de error legible del JSON de error de SUNAT.
    /// El formato es: "Error SUNAT RCE: 422 [GET url] - {&quot;cod&quot;:422,&quot;msg&quot;:&quot;...&quot;,&quot;errors&quot;:[{&quot;cod&quot;:1070,&quot;msg&quot;:&quot;...&quot;}]}"
    /// Devuelve el primer mensaje de la lista errors si existe, o el msg principal.
    /// </summary>
    private static string ExtraerMensajeSunat(string exMessage)
    {
        try
        {
            var jsonStart = exMessage.IndexOf('{');
            if (jsonStart < 0) return exMessage[..Math.Min(500, exMessage.Length)];
            var json = exMessage[jsonStart..];
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                var firstError = errors[0];
                if (firstError.TryGetProperty("msg", out var errMsg))
                    return $"SUNAT: {errMsg.GetString()}";
            }
            if (root.TryGetProperty("msg", out var msg))
                return $"SUNAT: {msg.GetString()}";
        }
        catch { /* ignorar errores de parseo */ }
        return exMessage[..Math.Min(500, exMessage.Length)];
    }

    /// <summary>
    /// Guarda el archivo en la ruta de red UNC usando las credenciales de NetworkShare.
    /// </summary>
    private async Task GuardarEnRedAsync(
        string rutaDirectorio,
        string nombreArchivo,
        byte[] contenido,
        CancellationToken cancellationToken)
    {
        var username = _configuration["NetworkShare:Username"];
        var password = _configuration["NetworkShare:Password"];
        var domain   = _configuration["NetworkShare:Domain"];

        if (OperatingSystem.IsWindows() && !string.IsNullOrEmpty(username))
            Helpers.NetworkShareHelper.Connect(rutaDirectorio, username, password, domain);

        if (!Directory.Exists(rutaDirectorio))
            Directory.CreateDirectory(rutaDirectorio);

        var rutaCompleta = Path.Combine(rutaDirectorio, nombreArchivo);
        await File.WriteAllBytesAsync(rutaCompleta, contenido, cancellationToken);
    }

    /// <summary>
    /// Espera la inicialización lazy de SIRE con auto-inicialización tras 2 minutos.
    /// Permite que la app funcione de forma desatendida después de un reinicio nocturno:
    /// si ningún usuario navega a Contabilidad → SIRE, el worker se auto-inicializa.
    /// </summary>
    private async Task EnsureSireInitializedAsync(CancellationToken stoppingToken)
    {
        if (_lazySireInitializer.IsInitialized) return;

        _logger.LogInformation(
            "[SIRE-WORKER] Esperando inicialización de SIRE. Si no hay actividad en 2 min, se auto-inicializará.");

        using var autoInitCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        autoInitCts.CancelAfter(TimeSpan.FromMinutes(2));

        try
        {
            await _lazySireInitializer.WaitForInitializationAsync(autoInitCts.Token);
            _logger.LogInformation("[SIRE-WORKER] SIRE inicializado por el usuario. Iniciando procesamiento de jobs.");
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // Timeout de gracia: inicializar automáticamente para operación desatendida.
            _logger.LogInformation("[SIRE-WORKER] Auto-inicializando SIRE para operación desatendida...");
            try
            {
                await _lazySireInitializer.InitializeAsync();
                _logger.LogInformation("[SIRE-WORKER] Auto-inicialización completada. Iniciando procesamiento de jobs.");
            }
            catch (Exception initEx)
            {
                _logger.LogError(initEx,
                    "[SIRE-WORKER] Error en auto-inicialización (¿SUNAT inaccesible?). Esperando inicialización manual.");
                // Esperar indefinidamente a que alguien inicie sesión y navegue a SIRE.
                await _lazySireInitializer.WaitForInitializationAsync(stoppingToken);
            }
        }
    }
}
