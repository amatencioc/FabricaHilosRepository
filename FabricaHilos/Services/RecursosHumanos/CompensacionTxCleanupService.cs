namespace FabricaHilos.Services.RecursosHumanos;

/// <summary>
/// BackgroundService que elimina transacciones de compensación que quedaron abiertas
/// (usuario cerró el browser, caída del servidor, timeout de sesión, etc.).
/// Corre cada 5 minutos y elimina entradas con más de 30 minutos de antigüedad.
/// </summary>
public class CompensacionTxCleanupService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxTxAge      = TimeSpan.FromMinutes(30);

    private readonly ILogger<CompensacionTxCleanupService> _logger;

    public CompensacionTxCleanupService(ILogger<CompensacionTxCleanupService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CompensacionTxCleanupService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false);
            await LimpiarHuerfanasAsync();
        }

        // Al detener el servidor, hacer rollback de todo lo que quede abierto
        await LimpiarTodasAsync();
        _logger.LogInformation("CompensacionTxCleanupService detenido — todas las transacciones descartadas.");
    }

    private async Task LimpiarHuerfanasAsync()
    {
        var corte = DateTime.UtcNow - MaxTxAge;
        var huerfanas = CompensacionDiaDiaService._activeTx
            .Where(kv => kv.Value.CreatedAt < corte)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var sessionId in huerfanas)
        {
            _logger.LogWarning(
                "Transacción huérfana encontrada para sesión {SessionId} (creada hace más de {Max} min). Descartando.",
                sessionId, (int)MaxTxAge.TotalMinutes);

            await CompensacionDiaDiaService.DisposeTransactionAsync(sessionId);
        }
    }

    private async Task LimpiarTodasAsync()
    {
        var todas = CompensacionDiaDiaService._activeTx.Keys.ToList();
        foreach (var sessionId in todas)
        {
            try
            {
                await CompensacionDiaDiaService.DisposeTransactionAsync(sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descartar transacción al apagar para sesión {SessionId}", sessionId);
            }
        }
    }
}
