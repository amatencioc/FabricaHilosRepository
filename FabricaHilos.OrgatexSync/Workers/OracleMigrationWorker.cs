namespace FabricaHilos.OrgatexSync.Workers;

using System.Diagnostics;
using System.Linq;
using FabricaHilos.OrgatexSync.Config;
using FabricaHilos.OrgatexSync.Data;
using FabricaHilos.OrgatexSync.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// BackgroundService que migra en tiempo real dbo.RecipeSnapshot_Cabecera/Detalle
/// (ORGATEX) hacia Oracle SIG: ING_RECETAS_G/D (PKG_ORGATEX.SP_MERGE_ING_RECETA) y
/// PARTIDA_MAS (SP_MERGE_PARTIDA_MAS). Sync CONTINUO: cada ciclo migra/re-sincroniza
/// TODA cabecera con detalle cuyo ROWVERSION (v3.0) avanzó desde el último watermark
/// exitoso -- sin exigir Terminated (se migra desde que la receta existe, típicamente
/// en estado Queued, antes de que el batch vaya a máquina), y sin importar si ya
/// había "cerrado" antes (IngRecetaMigrado quedó como flag solo informativo). Antes de
/// reintentar una cabecera ya sincronizada antes, se verifica que su header siga
/// existiendo en Oracle -- si un usuario lo borró/anuló intencionalmente allá, no se
/// recrea (se marca EliminadoEnOracle=1 y se deja de intentar, ver
/// OracleMigrationRepository.MigrarIngRecetaAsync). Fase 2 (PARTIDA_MAS) es igual de
/// temprana: vincula apenas haya candidatas en dbo.RecipeSnapshot_CabeceraPartida (v3.2:
/// hasta N partidas por receta, detectadas por patrón en BatchDetail, 1 fila por
/// partida -- no depende de una única "Partida" por cabecera), sin esperar el
/// cierre de Fase 1 ni Terminated. El estado se persiste en
/// dbo.RecipeSnapshot_OracleSync (Fase 1) / dbo.RecipeSnapshot_CabeceraPartida (Fase 2)
/// -- ambos pasos son idempotentes y reintentables sin
/// duplicar nada en Oracle.
/// </summary>
public sealed class OracleMigrationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OracleMigrationWorker> _logger;
    private readonly OracleMigrationOptions _opciones;

    public OracleMigrationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OracleMigrationWorker> logger,
        IOptions<OracleMigrationOptions> opciones)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _opciones = opciones.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.WorkerActivo)
        {
            _logger.LogWarning("[ORACLE-MIGRATION] Worker DESHABILITADO (OracleMigrationSync:WorkerActivo = false).");
            return;
        }

        var intervalo = _opciones.IntervaloMs > 0
            ? TimeSpan.FromMilliseconds(_opciones.IntervaloMs)
            : TimeSpan.FromMilliseconds(5000);

        if (_opciones.IntervaloMs <= 0)
        {
            _logger.LogWarning(
                "[ORACLE-MIGRATION] OracleMigrationSync:IntervaloMs inválido ({Valor}); se usa el default de {Default} ms.",
                _opciones.IntervaloMs, intervalo.TotalMilliseconds);
        }

        _logger.LogInformation(
            "[ORACLE-MIGRATION] Worker iniciado. Ciclo cada {Intervalo} ms, ventana de gracia {Ventana}s para el cierre final tras Terminated.",
            intervalo.TotalMilliseconds, _opciones.VentanaGraciaSegundos);

        // PeriodicTimer: mismo patrón que RecipeSnapshotWorker (sin drift, sin ticks
        // encolados mientras un ciclo largo todavía está migrando líneas a Oracle).
        using var timer = new PeriodicTimer(intervalo);

        try
        {
            do
            {
                await EjecutarCicloAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Cancelación esperada durante el shutdown del host.
        }

        _logger.LogInformation("[ORACLE-MIGRATION] Worker detenido.");
    }

    private async Task EjecutarCicloAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOracleMigrationRepository>();

            var inicio = Stopwatch.GetTimestamp();

            // Fase 1: migrar/re-sincronizar recetas en curso o recién cerradas (ING_RECETAS_G/D).
            var (pendientesIngReceta, watermark) = await repo.ObtenerCabecerasPendientesIngRecetaAsync(stoppingToken);
            var (recetasOk, recetasFail) = await MigrarPendientesIngRecetaAsync(repo, pendientesIngReceta, watermark, stoppingToken);

            // Fase 2: vincular PARTIDA_MAS para recetas ya migradas.
            var pendientesPartida = await repo.ObtenerCabecerasPendientesPartidaAsync(stoppingToken);
            var (partidasOk, partidasFail) = await VincularPendientesPartidaAsync(repo, pendientesPartida, stoppingToken);

            if (pendientesIngReceta.Count > 0 || pendientesPartida.Count > 0)
            {
                var duracion = Stopwatch.GetElapsedTime(inicio);
                _logger.LogInformation(
                    "[ORACLE-MIGRATION] Ciclo OK — ING_RECETA: {RecetasOk} ok/{RecetasFail} con error. PARTIDA_MAS: {PartidasOk} ok/{PartidasFail} con error. ({DuracionMs} ms).",
                    recetasOk, recetasFail, partidasOk, partidasFail, (int)duracion.TotalMilliseconds);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ORACLE-MIGRATION] Error en ciclo de migración. Se reintenta en el próximo ciclo.");
        }
    }

    /// <summary>
    /// Migra cada cabecera pendiente (independiente entre sí: su propia conexión y su
    /// propio registro en RecipeSnapshot_OracleSync) con paralelismo acotado, para no
    /// pagar N veces la latencia secuencial cuando varias recetas terminan casi juntas.
    /// </summary>
    private async Task<(int Ok, int Fail)> MigrarPendientesIngRecetaAsync(
        IOracleMigrationRepository repo, IReadOnlyList<RecipeCabeceraPendiente> pendientes, byte[] watermark, CancellationToken stoppingToken)
    {
        int recetasOk = 0, recetasFail = 0;

        // Trae el detalle de TODAS las cabeceras pendientes en una sola consulta batch
        // (en vez de una consulta SQL Server por cabecera dentro del Parallel.ForEachAsync),
        // reduciendo la cantidad de conexiones/round-trips a SQL Server por ciclo.
        var dyelotRefNos = pendientes.Select(p => p.DyelotRefNo).ToList();
        var detallesPorDyelot = await repo.ObtenerDetallesBatchAsync(dyelotRefNos, stoppingToken);

        await Parallel.ForEachAsync(
            pendientes,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _opciones.MaxGradoParalelismo), CancellationToken = stoppingToken },
            async (cabecera, ct) =>
            {
                var detalle = detallesPorDyelot.TryGetValue(cabecera.DyelotRefNo, out var lineas)
                    ? lineas
                    : Array.Empty<RecipeDetalleLinea>();

                var (ok, fail) = await repo.MigrarIngRecetaAsync(cabecera, detalle, watermark, _opciones.VentanaGraciaSegundos, ct);
                if (fail == 0 && ok > 0)
                {
                    Interlocked.Increment(ref recetasOk);
                    _logger.LogInformation(
                        "[ORACLE-MIGRATION] {Dyelot}: ING_RECETA migrada OK ({Lineas} línea(s)).",
                        cabecera.DyelotRefNo, ok);
                }
                else if (fail > 0)
                {
                    Interlocked.Increment(ref recetasFail);
                }
            });

        return (recetasOk, recetasFail);
    }

    /// <summary>
    /// Vincula PARTIDA_MAS para cada partida candidata pendiente (v3.2: 1 fila por
    /// partida detectada en dbo.RecipeSnapshot_CabeceraPartida, no 1 por cabecera --
    /// una misma receta puede aportar hasta N candidatas) con el mismo paralelismo
    /// acotado que <see cref="MigrarPendientesIngRecetaAsync"/>.
    /// </summary>
    private async Task<(int Ok, int Fail)> VincularPendientesPartidaAsync(
        IOracleMigrationRepository repo, IReadOnlyList<PartidaCandidata> pendientes, CancellationToken stoppingToken)
    {
        int partidasOk = 0, partidasFail = 0;

        await Parallel.ForEachAsync(
            pendientes,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _opciones.MaxGradoParalelismo), CancellationToken = stoppingToken },
            async (candidata, ct) =>
            {
                var vinculada = await repo.VincularPartidaAsync(candidata, ct);
                if (vinculada)
                {
                    Interlocked.Increment(ref partidasOk);
                    _logger.LogInformation("[ORACLE-MIGRATION] {Dyelot}: PARTIDA_MAS '{Partida}' vinculada OK.", candidata.DyelotRefNo, candidata.Partida);
                }
                else
                {
                    Interlocked.Increment(ref partidasFail);
                }
            });

        return (partidasOk, partidasFail);
    }
}
