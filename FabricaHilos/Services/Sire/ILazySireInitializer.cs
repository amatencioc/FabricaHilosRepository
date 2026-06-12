namespace FabricaHilos.Services.Sire;

/// <summary>
/// Interfaz para inicializar lazy los servicios SIRE.
/// Los servicios SIRE se inicializan solo cuando se accede al módulo de Contabilidad,
/// reduciendo tiempo de startup y consumo de recursos innecesarios.
/// </summary>
public interface ILazySireInitializer
{
    /// <summary>
    /// Inicializa los servicios SIRE de manera diferida (lazy).
    /// Esta operación es idempotente: llamarla múltiples veces es seguro.
    /// </summary>
    Task InitializeAsync();

    /// <summary>Verifica si los servicios SIRE ya han sido inicializados.</summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Espera de forma asíncrona hasta que InitializeAsync() haya completado.
    /// Usado por el worker para no procesar jobs antes de que el usuario acceda a SIRE.
    /// </summary>
    Task WaitForInitializationAsync(CancellationToken cancellationToken = default);
}
