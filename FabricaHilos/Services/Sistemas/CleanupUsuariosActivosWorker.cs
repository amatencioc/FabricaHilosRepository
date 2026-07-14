namespace FabricaHilos.Services.Sistemas;

/// <summary>
/// Elimina cada 1 minuto los registros de usuarios sin actividad en los ultimos 3 minutos.
/// Un usuario sin requests en 3 min probablemente cerro el navegador o esta inactivo.
/// </summary>
public sealed class CleanupUsuariosActivosWorker(
    UsuarioActivoStore store,
    ILogger<CleanupUsuariosActivosWorker> logger) : BackgroundService
{
    private static readonly TimeSpan _intervalo    = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan _tiempoLimite = TimeSpan.FromMinutes(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CleanupUsuariosActivos iniciado.");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_intervalo, stoppingToken);
            try
            {
                store.LimpiarInactivos(_tiempoLimite);
                logger.LogDebug("Limpieza usuarios activos — activos: {n}", store.CantidadActivos);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error en limpieza de usuarios activos. El worker continuará en el próximo ciclo.");
            }
        }
    }
}
