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
/// BackgroundService que procesa jobs de exportación SIRE de forma completamente desacoplada del frontend.
/// Flujo por job:
///   1. Llama ExportarPropuestaAsync() → obtiene numTicket de SUNAT.
///   2. Hace polling ConsultarTicketAsync() hasta que esFinal = true.
///   3. Llama DescargarArchivoReporteAsync() → obtiene el ZIP de propuesta.
///   4. Guarda el ZIP en la ruta de red \\10.0.7.14\FabricaHilos\Contabilidad\Sire\{Ventas|Compras}.
///   5. Parsea el TXT del ZIP e inserta/actualiza en Oracle SIG.SIRE_VALIDA.
///   6. Actualiza el estado del job en Oracle SIG.SIRE_JOB.
/// </summary>
public sealed class SireExportacionWorker : BackgroundService
{
    private readonly IServiceScopeFactory           _scopeFactory;
    private readonly ISireExportacionQueue          _queue;
    private readonly IConfiguration                 _configuration;
    private readonly ISireOracleRepository          _repo;
    private readonly ILogger<SireExportacionWorker> _logger;
    private readonly ILazySireInitializer           _lazySireInitializer;

    public SireExportacionWorker(
        IServiceScopeFactory            scopeFactory,
        ISireExportacionQueue           queue,
        IConfiguration                  configuration,
        ISireOracleRepository           repo,
        ILogger<SireExportacionWorker>  logger,
        ILazySireInitializer            lazySireInitializer)
    {
        _scopeFactory        = scopeFactory;
        _queue               = queue;
        _configuration       = configuration;
        _repo                = repo;
        _logger              = logger;
        _lazySireInitializer = lazySireInitializer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SIRE-WORKER] Servicio de exportación asíncrona iniciado.");

        // Esperar a que el usuario acceda a Contabilidad → SIRE antes de procesar jobs.
        // Esto evita llamadas a SUNAT en startup sin intervención del usuario.
        _logger.LogInformation("[SIRE-WORKER] Esperando inicialización lazy de SIRE (requiere acceso a Contabilidad → SIRE)...");
        await _lazySireInitializer.WaitForInitializationAsync(stoppingToken);
        if (stoppingToken.IsCancellationRequested) return;
        _logger.LogInformation("[SIRE-WORKER] SIRE inicializado. Iniciando procesamiento de jobs.");

        // Al arrancar, reencolar jobs que quedaron en estado Pendiente o EnProceso
        await ReencolarJobsInterrumpidosAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await _queue.DequeueAsync(stoppingToken);
                await ProcesarJobAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIRE-WORKER] Error inesperado en el loop principal del worker.");
                await Task.Delay(5000, stoppingToken);
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
                Fecha      = DateTime.UtcNow,
            };
            await _repo.InsertApiLogAsync(entry, stoppingToken);
        }

        try
        {
            // ── PASO 1: Marcar en proceso ──────────────────────────────────────────
            job.Estado             = EstadoJob.EnProceso;
            job.FechaActualizacion = DateTime.UtcNow;
            await _repo.UpdateJobAsync(job, stoppingToken);
            await LogFinAsync(SireOperacion.Exportar, 0, true,
                $"Job iniciado: tipo={job.TipoRegistro} periodo={job.Periodo} usuario={job.UsuarioId}");

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
                    job.FechaActualizacion = DateTime.UtcNow;
                    await _repo.UpdateJobAsync(job, stoppingToken);
                    await LogFinAsync(SireOperacion.Exportar, swExportar.ElapsedMilliseconds, true,
                        $"Ticket obtenido: {ticketInicial.NumTicket}");
                    _logger.LogInformation("[SIRE-WORKER] [{JobId}] Ticket obtenido: {Ticket}", job.JobId, job.NumTicket);
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
            catch
            {
                swTicket.Stop();
                await LogFinAsync(SireOperacion.Ticket, swTicket.ElapsedMilliseconds, false, "Error en polling de ticket");
                throw;
            }

            job.CodProceso         = ticketFinal.CodProceso;
            job.NombreArchivo      = ticketFinal.ArchivoReporte?.NomArchivoReporte;
            job.CodTipoArchivo     = ticketFinal.ArchivoReporte?.CodTipoArchivoReporte;
            job.FechaActualizacion = DateTime.UtcNow;
            await _repo.UpdateJobAsync(job, stoppingToken);

            // Caso 1: SUNAT no terminó de procesar dentro del tiempo límite de polling.
            if (string.Equals(ticketFinal.Estado, "TIMEOUT", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"SUNAT no completó la exportación en el tiempo máximo de espera. {ticketFinal.Mensaje} "
                    + "Reintente la exportación en unos minutos.");

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
                ticketFinal.CodProceso,
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
            job.FechaActualizacion = DateTime.UtcNow;
            await _repo.UpdateJobAsync(job, stoppingToken);
            await LogFinAsync(SireOperacion.Guardar, 0, true,
                $"Archivo guardado: {job.RutaArchivo}");

            _logger.LogInformation("[SIRE-WORKER] [{JobId}] Archivo guardado en: {Ruta}", job.JobId, job.RutaArchivo);

            // ── PASO 6:
            _logger.LogInformation("[SIRE-WORKER] [{JobId}] Cargando propuesta en Oracle SIRE_VALIDA...", job.JobId);
            var swCargar = System.Diagnostics.Stopwatch.StartNew();
            var resultado = await validaService.CargarDesdeZipAsync(
                archivo.Contenido, job.TipoRegistro, job.Periodo, stoppingToken);
            swCargar.Stop();

            job.RegistrosInsertados  = resultado.Insertados;
            job.RegistrosDuplicados  = resultado.Duplicados;
            await LogFinAsync(SireOperacion.Cargar, swCargar.ElapsedMilliseconds, true,
                $"Oracle SIRE_VALIDA actualizado: {resultado.Insertados} insertados, {resultado.Duplicados} duplicados, {resultado.Errores} errores");

            // ── PASO 7: Completado ────────────────────────────────────────────────
            job.Estado              = EstadoJob.Completado;
            job.FechaActualizacion  = DateTime.UtcNow;
            job.FechaFinalizacion   = DateTime.UtcNow;
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
                    jobDb.FechaActualizacion = DateTime.UtcNow;
                }
                else
                {
                    jobDb.Estado             = EstadoJob.Error;
                    jobDb.MensajeError       = ex.Message[..Math.Min(1000, ex.Message.Length)];
                    jobDb.FechaActualizacion = DateTime.UtcNow;
                    jobDb.FechaFinalizacion  = DateTime.UtcNow;
                }

                await _repo.UpdateJobAsync(jobDb, stoppingToken);
                await _repo.InsertApiLogAsync(new SireApiLog
                {
                    JobId     = job.JobId,
                    Operacion = EstadoJob.Error,
                    Exito     = false,
                    Mensaje   = ex.Message[..Math.Min(2000, ex.Message.Length)],
                    Fecha     = DateTime.UtcNow,
                }, stoppingToken);
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "[SIRE-WORKER] [{JobId}] No se pudo persistir estado de error.", job.JobId);
            }
        }
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
}
