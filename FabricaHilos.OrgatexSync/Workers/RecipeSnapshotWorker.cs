namespace FabricaHilos.OrgatexSync.Workers;

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

        _logger.LogInformation(
            "[RECIPE-SNAPSHOT] Worker iniciado. Polling de tmpProductionRecipe cada {Intervalo} ms.",
            _opciones.IntervaloMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IRecipeSnapshotRepository>();

                var (filasDetalle, filasCabecera) = await repo.SincronizarAsync(stoppingToken);

                if (filasDetalle > 0 || filasCabecera > 0)
                {
                    _logger.LogInformation(
                        "[RECIPE-SNAPSHOT] Ciclo OK — {FilasDetalle} fila(s) detalle, {FilasCabecera} fila(s) cabecera.",
                        filasDetalle, filasCabecera);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RECIPE-SNAPSHOT] Error en ciclo de polling. Se reintenta en el próximo ciclo.");
            }

            try
            {
                await Task.Delay(_opciones.IntervaloMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("[RECIPE-SNAPSHOT] Worker detenido.");
    }
}
