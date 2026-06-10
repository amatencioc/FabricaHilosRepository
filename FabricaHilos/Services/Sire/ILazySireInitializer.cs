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
    /// <returns>Task completada cuando la inicialización es exitosa</returns>
    Task InitializeAsync();

    /// <summary>
    /// Verifica si los servicios SIRE ya han sido inicializados.
    /// </summary>
    bool IsInitialized { get; }
}
