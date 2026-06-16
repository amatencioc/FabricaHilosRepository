using FabricaHilos.Models.Sire;
using FabricaHilos.Sire.Constants;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using FabricaHilos.Sire.Services;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// BackgroundService ligero: Fase 2 del polling inteligente de tickets SUNAT.
/// Despierta cada WatcherIntervalMin minutos (default: 15 min) y consulta
/// todos los jobs en estado EsperandoTicket cuya ProximaConsulta ya venció.
///
/// Flujo por ciclo:
///   1. Busca en Oracle: ESTADO='EsperandoTicket' AND PROXIMA_CONSULTA &lt;= NOW
///   2. Por cada job (ventas o compras): llama ConsultarTicketAsync una sola vez.
///   3a. Ticket listo → re-encola en SireExportacionQueue para continuar Fase 1 (descarga, etc.)
///   3b. Ticket no listo → actualiza PROXIMA_CONSULTA = ahora + WatcherIntervalMin
///   4. Duerme WatcherIntervalMin minutos.
/// </summary>
public sealed class SireTicketWatcherWorker : BackgroundService
{
    private readonly IServiceScopeFactory              _scopeFactory;
    private readonly ISireExportacionQueue             _queue;
    private readonly ISireOracleRepository             _repo;
    private readonly ILazySireInitializer              _lazySireInitializer;
    private readonly ILogger<SireTicketWatcherWorker>  _logger;
    private readonly SireOptions                       _options;

