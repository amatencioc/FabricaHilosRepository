namespace FabricaHilos.OrgatexSync.Workers;

using System.Diagnostics;
using FabricaHilos.OrgatexSync.Config;
using FabricaHilos.OrgatexSync.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// BackgroundService que reemplaza al trigger trg_tmpProductionRecipe_Snapshot
/// (ver ORGATEX/snapshot_recipe_trigger.sql), deshabilitado permanentemente porque
/// SQL Server prohíbe cualquier trigger sobre dbo.tmpProductionRecipe mientras el
/// cliente OrgaTex inserte con OUTPUT sin INTO (Msg 334). Cada
/// <see cref="RecipeSnapshotOptions.IntervaloMs"/> lee tmpProductionRecipe (solo
/// SELECT) y hace MERGE hacia RecipeSnapshot_Detalle/RecipeSnapshot_Cabecera antes
/// de que la tabla transitoria se vacíe. Al correr en su propia conexión/transacción,
/// nunca puede interferir con la impresión real de recetas del cliente OrgaTex.
/// </summary>
public sealed class RecipeSnapshotWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecipeSnapshotWorker> _logger;
    private readonly RecipeSnapshotOptions _opciones;

    public RecipeSnapshotWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<RecipeSnapshotWorker> logger,
        IOptions<RecipeSnapshotOptions> opciones)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _opciones = opciones.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.WorkerActivo)
        {
            _logger.LogWarning("[RECIPE-SNAPSHOT] Worker DESHABILITADO (RecipeSnapshotSync:WorkerActivo = false).");
            return;
        }

        var intervalo = _opciones.IntervaloMs > 0
            ? TimeSpan.FromMilliseconds(_opciones.IntervaloMs)
            : TimeSpan.FromMilliseconds(1500);

        if (_opciones.IntervaloMs <= 0)
        {
            _logger.LogWarning(
                "[RECIPE-SNAPSHOT] RecipeSnapshotSync:IntervaloMs inválido ({Valor}); se usa el default de {Default} ms.",
                _opciones.IntervaloMs, intervalo.TotalMilliseconds);
        }

        _logger.LogInformation(
            "[RECIPE-SNAPSHOT] Worker iniciado. Polling de tmpProductionRecipe cada {Intervalo} ms.",
            intervalo.TotalMilliseconds);

        // PeriodicTimer: no acumula drift, no encola ticks mientras el ciclo anterior
        // sigue corriendo (comportamiento secuencial equivalente al Task.Delay previo,
        // pero sin doble manejo de OperationCanceledException ni allocations extra por ciclo).
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

        _logger.LogInformation("[RECIPE-SNAPSHOT] Worker detenido.");
    }

    private async Task EjecutarCicloAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRecipeSnapshotRepository>();

            var inicio = Stopwatch.GetTimestamp();
            var (filasDetalle, filasCabecera, filasCerradas) = await repo.SincronizarAsync(stoppingToken);

            if (filasDetalle > 0 || filasCabecera > 0 || filasCerradas > 0)
            {
                var duracion = Stopwatch.GetElapsedTime(inicio);
                _logger.LogInformation(
                    "[RECIPE-SNAPSHOT] Ciclo OK — {FilasDetalle} fila(s) detalle, {FilasCabecera} fila(s) cabecera, {FilasCerradas} cabecera(s) cerrada(s) con Terminated ({DuracionMs} ms).",
                    filasDetalle, filasCabecera, filasCerradas, (int)duracion.TotalMilliseconds);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RECIPE-SNAPSHOT] Error en ciclo de polling. Se reintenta en el próximo ciclo.");
        }
    }
}