    public SireTicketWatcherWorker(
        IServiceScopeFactory             scopeFactory,
        ISireExportacionQueue            queue,
        ISireOracleRepository            repo,
        ILazySireInitializer             lazySireInitializer,
        ILogger<SireTicketWatcherWorker> logger,
        IOptions<SireOptions>            options)
    {
        _scopeFactory        = scopeFactory;
        _queue               = queue;
        _repo                = repo;
        _lazySireInitializer = lazySireInitializer;
        _logger              = logger;
        _options             = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SIRE-WATCHER] Servicio de vigilancia de tickets iniciado " +
                               "(intervalo: {Min} min).", _options.WatcherIntervalMin);

        // Esperar la misma inicialización lazy que el worker principal, con auto-init tras 2 min.
        await EnsureSireInitializedAsync(stoppingToken);
        if (stoppingToken.IsCancellationRequested) return;

        _logger.LogInformation("[SIRE-WATCHER] SIRE inicializado. Iniciando ciclos de vigilancia.");

        // Ejecutar un ciclo inmediato al arrancar para recuperar jobs que ya tienen
        // ProximaConsulta vencida (por reinicios del servidor u otros desfases de timing).
        try
        {
            await EjecutarCicloAsync(stoppingToken);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIRE-WATCHER] Error en ciclo inicial de arranque.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // Dormir hasta el próximo ciclo.
            await Task.Delay(TimeSpan.FromMinutes(_options.WatcherIntervalMin), stoppingToken);

            try
            {
                await EjecutarCicloAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIRE-WATCHER] Error inesperado en ciclo de vigilancia.");
            }
        }

        _logger.LogInformation("[SIRE-WATCHER] Servicio de vigilancia de tickets detenido.");
    }

    /// <summary>
    /// Ejecuta un ciclo completo: consulta todos los jobs pendientes de ticket
    /// y decide si re-encolar (listo) o reprogramar (no listo).
    /// </summary>
    private async Task EjecutarCicloAsync(CancellationToken ct)
    {
        var jobs = await _repo.GetJobsEsperandoTicketAsync(ct);

        if (jobs.Count == 0)
        {
            _logger.LogDebug("[SIRE-WATCHER] Sin jobs en EsperandoTicket. Próximo ciclo en {Min} min.",
                _options.WatcherIntervalMin);
            return;
        }

        _logger.LogInformation("[SIRE-WATCHER] {Count} job(s) en EsperandoTicket. Consultando...",
            jobs.Count);

        using var scope          = _scopeFactory.CreateScope();
        var ventasService        = scope.ServiceProvider.GetRequiredService<ISireVentasService>();
        var comprasService       = scope.ServiceProvider.GetRequiredService<ISireComprasService>();

        foreach (var job in jobs)
        {
            if (ct.IsCancellationRequested) break;
            await ConsultarTicketJobAsync(job, ventasService, comprasService, ct);
        }
    }

    /// <summary>
    /// Consulta el estado del ticket de un job en SUNAT (1 sola llamada).
    /// Si está listo → re-encola. Si no → reprograma.
    /// </summary>
    private async Task ConsultarTicketJobAsync(
        SireExportacionJob job,
        ISireVentasService ventasService,
        ISireComprasService comprasService,
        CancellationToken ct)
    {
        var esVentas = job.TipoRegistro.Equals("ventas", StringComparison.OrdinalIgnoreCase);
        var sw       = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation(
            "[SIRE-WATCHER] [{JobId}] Consultando ticket {Ticket} ({Tipo}/{Periodo})...",
            job.JobId, job.NumTicket, job.TipoRegistro, job.Periodo);

        try
        {
            // Bug 3: NumTicket puede ser null si el job pasó a EsperandoTicket antes de persistir el ticket.
            if (string.IsNullOrWhiteSpace(job.NumTicket))
            {
                _logger.LogError(
                    "[SIRE-WATCHER] [{JobId}] Job en EsperandoTicket sin NumTicket. Se marca como Error.",
                    job.JobId);
                job.Estado             = EstadoJob.Error;
                job.MensajeError       = "EsperandoTicket sin NumTicket: estado inconsistente.";
                job.ProximaConsulta    = null;
                job.FechaActualizacion = DateTime.Now;
                job.FechaFinalizacion  = DateTime.Now;
                await _repo.UpdateJobAsync(job, ct);
                return;
            }

            TicketEstado estado;
            try
            {
                estado = esVentas
                    ? await ventasService.ConsultarTicketAsync(job.NumTicket, job.Periodo, ct)
                    : await comprasService.ConsultarTicketAsync(job.NumTicket, job.Periodo, ct);
                sw.Stop();
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogWarning(ex,
                    "[SIRE-WATCHER] [{JobId}] Error al consultar ticket {Ticket}. Se reprograma.",
                    job.JobId, job.NumTicket);

                await RegistrarLogAsync(job, sw.ElapsedMilliseconds, exito: false,
                    $"Error en consulta watcher: {ex.Message}");
                await ReprogramarJobAsync(job, ct);
                return;
            }

            await RegistrarLogAsync(job, sw.ElapsedMilliseconds, exito: true,
                $"[Watcher] Estado SUNAT: {estado.Estado} — {estado.Mensaje}");

            if (estado.EsFinal && !string.IsNullOrWhiteSpace(estado.ArchivoReporte?.NomArchivoReporte))
            {
                // Ticket listo: devolver al SireExportacionWorker para continuar desde Paso 4.
                job.Estado             = EstadoJob.EnProceso;
                job.ProximaConsulta    = null;
                job.FechaActualizacion = DateTime.Now;
                // Preservar datos del ticket que devuelve SUNAT.
                job.CodProceso     = estado.CodProceso     ?? job.CodProceso;
                job.NombreArchivo  = estado.ArchivoReporte?.NomArchivoReporte ?? job.NombreArchivo;
                job.CodTipoArchivo = estado.ArchivoReporte?.CodTipoArchivoReporte ?? job.CodTipoArchivo;
                await _repo.UpdateJobAsync(job, ct);

                _queue.Encolar(job.Id);

                _logger.LogInformation(
                    "[SIRE-WATCHER] [{JobId}] ✓ Ticket {Ticket} listo. Job reencolado para descarga.",
                    job.JobId, job.NumTicket);
            }
            else if (estado.EsFinal)
            {
                // SUNAT finalizó pero sin archivo (error de SUNAT, no nuestro).
                job.Estado             = EstadoJob.Error;
                job.ProximaConsulta    = null;
                job.MensajeError       = $"SUNAT finalizó ticket {job.NumTicket} sin archivo: {estado.Mensaje}";
                job.FechaActualizacion = DateTime.Now;
                job.FechaFinalizacion  = DateTime.Now;
                await _repo.UpdateJobAsync(job, ct);

                _logger.LogWarning(
                    "[SIRE-WATCHER] [{JobId}] Ticket {Ticket} finalizado sin archivo. Estado SUNAT: {Estado}.",
                    job.JobId, job.NumTicket, estado.Estado);
            }
            else
            {
                // SUNAT aún procesando: reprogramar próxima consulta.
                await ReprogramarJobAsync(job, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIRE-WATCHER] [{JobId}] Error inesperado procesando job.", job.JobId);
        }
    }

    /// <summary>Actualiza ProximaConsulta al siguiente ciclo sin cambiar el estado.</summary>
    private async Task ReprogramarJobAsync(SireExportacionJob job, CancellationToken ct)
    {
        var proxima = DateTime.Now.AddMinutes(_options.WatcherIntervalMin);
        job.ProximaConsulta    = proxima;
        job.FechaActualizacion = DateTime.Now;
        await _repo.UpdateJobAsync(job, ct);

        _logger.LogInformation(
            "[SIRE-WATCHER] [{JobId}] SUNAT aún procesando. Próxima consulta: {Proxima:dd/MM/yyyy HH:mm}.",
            job.JobId, proxima);
    }

    /// <summary>Inserta un registro de auditoría en SIRE_LOG. No interrumpe el flujo.</summary>
    private async Task RegistrarLogAsync(
        SireExportacionJob job,
        long duracionMs,
        bool exito,
        string mensaje)
    {
        await _repo.InsertApiLogAsync(new SireApiLog
        {
            JobId      = job.JobId,
            Operacion  = SireOperacion.Ticket,
            DuracionMs = duracionMs,
            Exito      = exito,
            Mensaje    = mensaje,
            Fecha      = DateTime.Now,
        }, CancellationToken.None); // fire-and-forget seguro
    }

    /// <summary>
    /// Espera la inicialización lazy de SIRE con auto-inicialización tras 2 minutos.
    /// Idéntico al comportamiento del SireExportacionWorker para consistencia.
    /// </summary>
    private async Task EnsureSireInitializedAsync(CancellationToken stoppingToken)
    {
        if (_lazySireInitializer.IsInitialized) return;

        _logger.LogInformation(
            "[SIRE-WATCHER] Esperando inicialización de SIRE. Si no hay actividad en 2 min, se auto-inicializará.");

        using var autoInitCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        autoInitCts.CancelAfter(TimeSpan.FromMinutes(2));

        try
        {
            await _lazySireInitializer.WaitForInitializationAsync(autoInitCts.Token);
            _logger.LogInformation("[SIRE-WATCHER] SIRE inicializado por el usuario. Iniciando ciclos de vigilancia.");
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[SIRE-WATCHER] Auto-inicializando SIRE para operación desatendida...");
            try
            {
                await _lazySireInitializer.InitializeAsync();
                _logger.LogInformation("[SIRE-WATCHER] Auto-inicialización completada. Iniciando ciclos de vigilancia.");
            }
            catch (Exception initEx)
            {
                _logger.LogError(initEx,
                    "[SIRE-WATCHER] Error en auto-inicialización (¿SUNAT inaccesible?). Esperando inicialización manual.");
                await _lazySireInitializer.WaitForInitializationAsync(stoppingToken);
            }
        }
    }
}
